using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Scripting;
using MonkeyWebServer;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Scriban;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using System.Web;

#region MonkeyServer

public class PluginConfig
{
    public string NameSpace = string.Empty;
    public string Class = string.Empty;
    public string Function = string.Empty;
    public string PluginName = string.Empty;

    public PluginConfig()
    {

    }
}

public class MonkeyEndpointSyntaxWalker : CSharpSyntaxWalker
{
    public string ClassName { get; private set; }

    public override void VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        if (node.BaseList?.Types.Any(t => t.Type.ToString() == "MonkeyEndpoint") == true)
        {
            ClassName = node.Identifier.Text;
        }

        base.VisitClassDeclaration(node);
    }
}


public interface IMonkeyPlugin
{
    public MonkeyEndpoint GetInstance();
}

public class Resource
{
    public string Name { get; private set; }
    public string PathInMemory { get; private set; }
    public List<Resource> SubDirectories { get; private set; }

    public List<string> Resources { get; private set; }

    public Resource(string name, string pathInMemory)
    {
        Name = name;
        PathInMemory = pathInMemory;
        SubDirectories = new List<Resource>();
    }

    public Resource(string name, Resource[] subresoucres,string pathInMemory, string[] resources)
    {
        Name = name;

        Resources = resources.ToList();
        PathInMemory = pathInMemory;
        SubDirectories = subresoucres.ToList();
    }

    public Resource(string name, List<Resource> subDirectories)
    {
        Name = name;
        PathInMemory = null;
        SubDirectories = subDirectories;
    }

    public string Get(string fileName)
    {
        if (PathInMemory != null)
        {
            try
            {
                // Assuming the content is in-memory (e.g., a byte array)
                return File.ReadAllText(Path.Combine(PathInMemory, fileName));
                
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to read content for resource '{Name}/{fileName}': {ex.Message}");
                return null;
            }
        }
        else
        {
            Logger.LogError($"Resource '{Name}' is a directory, not a file.");
            return null;
        }
    }

    public string GetPath(string fileName)
    {
        if (PathInMemory != null)
        {
            try
            {
                // Assuming the content is in-memory (e.g., a byte array)
                return Path.Combine(PathInMemory, fileName);

            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to read content for resource '{Name}/{fileName}': {ex.Message}");
                return null;
            }
        }
        else
        {
            Logger.LogError($"Resource '{Name}' is a directory, not a file.");
            return null;
        }
    }

    public Resource GetSubResource(string subDirectoryName)
    {
        foreach (var subDirectory in SubDirectories)
        {
            if (subDirectory.Name == subDirectoryName)
            {
                return subDirectory;
            }
        }

        // Sub-directory not found
        Logger.LogError($"Sub-resource '{subDirectoryName}' not found in directory '{Name}'.");
        return null;
    }


    public static Resource CreateFromDirectory(string directoryPath)
    {
        try
        {
            string directoryName = Path.GetFileName(directoryPath);
            List<Resource> subResources = new List<Resource>();

            List<string> files = new List<string>();

            // Add files as resources in the current directory
            foreach (var file in Directory.GetFiles(directoryPath))
            {
                files.Add(file);
            }

            // Recursively add subdirectories as resources
            foreach (var subDirectory in Directory.GetDirectories(directoryPath))
            {
                subResources.Add(CreateFromDirectory(subDirectory));
            }

            return new Resource(directoryName, subResources.ToArray(), directoryPath, files.ToArray());
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to create resource from directory path '{directoryPath}': {ex.Message}");
            return null;
        }
    }


    public override string ToString()
    {
        return ToString(0);
    }

    private string ToString(int indentationLevel)
    {
        StringBuilder sb = new StringBuilder();
        string indentation = new string(' ', indentationLevel * 2);

        sb.AppendLine($"{indentation}Name: {Name}");
        sb.AppendLine($"{indentation}PathInMemory: {PathInMemory}");

        if (Resources != null && Resources.Count > 0)
        {
            sb.AppendLine($"{indentation}Resources:");
            foreach (var resource in Resources)
            {
                sb.AppendLine($"{indentation}  - {resource}");
            }
        }

        if (SubDirectories != null && SubDirectories.Count > 0)
        {
            sb.AppendLine($"{indentation}SubDirectories:");
            foreach (var subDirectory in SubDirectories)
            {
                sb.AppendLine(subDirectory.ToString(indentationLevel + 1));
            }
        }

        return sb.ToString();
    }


}

