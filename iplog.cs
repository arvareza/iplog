using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using System.Net.Http;
using System.Threading.Tasks;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

const string logFilePath = "log.json";
const string ipinfoBaseUrl = "https://ipinfo.io/";

HttpClient httpClient = new HttpClient();

app.MapGet("/", async (HttpContext context) =>
{
    string ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    string userAgent = context.Request.Headers["User-Agent"];

    // If behind a reverse proxy like Nginx, use this instead:
    // ip = context.Request.Headers["X-Forwarded-For"].FirstOrDefault() ?? ip;

    string url = $"{ipinfoBaseUrl}{ip}/json";
    IpInfo? ipData = null;

    try
    {
        var response = await httpClient.GetStringAsync(url);
        ipData = JsonSerializer.Deserialize<IpInfo>(response);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Failed to fetch IP info: {ex.Message}");
    }

    var logEntry = new
    {
        ip,
        timestamp = DateTime.UtcNow.ToString("o"),
        city = ipData?.City,
        region = ipData?.Region,
        country = ipData?.Country,
        loc = ipData?.Loc,
        org = ipData?.Org,
        hostname = ipData?.Hostname,
        userAgent
    };

    List<object> logs = new();

    if (File.Exists(logFilePath))
    {
        try
        {
            var existing = await File.ReadAllTextAsync(logFilePath);
            logs = JsonSerializer.Deserialize<List<object>>(existing) ?? new List<object>();
        }
        catch
        {
            logs = new List<object>();
        }
    }

    logs.Add(logEntry);
    var json = JsonSerializer.Serialize(logs, new JsonSerializerOptions { WriteIndented = true });
    await File.WriteAllTextAsync(logFilePath, json);

    context.Response.ContentType = "application/json";
    await context.Response.WriteAsJsonAsync(new { status = "logged", ip });
});

app.Run("http://0.0.0.0:5000");

record IpInfo(
    [property: JsonPropertyName("ip")] string? Ip,
    [property: JsonPropertyName("hostname")] string? Hostname,
    [property: JsonPropertyName("city")] string? City,
    [property: JsonPropertyName("region")] string? Region,
    [property: JsonPropertyName("country")] string? Country,
    [property: JsonPropertyName("loc")] string? Loc,
    [property: JsonPropertyName("org")] string? Org
);
