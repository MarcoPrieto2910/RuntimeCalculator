var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/probe", async (HttpContext context) =>
{
    context.Response.ContentType = "text/plain";

    while (true)
    {
        using var reader = new StreamReader("stream-test2.txt");

        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            await context.Response.WriteAsync(line);
            await context.Response.WriteAsync("\n");
            await context.Response.Body.FlushAsync();
        
            await Task.Delay(10);
        }
    
        await Task.Delay(10000);
    }
});

app.Run("http://localhost:5000");