public static class ResourcesManager
{
    private static Dictionary<string, Resource> resources = new Dictionary<string, Resource>();

    public static void DumpResource(string name)
    {
        if (resources.ContainsKey(name))
        {
            Console.WriteLine(resources[name].ToString());
        }
        else
        {
            Logger.Log("Couldnt find the resource with name:" + name);
        }
    }

    public static bool RegisterResource(string name, Resource resource)
    {
        try
        {
            if (!resources.ContainsKey(name))
            {
                resources[name] = resource;
                Logger.Log($"Resource '{name}' registered successfully.");
                return true;
            }
            else
            {
                Logger.LogError($"Resource with the name '{name}' already exists. Registration failed.");
                return false;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to register resource '{name}': {ex.Message}");
            return false;
        }
    }

    public static bool RegisterResourceFromPath(string name, string path)
    {
        try
        {
            Resource resource = Resource.CreateFromDirectory(path);
            if (resource != null)
            {
                return RegisterResource(name, resource);
            }
            else
            {
                return false;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to register resource '{name}' from path '{path}': {ex.Message}");
            return false;
        }
    }

    public static Resource GetResource(string name)
    {
        if (resources.ContainsKey(name))
        {
            return resources[name];
        }
        else
        {
            Logger.LogError($"Resource '{name}' not found.");
            return null;
        }
    }
}

public class MonkeyServer
{
    private HttpListener listener = null;
    private MonkeyEndpoints endPoints = null;
    private string url = "http://localhost:8000/";
    private bool verbose = false;
    private bool runServer = false;
    private bool isDebug = false;

    public MonkeyServer(string url, bool debug, bool verbose)
    {
        this.url = url;
        listener = new HttpListener();
        endPoints = new MonkeyEndpoints();
        RefreshEndpoints();
        StartServer(debug, verbose);
    }

    public void RefreshEndpoints()
    {
        var directorys = Directory.GetDirectories("./endpoints");

        foreach(var directory in directorys)
        {
            if (Directory.Exists(directory))
            {
                Logger.Log("Loading " + directory);
                CompileAndLoadPlugin(directory);
            }
        }
    }

    public MonkeyEndpoints GetEndpoints()
    {
        return endPoints;
    }

    public HttpListener GetHTTPListener()
    {
        return listener;
    }

    private string errorTemplate = @"<!DOCTYPE html>
                                            <html>
                                            <head>
                                                <title>Error</title>
                                                <link rel=""stylesheet"" href=""https://stackpath.bootstrapcdn.com/bootstrap/4.5.2/css/bootstrap.min.css"">
                                                <style>
                                                    body {
                                                        background-color: #252525;
                                                        color: #fff;
                                                    }

                                                    .card {
                                                        background-color: #0a0a0a;
                                                        margin: 100px auto;
                                                        /* Update the max-width and width to make the card wider */
                                                        max-width: 200%;
                                                        width: 100%;
                                                        border-radius: 5px;
                                                        box-shadow: 0 0 10px rgba(255, 0, 0, 0.397);
                                                    }

                                                    .card-header {
                                                        background-color: #1d1d1d;
                                                        color: #fff;
                                                        border-radius: 5px 5px 0 0;
                                                    }

                                                    .card-footer {
                                                        background-color: #1d1d1d;
                                                        color: #fff;
                                                        border-radius: 0 0 5px 5px;
                                                    }

                                                    .error-code {
                                                        font-size: 24px;
                                                        margin-bottom: 10px;
                                                    }

                                                    .error-message {
                                                        font-size: 18px;
                                                    }
                                                </style>
                                            </head>
                                            <body>
                                                <div class=""container"">
                                                    <div class=""card"">
                                                        <div class=""card-header"">
                                                            <h4>Monkey Web Server - Error</h4>
                                                        </div>
                                                        <div class=""card-body"">
                                                            <p class=""error-message"">{{error}}</p>
                                                        </div>
                                                        <div class=""card-footer text-muted"">
                                                            <small>@ Endpoint: {{endpoint}}</small>
                                                        </div>
                                                    </div>
                                                </div>
                                            </body>
                                            </html>
                                            ";

    public void SetErrorTemplate(string templateText)
    {
        errorTemplate = templateText;
    }

    public void SetErrorTemplateFromFile(string filePath)
    {
        if (File.Exists(filePath))
        {
            SetErrorTemplate(File.ReadAllText(filePath));
        }
        else
        {
            Logger.Log("Error Template file doesnt exist: " + filePath);
        }
    }

    public void ClearLoadedPluginData()
    {
        if (Directory.Exists("./plugindata"))
        {
            Directory.Delete("./plugindata", true);
        }
        Directory.CreateDirectory("./plugindata");
    }


    public async void CompileAndLoadPlugin(string path)
    {
        if (!Directory.Exists(path))
        {
            Logger.Log("The Plugin: " + path + ", is not a folder!");
            return;
        }

        var csFiles = Directory.GetFiles(path, "*.cs");

        try
        {
            var scriptOptions = ScriptOptions.Default
                .WithReferences(AppDomain.CurrentDomain.GetAssemblies()) // Include necessary assemblies
                .WithReferences(AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic).Select(a => a.Location)); // Include references to assembly locations
            ResourcesManager.RegisterResourceFromPath(Path.GetFileName(path), Path.Combine(path, "data"));
            foreach (var csFile in csFiles)
            {
                try
                {
                    var scriptCode = File.ReadAllText(csFile);
                    var script = CSharpScript.Create(scriptCode, ScriptOptions.Default.WithReferences(Assembly.GetExecutingAssembly()));
                    script.Compile();
                    // run and you get Type object for your fresh type
                    var testType = (Type)script.RunAsync().Result.ReturnValue;
                    // create and cast to interface
                    var runnable = (MonkeyEndpoint)Activator.CreateInstance(testType);
                    // use
                    endPoints.AddEndpoints(runnable);
                    Logger.Log("Loaded a endpoint");
                


   

        
                }
                catch (Exception ex)
                {
                    Logger.Log("Error loading plugin from " + csFile + ": " + ex);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"Failed to load endpoint: {path} {Environment.NewLine} {ex}");
        }
    }

    public void LoadPlugin(string path)
    {
        if (!Directory.Exists(path))
        {
            Logger.Log("The Plugin: " + path + ", is not a folder!");
            return;
        }

        ResourcesManager.RegisterResourceFromPath(Path.GetFileName(path), path);

        //ExtractPluginData(path, Path.GetDirectoryName(path));

        var cfgFiles = Directory.GetFiles(path, "*.json");
        var dllFiles = Directory.GetFiles(path, "*.dll");

        var cfg = JsonConvert.DeserializeObject<PluginConfig>(File.ReadAllText(cfgFiles[0]));

        foreach (var dllFile in dllFiles)
        {
            try
            {
                // Load the assembly from the DLL file
                Assembly assembly = Assembly.LoadFrom(dllFile);

                // Find the MonkeyPlugin type in the assembly
                Type monkeyPluginType = assembly.GetType($"{cfg.NameSpace}.{cfg.Class}");

                if (monkeyPluginType != null)
                {
                    // Get the static Main method
                    MethodInfo mainMethod = monkeyPluginType.GetMethod(cfg.Function, BindingFlags.Public | BindingFlags.Static);

                    if (mainMethod != null)
                    {
                        // Invoke the Main method (pass null for static methods)
                        mainMethod.Invoke(null, new object[] { this });
                        Logger.Log("A plugin has been loaded!");
                    }
                    else
                    {
                        Logger.Log("Main method not found in MonkeyPlugin in: " + dllFile);
                    }
                }
                else
                {
                    Logger.Log("MonkeyPlugin type not found in: " + dllFile);
                }
            }
            catch (Exception ex)
            {
                Logger.Log("Error loading plugin from " + dllFile + ": " + ex);
            }
        }
    }
    private static void ExtractPluginData(string path, string pluginname)
    {
        var pluginDataPath = Path.Combine(path, "plugindata");

        if (Directory.Exists(pluginDataPath))
        {
            var destinationPath = Path.Combine(".", pluginname, "plugindata");

            // Create the destination directory if it doesn't exist
            if (!Directory.Exists(destinationPath))
            {
                Directory.CreateDirectory(destinationPath);
            }

            // Copy all files and subdirectories recursively
            CopyDirectory(pluginDataPath, destinationPath);

            // Log the extraction
            Logger.Log($"Plugin data extracted from '{pluginDataPath}' to '{destinationPath}'.");
        }
        else
        {
            Logger.LogError("The 'plugindata' folder does not exist in the specified path.");
        }
    }

    private static void CopyDirectory(string sourceDir, string destDir)
    {
        // Copy all files
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var destFile = Path.Combine(destDir, Path.GetFileName(file));
            File.Copy(file, destFile, true);
        }

        // Recursively copy subdirectories
        foreach (var subdirectory in Directory.GetDirectories(sourceDir))
        {
            var destSubdirectory = Path.Combine(destDir, Path.GetFileName(subdirectory));
            CopyDirectory(subdirectory, destSubdirectory);
        }
    }


    public void StartServer(bool Debug, bool Verbose)
    {
        isDebug = Debug;
        verbose = Verbose;
        if (runServer == false)
        {
            
            runServer = true;
            listener.Start();
            AddPrefix(url);
            Logger.Log($"Listening for requests on§2 {url}", verbose);
            Task.Run(() => HandleIncomingConnections());
        }
    }

    public void AddPrefix(string prefix)
    {
        listener.Prefixes.Add(prefix);
    }

    public void StopServer()
    {
        runServer = false;
    }

    private async void HandleFaviconRequest(HttpListenerContext context)
    {
        if (File.Exists("./favicon.ico"))
        {
            Logger.Log($"Serving favicon..", verbose);
            // Load the icon file into a byte array
            byte[] iconData = File.ReadAllBytes("./favicon.ico");

            // Set the appropriate response headers
            context.Response.ContentType = "image/x-icon";
            context.Response.ContentLength64 = iconData.Length;

            // Write the icon data to the response stream
            await context.Response.OutputStream.WriteAsync(iconData, 0, iconData.Length);
        }
        else
        {
            Logger.Log("Favicon not found, place it into the project folder and call it: favicon.ico", verbose);
        }
        context.Response.Close();
    }


    private void HandleEndpointNotFound(HttpListenerContext context, string urlPath)
    {
        if (isDebug)
        {
            Logger.LogIncoming($"Couldn't find endpoint: §4{urlPath}, §8did you register it using §6MonkeyEndpoints§8.§2AddEndpoint§12()§8?", verbose);


            MonkeyResponse resp = MonkeyResponse.RenderTemplateText(new MonkeyRequest(context), errorTemplate, new { endpoint = urlPath, error = "Your team of monkeys couldn't find the endpoint you just tried to navigate to. Did you register it using: MonkeyEndpoints.AddEndpoint()?" }); ;
            resp.SendToClient();
        }
        else
        {
            MonkeyResponse monkeyresponseerr2 = MonkeyResponse.Text(new MonkeyRequest(context), "Error 500", 500);
            monkeyresponseerr2.SendToClient();
        }
    }

    private async Task ProcessRequest(HttpListenerContext context)
    {
        string urlPath = context.Request.Url.AbsolutePath;
        Logger.Log(urlPath);

        if (urlPath == "/favicon.ico")
        {
            // Handle favicon request
            HandleFaviconRequest(context);
        }
        else
        {
            // Handle other requests
            HandleOtherRequest(context, urlPath);
        }
    }

    private void HandleOtherRequest(HttpListenerContext context, string urlPath)
    {
        MonkeyEndpoint endpoint = Find(urlPath);

        if (endpoint != null)
        {
            try
            {
                // Execute the endpoint and send the response
                Logger.LogIncoming($"Handling endpoint: §2{urlPath}", verbose);
                MonkeyResponse monkeyresponse = endpoint.Execute(new MonkeyRequest(context));
                monkeyresponse.SendToClient();
            }
            catch (Exception ex)
            {
                Logger.LogIncoming($"Error encountered while handling the endpoint: §4{urlPath}", verbose);
                HandleErrorResponse(context, ex, urlPath);
            }
        }
        else
        {
            HandleEndpointNotFound(context, urlPath);
        }
        context.Response.Close();
    }

    private void HandleErrorResponse(HttpListenerContext context, Exception ex, string urlPath)
    {
        Logger.LogIncoming($"Error encountered while handling the endpoint: §4{urlPath}", verbose);
        if (isDebug)
        {
            Logger.Log(ex.Message, verbose);
            MonkeyResponse monkeyresponseerr = MonkeyResponse.RenderTemplateText(new MonkeyRequest(context), errorTemplate, new { endpoint = urlPath, error = ex.Message });

            monkeyresponseerr.SendToClient();
        }
        else
        {
            MonkeyResponse monkeyresponseerr2 = MonkeyResponse.Text(new MonkeyRequest(context), "Error 500", 500);
            monkeyresponseerr2.SendToClient();
        }
    }


    public async Task HandleIncomingConnections()
    {
        while (runServer)
        {
            HttpListenerContext context;

            try
            {
                context = await listener.GetContextAsync();

                // Use Task.Run with TaskCreationOptions.LongRunning to run the task on a dedicated thread
                Task.Run(() => ProcessRequest(context));
            }
            catch (HttpListenerException ex)
            {
                Logger.LogError($"HttpListener Exception: {ex}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Unhandled Exception: {ex}");
            }
        }
    }




    public MonkeyEndpoint Find(string urlPath)
    {
        MonkeyEndpoint existingEndpoint = endPoints.EndPoints.Find(x => x.Endpoint == urlPath);
        if (existingEndpoint != null)
        {
            // Create a new instance of MonkeyEndpoint using MemberwiseClone method
            var newEndpoint = (MonkeyEndpoint)Activator.CreateInstance(existingEndpoint.GetType());
            //MonkeyEndpoint newEndpoint = (MonkeyEndpoint)existingEndpoint.DuplicateRaw();


            return newEndpoint;
        }
        else
        {
            return null; // No matching endpoint found
        }
    }

}

public abstract class MonkeyEndpoint
{
    public abstract string Endpoint { get; }



