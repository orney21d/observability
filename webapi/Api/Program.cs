using System.Diagnostics.Metrics;
using System.Diagnostics;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using OpenTelemetry;
using OpenTelemetry.Logs;

// This is required if the collector doesn't expose an https endpoint as .NET by default
// only allow http2 (required for gRPC) to secure endpoints
AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

var builder = WebApplication.CreateBuilder(args);

// Configure metrics
builder.Services.AddOpenTelemetryMetrics(builder =>
{
    builder.AddHttpClientInstrumentation();
    builder.AddAspNetCoreInstrumentation();
    builder.AddMeter("MyApplicationMetrics");
    builder.AddOtlpExporter(options => options.Endpoint = new Uri("http://localhost:4317"));
});

// Configure tracing
builder.Services.AddOpenTelemetryTracing(builder =>
{
    builder.AddHttpClientInstrumentation();
    builder.AddAspNetCoreInstrumentation();
    builder.AddSource("MyApplicationActivitySource");
    builder.AddOtlpExporter(options => options.Endpoint = new Uri("http://localhost:4317"));
});

// Configure logging
builder.Logging.AddOpenTelemetry(builder =>
{
    builder.IncludeFormattedMessage = true;
    builder.IncludeScopes = true;
    builder.ParseStateValues = true;
    builder.AddOtlpExporter(options => options.Endpoint = new Uri("http://localhost:4317"));
});

var app = builder.Build();


// Create a route (GET /) that will make an http call, increment a metric and log a trace
var activitySource = new ActivitySource("MyApplicationActivitySource");
var meter = new Meter("MyApplicationMetrics");
var requestCounter = meter.CreateCounter<int>("compute_requests");
var httpClient = new HttpClient();

// Routes are tracked by AddAspNetCoreInstrumentation
// note: You can add more data to the Activity by using HttpContext.Activity.SetTag("key", "value")
app.MapGet("/", async (ILogger<Program> logger) =>
{
    requestCounter.Add(1);

    using (var activity = activitySource.StartActivity("Get data"))
    {
        // Add data the the activity
        // You can see these data in Zipkin
        activity?.AddTag("sample", "value");

        // Http calls are tracked by AddHttpClientInstrumentation
        var str1 = await httpClient.GetStringAsync("https://example.com");
        var str2 = await httpClient.GetStringAsync("https://www.meziantou.net");

        logger.LogInformation("Response1 length: {Length}", str1.Length);
        logger.LogInformation("Response2 length: {Length}", str2.Length);
    }

    return Results.Ok();
});

app.Run();
