using System.Net.Http.Json;
using Shared;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient("checkout", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:CheckoutBaseUrl"]!);
    client.Timeout = TimeSpan.FromSeconds(3);
});

var app = builder.Build();

app.Use(async (context, next) =>
{
    var requestId = context.Request.Headers["X-Request-Id"].FirstOrDefault();
    if (string.IsNullOrWhiteSpace(requestId))
    {
        requestId = Guid.NewGuid().ToString("n");
    }

    context.Items["RequestId"] = requestId;
    context.Response.Headers["X-Request-Id"] = requestId;

    app.Logger.LogInformation(
        "Gateway incoming {Method} {Path} requestId={RequestId}",
        context.Request.Method,
        context.Request.Path,
        requestId);

    await next();

    app.Logger.LogInformation(
        "Gateway completed {Method} {Path} status={StatusCode} requestId={RequestId}",
        context.Request.Method,
        context.Request.Path,
        context.Response.StatusCode,
        requestId);
});

app.MapGet("/", () =>
{
    return Results.Content(
        "<html><body><h1>Checkout Gateway</h1><p>POST /api/checkout</p></body></html>",
        "text/html");
});

app.MapGet("/api/arch", () =>
{
    return Results.Text("gateway -> checkout -> pricing + inventory + postgres");
});

app.MapGet("/api/ping", () =>
{
    return Results.Ok(new { ok = true, utc = DateTime.UtcNow });
});

app.MapPost("/api/checkout", async (
    CheckoutRequest request,
    IHttpClientFactory httpClientFactory,
    HttpContext context,
    CancellationToken cancellationToken) =>
{
    var requestId = (string)context.Items["RequestId"]!;

    var client = httpClientFactory.CreateClient("checkout");

    using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/checkout")
    {
        Content = JsonContent.Create(request)
    };
    httpRequest.Headers.Add("X-Request-Id", requestId);

    var response = await client.SendAsync(httpRequest, cancellationToken);
    var content = await response.Content.ReadAsStringAsync(cancellationToken);

    return Results.Content(content, "application/json", statusCode: (int)response.StatusCode);
});

app.Run();
