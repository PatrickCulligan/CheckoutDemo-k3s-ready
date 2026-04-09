using Shared;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.Use(async (context, next) =>
{
    var requestId = context.Request.Headers["X-Request-Id"].FirstOrDefault() ?? "missing";
    app.Logger.LogInformation("Pricing {Method} {Path} requestId={RequestId}",
        context.Request.Method, context.Request.Path, requestId);
    await next();
});

app.MapGet("/health", () => Results.Ok(new { ok = true }));

app.MapPost("/price", (PricingRequest request) =>
{
    decimal unitPrice = request.ItemId switch
    {
        "SKU-1" => 9.99m,
        "SKU-2" => 19.99m,
        "SKU-3" => 29.99m,
        _ => 4.99m
    };

    return Results.Ok(new PricingResponse(
        request.ItemId,
        request.Quantity,
        unitPrice,
        unitPrice * request.Quantity));
});

app.Run();