    public abstract MonkeyResponse Execute(MonkeyRequest req);

    public MonkeyEndpoint DuplicateRaw()
    {
        return (MonkeyEndpoint)this.MemberwiseClone();
    }
}

public class MonkeyEndpoints
{
    public List<MonkeyEndpoint> EndPoints = new List<MonkeyEndpoint>();

    public void AddEndpoint(MonkeyEndpoint endpoint)
    {
        EndPoints.Add(endpoint);
    }

    public void AddEndpoints(params MonkeyEndpoint[] endpoint)
    {
        EndPoints.AddRange(endpoint);
    }
}


public class MonkeyRequest
{
    public JObject JsonReq { get; set; }
    public NameValueCollection Get { get; set; }
    public NameValueCollection Post { get; set; }
    public Dictionary<string, HttpFile> Files { get; set; }

    private HttpListenerContext ctx = null;

    public MonkeyRequest(HttpListenerContext ctx)
    {
        this.ctx = ctx;
        Get = ctx.Request.QueryString;
        Post = new NameValueCollection();
        Files = new Dictionary<string, HttpFile>();

        if (ctx.Request.ContentType == "application/json")
        {
            JsonReq = JsonConvert.DeserializeObject<JObject>(GetContent(ctx.Request.InputStream, ctx.Request.ContentEncoding));
        }
        else if (ctx.Request.HttpMethod == "POST" && ctx.Request.ContentType?.StartsWith("multipart/form-data") == true)
        {
            PopulatePostMultiPart(ctx, ctx.Request.InputStream, ctx.Request.ContentEncoding);
        }
        else
        {
            PopulatePost(ctx.Request.InputStream, ctx.Request.ContentEncoding);
        }
    }

