﻿using MonkeyWebServer;
using Newtonsoft.Json;
using Scriban;
using System.Dynamic;
using System.Net;
using System.Text;

public class Prog
{
    public static void Main(string[] args)
    {
        MonkeyEndpoints.AddEndpoint(new ExampleEndpoint());
        MonkeyEndpoints.AddEndpoint(new ExampleJsonEndpoint());

        MonkeyServer.StartServer(true);
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

public class ExampleJsonEndpoint : MonkeyEndpoint
{
    public override string Endpoint { get => "/testjson"; }

    public override MonkeyResponse Execute(HttpListenerContext ctx)
    {
        var ProductList = new List<dynamic>
        {
            new { name = "Product 1", price = 10.99, description = "Lorem ipsum dolor sit amet" },
            new { name = "Product 2", price = 19.99, description = "Consectetur adipiscing elit" },
            new { name = "Product 3", price = 5.99, description = "Sed do eiusmod tempor" },
            new { name = "Product 4", price = 5.2449, description = "Sedo do edsdiusmod tempor" },
        };

        return MonkeyResponse.Json(ctx.Response, ProductList);
    }
}






#region MonkeyServer
public static class MonkeyServer
{
    public static HttpListener listener;
    public static string url = "http://localhost:8000/";
    private static bool runServer = false;
    private static bool isDebug = false;

    public static void StartServer(bool dbg)
    {
        isDebug = dbg;
        if (runServer == false)
        {
            listener = new HttpListener();
            listener.Start();
            Console.WriteLine($"Listening for requests on {url}");
            runServer = true;
        }
    }

    public static void StartServer()
    {
        if (runServer == false)
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
                try
                {
                    // Execute the endpoint and send the response
                    Logger.LogIncoming($"Handling endpoint: §2{urlPath}");
                    MonkeyResponse monkeyresponse = endpoint.Execute(context);
                    await monkeyresponse.response.OutputStream.WriteAsync(monkeyresponse.responseData, 0, monkeyresponse.responseData.Length);
                    monkeyresponse.response.Close();
                }
                catch(Exception ex)
                {
                    Logger.LogIncoming($"Error encountered while handling the endpoint: §4{urlPath}");
                    if (isDebug)
                    {
                        dynamic error = new ExpandoObject();
                        error.message = ex.Message;
                        error.endpoint = urlPath;
                        MonkeyResponse monkeyresponseerr = MonkeyResponse.RenderTemplate(context.Response, "./internal/monkey-error.html", new { Error = error });
                        await monkeyresponseerr.response.OutputStream.WriteAsync(monkeyresponseerr.responseData, 0, monkeyresponseerr.responseData.Length);
                        monkeyresponseerr.response.Close();

                    }
                    else
                    {
                        MonkeyResponse monkeyresponseerr2 = MonkeyResponse.Text(context.Response, "Error 500", 500);
                        await monkeyresponseerr2.response.OutputStream.WriteAsync(monkeyresponseerr2.responseData, 0, monkeyresponseerr2.responseData.Length);
                        monkeyresponseerr2.response.Close();
                    }


                }

            }
            else
            {
                if (isDebug)
                {
                    Logger.LogIncoming($"Coudlnt find endpoint: §4{urlPath}, §8did you register it using §6MonkeyEndpoints§8.§2AddEndpoint§12()§8?");
                    dynamic error = new ExpandoObject();
                    error.message = "Your team of monkeys couldnt find the endpoint you just tried to navigate to, Did you register it using: MonkeyEndpoints.AddEndpoint()?";
                    error.endpoint = urlPath;
                    MonkeyResponse resp = MonkeyResponse.RenderTemplate(context.Response, "./internal/monkey-error.html", new { Error = error });
                    await resp.response.OutputStream.WriteAsync(resp.responseData, 0, resp.responseData.Length);
                    context.Response.Close();
                }
                else
                {
                    MonkeyResponse monkeyresponseerr2 = MonkeyResponse.Text(context.Response, "Error 500", 500);
                    await monkeyresponseerr2.response.OutputStream.WriteAsync(monkeyresponseerr2.responseData, 0, monkeyresponseerr2.responseData.Length);
                    monkeyresponseerr2.response.Close();
                }

            }
        }
    }

    private static MonkeyEndpoint GetEndpointByUrlPath(string urlPath)
    {
        return MonkeyEndpoints.EndPoints.Find(x => x.Endpoint == urlPath);
    }
}

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



public class MonkeyResponse
{
    public HttpListenerResponse response;
    public dynamic responseData { get; set; }
       
    public MonkeyResponse(HttpListenerResponse response, dynamic responseData)
    {
        this.response = response;
        this.responseData = responseData;
    }

    public static MonkeyResponse Text(HttpListenerResponse response, string text, int statusCode)
    {
        byte[] data = Encoding.UTF8.GetBytes(text);

        response.StatusCode = statusCode;
        response.ContentType = "text/plain";
        response.ContentEncoding = Encoding.UTF8;
        response.ContentLength64 = data.LongLength;

        return new MonkeyResponse(response, data);
    }

    public static MonkeyResponse Text(HttpListenerResponse response, string text)
    {
        byte[] data = Encoding.UTF8.GetBytes(text);

        response.StatusCode = 200;
        response.ContentType = "application/json";
        response.ContentEncoding = Encoding.UTF8;
        response.ContentLength64 = data.LongLength;

        return new MonkeyResponse(response, data);
    }

    public static MonkeyResponse Json(HttpListenerResponse response, object jsonData)
    {
        string jsonString = JsonConvert.SerializeObject(jsonData);
        byte[] data = Encoding.UTF8.GetBytes(jsonString);

        response.StatusCode = 200;
        response.ContentType = "application/json";
        response.ContentEncoding = Encoding.UTF8;
        response.ContentLength64 = data.LongLength;

        return new MonkeyResponse(response, data);
    }


    public static MonkeyResponse Json(HttpListenerResponse response, object jsonData, int statusCode)
    {
        string jsonString = JsonConvert.SerializeObject(jsonData);
        byte[] data = Encoding.UTF8.GetBytes(jsonString);

        response.StatusCode = statusCode;
        response.ContentType = "application/json";
        response.ContentEncoding = Encoding.UTF8;
        response.ContentLength64 = data.LongLength;

        return new MonkeyResponse(response, data);
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

    public static MonkeyResponse RenderTemplate(HttpListenerResponse response, string templatePath, object templateData, int statusCode)
    {
        string templateSource = File.ReadAllText(templatePath);
        Template template = Template.Parse(templateSource);

        string result = template.Render(templateData);
        byte[] data = Encoding.UTF8.GetBytes(result);

        response.StatusCode = statusCode;
        response.ContentType = "text/html";
        response.ContentEncoding = Encoding.UTF8;
        response.ContentLength64 = data.LongLength;

        return new MonkeyResponse(response, data);
    }


}
#endregion
