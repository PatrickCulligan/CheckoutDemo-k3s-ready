using Shared;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.Use(async (context, next) =>
{
    var requestId = context.Request.Headers["X-Request-Id"].FirstOrDefault() ?? "missing";
    app.Logger.LogInformation("Inventory {Method} {Path} requestId={RequestId}",
        context.Request.Method, context.Request.Path, requestId);
    await next();
});

app.MapGet("/health", () => Results.Ok(new { ok = true }));

app.MapPost("/reserve", (InventoryRequest request) =>
{
    if (request.ItemId == "OOS-1")
    {
        return Results.Ok(new InventoryResponse(
            request.ItemId,
            request.Quantity,
            false,
            "Item is out of stock"));
    }

    return Results.Ok(new InventoryResponse(
        request.ItemId,
        request.Quantity,
        true,
        "Reserved"));
});

app.Run();