    private string GetContent(Stream inputStream, Encoding encoding)
    {
        using (var reader = new StreamReader(inputStream, encoding))
        {
            return reader.ReadToEnd();
        }
    }

    private void PopulatePostMultiPart(HttpListenerContext _ctx, Stream inputStream, Encoding encoding)
    {
        var post_string = GetContent(inputStream, encoding);

        var boundary_index = _ctx.Request.ContentType.IndexOf("boundary=") + 9;
        var boundary = _ctx.Request.ContentType.Substring(boundary_index, _ctx.Request.ContentType.Length - boundary_index);

        var upper_bound = post_string.Length - 4;

        if (post_string.Substring(2, boundary.Length) != boundary)
            throw (new InvalidDataException());

        var raw_post_strings = new List<string>();
        var current_string = new StringBuilder();

        for (var x = 4 + boundary.Length; x < upper_bound; ++x)
        {
            if (post_string.Substring(x, boundary.Length) == boundary)
            {
                x += boundary.Length + 1;
                raw_post_strings.Add(current_string.ToString().Remove(current_string.Length - 3, 3));
                current_string.Clear();
                continue;
            }

            current_string.Append(post_string[x]);

            var post_variable_string = current_string.ToString();

            var end_of_header = post_variable_string.IndexOf("\r\n\r\n");

            if (end_of_header == -1) throw (new InvalidDataException());

            var filename_index = post_variable_string.IndexOf("filename=\"", 0, end_of_header);
            var filename_starts = filename_index + 10;
            var content_type_starts = post_variable_string.IndexOf("Content-Type: ", 0, end_of_header) + 14;
            var name_starts = post_variable_string.IndexOf("name=\"") + 6;
            var data_starts = end_of_header + 4;

            if (filename_index == -1) continue;

            var filename = post_variable_string.Substring(filename_starts, post_variable_string.IndexOf("\"", filename_starts) - filename_starts);
            var content_type = post_variable_string.Substring(content_type_starts, post_variable_string.IndexOf("\r\n", content_type_starts) - content_type_starts);
            var file_data = post_variable_string.Substring(data_starts, post_variable_string.Length - data_starts);
            var name = post_variable_string.Substring(name_starts, post_variable_string.IndexOf("\"", name_starts) - name_starts);
            Files.Add(name, new HttpFile() { FileName = filename, ContentType = content_type, FileData = file_data });
            continue;

        }
    }

