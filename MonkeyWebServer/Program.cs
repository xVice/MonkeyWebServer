using MonkeyWebServer;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Scriban;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Text;
using System.Web;

#region MonkeyServer
public static class MonkeyServer
{
    public static HttpListener listener;
    public static string url = "http://localhost:8000/";
    private static bool verbose = false;
    private static bool runServer = false;
    private static bool isDebug = false;

    private static string errorTemplate = @"<!DOCTYPE html>
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

    public static void StartServer(bool Debug)
    {
        isDebug = Debug;
        if (runServer == false)
        {
            listener = new HttpListener();
            listener.Start();
            Logger.Log($"Listening for requests on§2 {url}", verbose);
            runServer = true;
        }
    }

    public static void StartServer(bool Debug, bool Verbose)
    {
        isDebug = Debug;
        verbose = Verbose;
        if (runServer == false)
        {
            listener = new HttpListener();
            listener.Start();
            Logger.Log($"Listening for requests on§2 {url}", verbose);
            runServer = true;
        }
    }

    public static void StartServer()
    {
        if (runServer == false)
        {
            listener = new HttpListener();
            listener.Start();
            Logger.Log($"Listening for requests on§2 {url}", verbose);
            runServer = true;
        }
    }

    public static void AddPrefix(string prefix)
    {
        listener.Prefixes.Add(prefix);
    }

    public static void StopServer()
    {
        runServer = false;
    }

    public static async Task HandleIncomingConnections()
    {
        StartServer();
        while (runServer)
        {
            HttpListenerContext context;
            try
            {
                // Wait for a request to come in
                context = await listener.GetContextAsync();

                // Get the URL path from the request
                string urlPath = context.Request.Url.AbsolutePath;

                if (urlPath == "/favicon.ico")
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

                    // Close the response
                    context.Response.Close();
                }
                else
                {
                    // Find the matching endpoint based on the URL path
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
                    }
                    else
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

                    // Close the response for all cases
                    context.Response.Close();
                }
            }
            catch (HttpListenerException ex)
            {
                // Handle exceptions related to the HttpListener
                // For example, if the listener is closed while waiting for a request
                Logger.LogError($"HttpListener Exception: {ex.Message}");
            }
            catch (Exception ex)
            {
                // Handle other exceptions
                Logger.LogError($"Unhandled Exception: {ex.Message}");
            }
        }
    }

   

    public static MonkeyEndpoint Find(string urlPath)
    {
        MonkeyEndpoint existingEndpoint = MonkeyEndpoints.EndPoints.Find(x => x.Endpoint == urlPath);
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

public static class MonkeyEndpoints
{
    public static List<MonkeyEndpoint> EndPoints = new List<MonkeyEndpoint>();

    public static void AddEndpoint(MonkeyEndpoint endpoint)
    {
        EndPoints.Add(endpoint);
    }
}


public class MonkeyRequest
{
    public JObject jsonReq;

    public HttpListenerContext ctx;
    public HttpNameValueCollection Data;

    public MonkeyRequest(HttpListenerContext ctx) 
    {
        this.ctx = ctx;
        HandleContext();
    }

    public string GetContent()
    {
        var request = ctx.Request;
        string content;
        using (var reader = new StreamReader(request.InputStream,
                                             request.ContentEncoding))
        {
            content = reader.ReadToEnd();
        }
        return content;
    }


    void HandleContext()
    {
        if(ctx.Request.ContentType == "application/json")
        {
            jsonReq = JsonConvert.DeserializeObject<JObject>(GetContent());
        }
        Data = new HttpNameValueCollection(ref ctx);
    }
}


public class HttpNameValueCollection
{
    public class File
    {
        private string _fileName;
        public string FileName { get { return _fileName ?? (_fileName = ""); } set { _fileName = value; } }

        private string _fileData;
        public string FileData { get { return _fileData ?? (_fileName = ""); } set { _fileData = value; } }

        private string _contentType;
        public string ContentType { get { return _contentType ?? (_contentType = ""); } set { _contentType = value; } }
    }

    private NameValueCollection _get;
    private Dictionary<string, File> _files;
    private readonly HttpListenerContext _ctx;

    public NameValueCollection Get { get { return _get ?? (_get = new NameValueCollection()); } set { _get = value; } }
    public NameValueCollection Post { get { return _ctx.Request.QueryString; } }
    public Dictionary<string, File> Files { get { return _files ?? (_files = new Dictionary<string, File>()); } set { _files = value; } }

    private void PopulatePostMultiPart(string post_string)
    {
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
            Files.Add(name, new File() { FileName = filename, ContentType = content_type, FileData = file_data });
            continue;

        }
    }

    private void PopulatePost()
    {
        if (_ctx.Request.HttpMethod != "POST" || _ctx.Request.ContentType == null) return;

        var post_string = new StreamReader(_ctx.Request.InputStream, _ctx.Request.ContentEncoding).ReadToEnd();

        if (_ctx.Request.ContentType.StartsWith("multipart/form-data"))
            PopulatePostMultiPart(post_string);
        else
            Get = HttpUtility.ParseQueryString(post_string);

    }

    public HttpNameValueCollection(ref HttpListenerContext ctx)
    {
        _ctx = ctx;
        PopulatePost();
    }


}

