using MonkeyWebServer;
using Scriban;
using System;
using System.Dynamic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

public class Prog
{
    public static void Main(string[] args)
    {
        MonkeyEndpoints.AddEndpoint(new ExampleEndpoint());

        MonkeyServer.StartServer();
        MonkeyServer.AddPrefix("http://localhost:8000/");
        Task.Run(MonkeyServer.HandleIncomingConnections);

        Console.ReadKey();

        MonkeyServer.StopServer();
    }
}

public class ExampleEndpoint : MonkeyEndpoint
{
    public override string Endpoint { get => "/test"; }

    public override MonkeyResponse Execute(HttpListenerContext ctx)
    {
        var ProductList = new List<dynamic>
        {
            new { name = "Product 1", price = 10.99, description = "Lorem ipsum dolor sit amet" },
            new { name = "Product 2", price = 19.99, description = "Consectetur adipiscing elit" },
            new { name = "Product 3", price = 5.99, description = "Sed do eiusmod tempor" },
            new { name = "Product 4", price = 5.2449, description = "Sedo do edsdiusmod tempor" },
        };

        return MonkeyResponse.RenderTemplate(ctx.Response, "./templates/test.html", new { Products = ProductList });
    }
}






#region MonkeyServer
public abstract class MonkeyEndpoint
{
    public abstract string Endpoint { get; }

    public abstract MonkeyResponse Execute(HttpListenerContext ctx);
}

public static class MonkeyEndpoints
{
    public static List<MonkeyEndpoint> EndPoints = new List<MonkeyEndpoint>();

    public static void AddEndpoint(MonkeyEndpoint endpoint)
    {
        EndPoints.Add(endpoint);
    }
}

public static class MonkeyServer
{
    public static HttpListener listener;
    public static string url = "http://localhost:8000/";
    private static bool runServer = false;

    public static void StartServer()
    {
        if(runServer == false)
        {
            listener = new HttpListener();
            listener.Start();
            Console.WriteLine($"Listening for requests on {url}");
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
            // Wait for a request to come in
            HttpListenerContext context = await listener.GetContextAsync();

            // Get the URL path from the request
            string urlPath = context.Request.Url.AbsolutePath;

            // Find the matching endpoint based on the URL path
            MonkeyEndpoint endpoint = GetEndpointByUrlPath(urlPath);

            if (endpoint != null)
            {
                // Execute the endpoint and send the response
                Logger.LogIncoming($"Handling endpoint: §2{urlPath}");
                MonkeyResponse monkeyresponse = endpoint.Execute(context);
                await monkeyresponse.response.OutputStream.WriteAsync(monkeyresponse.responseData, 0, monkeyresponse.responseData.Length);
                monkeyresponse.response.Close();
            }
            else
            {
                Logger.LogIncoming($"Coudlnt find endpoint: §4{urlPath}, §8did you register it using §6MonkeyEndpoints§8.§2AddEndpoint§12()§8?");
                // No matching endpoint found, send a 404 response
                context.Response.StatusCode = 404;
                context.Response.Close();
            }
        }
    }

    private static MonkeyEndpoint GetEndpointByUrlPath(string urlPath)
    {
        return MonkeyEndpoints.EndPoints.Find(x => x.Endpoint == urlPath);
    }
}

public class MonkeyResponse
{
    public HttpListenerResponse response;
    public dynamic responseData { get; set; }
       
    public MonkeyResponse(HttpListenerResponse response, dynamic responseData)
    {
        this.response = response;
        this.responseData = responseData;
    }

    public static MonkeyResponse RenderTemplate(HttpListenerResponse response, string templatePath, object templateData)
    {
        string templateSource = File.ReadAllText(templatePath);
        Template template = Template.Parse(templateSource);

        string result = template.Render(templateData);
        byte[] data = Encoding.UTF8.GetBytes(result);

        response.StatusCode = 200;
        response.ContentType = "text/html";
        response.ContentEncoding = Encoding.UTF8;
        response.ContentLength64 = data.LongLength;

        return new MonkeyResponse(response, data);
    }


}
#endregion