    public HttpListenerContext GetContext()
    {
        return ctx;
    }

    private void PopulatePost(Stream inputStream, Encoding encoding)
    {
        var postString = GetContent(inputStream, encoding);
        Post = HttpUtility.ParseQueryString(postString);
    }
}

public class HttpFile
{
    public string FileName { get; set; }
    public string FileData { get; set; }
    public string ContentType { get; set; }
}

public class MonkeyResponse : IDisposable
{
    public MonkeyRequest req;

    public byte[] content;

    public MonkeyResponse(MonkeyRequest req)
    {
        this.req = req;
    }

    public async void SendToClient()
    {
        await req.GetContext().Response.OutputStream.WriteAsync(content, 0, content.Length);
        this.Dispose();
    }

    public static MonkeyResponse Text(MonkeyRequest req, string text, int statusCode)
    {
        byte[] data = Encoding.UTF8.GetBytes(text);

        //next this!
        req.GetContext().Response.StatusCode = statusCode;
        req.GetContext().Response.ContentType = "text/plain";
        req.GetContext().Response.ContentEncoding = Encoding.UTF8;
        req.GetContext().Response.ContentLength64 = data.LongLength;


        return new MonkeyResponse(req) { content = data };
    }

    public static MonkeyResponse Text(MonkeyRequest req, string text)
    {
        byte[] data = Encoding.UTF8.GetBytes(text);

        req.GetContext().Response.StatusCode = 200;
        req.GetContext().Response.ContentType = "application/json";
        req.GetContext().Response.ContentEncoding = Encoding.UTF8;
        req.GetContext().Response.ContentLength64 = data.LongLength;

        return new MonkeyResponse(req) { content = data };
    }