public class MonkeyResponse
{
    public MonkeyRequest req;

    public byte[] content;

    public MonkeyResponse(MonkeyRequest req)
    {
        this.req = req;
    }

    public async void SendToClient()
    {
        await req.ctx.Response.OutputStream.WriteAsync(content, 0, content.Length);
    }

    public static MonkeyResponse Text(MonkeyRequest req, string text, int statusCode)
    {
        byte[] data = Encoding.UTF8.GetBytes(text);

        //next this!
        req.ctx.Response.StatusCode = statusCode;
        req.ctx.Response.ContentType = "text/plain";
        req.ctx.Response.ContentEncoding = Encoding.UTF8;
        req.ctx.Response.ContentLength64 = data.LongLength;


        return new MonkeyResponse(req) { content = data};
    }

    public static MonkeyResponse Text(MonkeyRequest req, string text)
    {
        byte[] data = Encoding.UTF8.GetBytes(text);

        req.ctx.Response.StatusCode = 200;
        req.ctx.Response.ContentType = "application/json";
        req.ctx.Response.ContentEncoding = Encoding.UTF8;
        req.ctx.Response.ContentLength64 = data.LongLength;

        return new MonkeyResponse(req) { content = data };
    }

    public static MonkeyResponse Json(MonkeyRequest req, object jsonData)
    {
        string jsonString = JsonConvert.SerializeObject(jsonData);
        byte[] data = Encoding.UTF8.GetBytes(jsonString);

        req.ctx.Response.StatusCode = 200;
        req.ctx.Response.ContentType = "application/json";
        req.ctx.Response.ContentEncoding = Encoding.UTF8;
        req.ctx.Response.ContentLength64 = data.LongLength;

        return new MonkeyResponse(req) { content = data };
    }


    public static MonkeyResponse Json(MonkeyRequest req, object jsonData, int statusCode)
    {
        string jsonString = JsonConvert.SerializeObject(jsonData);
        byte[] data = Encoding.UTF8.GetBytes(jsonString);

        req.ctx.Response.StatusCode = statusCode;
        req.ctx.Response.ContentType = "application/json";
        req.ctx.Response.ContentEncoding = Encoding.UTF8;
        req.ctx.Response.ContentLength64 = data.LongLength;

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


    public static MonkeyResponse RenderTemplate(MonkeyRequest req, string templatePath, object view)
    {
        string templateSource = File.ReadAllText(templatePath);
        string result = GetRenderedTemplate(templateSource, view);
        byte[] data = Encoding.UTF8.GetBytes(result);
        var mreq = BuildContext(req, 200, result);

        return new MonkeyResponse(mreq) { content = data};
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

        req.ctx.Response.StatusCode = statusCode;
        req.ctx.Response.ContentType = "text/html";
        req.ctx.Response.ContentEncoding = Encoding.UTF8;
        req.ctx.Response.ContentLength64 = databytes.LongLength;
        return req;
    }

}
#endregion
