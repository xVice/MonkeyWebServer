# MonkeyWebServer
A Csharp WebServer powered by Monke.

- Template Rendering using Scriban.
- Json responses using NewtonsoftJson.
- Easy routing.
- cool dev errors.

# Usage
```csharp
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
```