    public static MonkeyResponse PHP(MonkeyRequest req, string phpFile)
    {


        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = "./bin/php-cgi.exe",
            Arguments = $"-f {phpFile}",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using (Process process = new Process { StartInfo = psi })
        {
            process.Start();
            string output = process.StandardOutput.ReadToEnd();

            byte[] buffer = Encoding.UTF8.GetBytes(output);

            req.GetContext().Response.StatusCode = 200;
            req.GetContext().Response.ContentLength64 = buffer.Length;
            req.GetContext().Response.ContentType = "text/html";
            req.GetContext().Response.ContentEncoding = Encoding.UTF8;

            return new MonkeyResponse(req) { content = buffer };
        }
    }




    public static MonkeyResponse Json(MonkeyRequest req, object jsonData)
    {
        string jsonString = JsonConvert.SerializeObject(jsonData);
        byte[] data = Encoding.UTF8.GetBytes(jsonString);

        req.GetContext().Response.StatusCode = 200;
        req.GetContext().Response.ContentType = "application/json";
        req.GetContext().Response.ContentEncoding = Encoding.UTF8;
        req.GetContext().Response.ContentLength64 = data.LongLength;

        return new MonkeyResponse(req) { content = data };
    }


    public static MonkeyResponse Json(MonkeyRequest req, object jsonData, int statusCode)
    {
        string jsonString = JsonConvert.SerializeObject(jsonData);
        byte[] data = Encoding.UTF8.GetBytes(jsonString);

        req.GetContext().Response.StatusCode = statusCode;
        req.GetContext().Response.ContentType = "application/json";
        req.GetContext().Response.ContentEncoding = Encoding.UTF8;
        req.GetContext().Response.ContentLength64 = data.LongLength;

        return new MonkeyResponse(req) { content = data };
    }



    public static MonkeyResponse RenderTemplateText(MonkeyRequest req, string templateText, object view)
    {
        //first this
        string result = GetRenderedTemplate(templateText, view);

        var mreq = BuildContext(req, 200, result);

        return new MonkeyResponse(mreq) { content = Encoding.UTF8.GetBytes(result) };

    }

    public static MonkeyResponse RenderTemplateText(MonkeyRequest req, string templateText, object view, int statusCode)
    {
        string result = GetRenderedTemplate(templateText, view);
        byte[] data = Encoding.UTF8.GetBytes(result);
        var mreq = BuildContext(req, statusCode, result);

        return new MonkeyResponse(mreq) { content = data };
    }


    public static MonkeyResponse TemplatePHP(MonkeyRequest req, string phpFile, object view)
    {
        var tempFile = Path.GetTempFileName();

        File.WriteAllText(tempFile, GetRenderedTemplate(File.ReadAllText(phpFile), view));


        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = "./bin/php-cgi.exe",
            Arguments = $"-f {tempFile}",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using (Process process = new Process { StartInfo = psi })
        {
            process.Start();
            string output = process.StandardOutput.ReadToEnd();

            byte[] buffer = Encoding.UTF8.GetBytes(output);

            req.GetContext().Response.StatusCode = 200;
            req.GetContext().Response.ContentLength64 = buffer.Length;
            req.GetContext().Response.ContentType = "text/html";
            req.GetContext().Response.ContentEncoding = Encoding.UTF8;

            return new MonkeyResponse(req) { content = buffer };
        }
    }

    public static MonkeyResponse RenderTemplate(MonkeyRequest req, string templatePath, object view)
    {
        string templateSource = File.ReadAllText(templatePath);
        string result = GetRenderedTemplate(templateSource, view);
        byte[] data = Encoding.UTF8.GetBytes(result);
        var mreq = BuildContext(req, 200, result);

        return new MonkeyResponse(mreq) { content = data };
    }

    public static MonkeyResponse RenderTemplate(MonkeyRequest req, string templatePath, object view, int statusCode)
    {
        string templateSource = File.ReadAllText(templatePath);
        string result = GetRenderedTemplate(templateSource, view);
        byte[] data = Encoding.UTF8.GetBytes(result);
        var mreq = BuildContext(req, statusCode, result);

        return new MonkeyResponse(mreq) { content = data };
    }

    private static string GetRenderedTemplate(string templateText, object view)
    {
        Template template = Template.Parse(templateText);
        return template.Render(view);
    }

    private static MonkeyRequest BuildContext(MonkeyRequest req, int statusCode, string data)
    {
        byte[] databytes = Encoding.UTF8.GetBytes(data);

        req.GetContext().Response.StatusCode = statusCode;
        req.GetContext().Response.ContentType = "text/html";
        req.GetContext().Response.ContentEncoding = Encoding.UTF8;
        req.GetContext().Response.ContentLength64 = databytes.LongLength;
        return req;
    }

    public void Dispose()
    {
        req = null;
        content = null;

    }
}
#endregion
