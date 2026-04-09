using System.Net.Http.Json;
using Checkout.Data;
using Microsoft.EntityFrameworkCore;
using Shared;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

builder.Services.AddHttpClient("pricing", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:PricingBaseUrl"]!);
    client.Timeout = TimeSpan.FromMilliseconds(800);
});

builder.Services.AddHttpClient("inventory", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:InventoryBaseUrl"]!);
    client.Timeout = TimeSpan.FromMilliseconds(800);
});

var app = builder.Build();

await EnsureDatabaseAsync(app);

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
        "Checkout incoming {Method} {Path} requestId={RequestId}",
        context.Request.Method,
        context.Request.Path,
        requestId);

    await next();

    app.Logger.LogInformation(
        "Checkout completed {Method} {Path} status={StatusCode} requestId={RequestId}",
        context.Request.Method,
        context.Request.Path,
        context.Response.StatusCode,
        requestId);
});

app.MapGet("/health", async (AppDbContext db, CancellationToken cancellationToken) =>
{
    var canConnect = await db.Database.CanConnectAsync(cancellationToken);
    return canConnect ? Results.Ok(new { ok = true, db = "up" }) : Results.StatusCode(503);
});

app.MapPost("/checkout", async (
    CheckoutRequest request,
    IHttpClientFactory httpClientFactory,
    AppDbContext db,
    HttpContext context,
    CancellationToken cancellationToken) =>
{
    var requestId = (string)context.Items["RequestId"]!;

    if (string.IsNullOrWhiteSpace(request.ItemId) || request.Quantity <= 0)
    {
        await SaveAudit(db, requestId, request.ItemId, request.Quantity, null, "InvalidInput", "Invalid itemId or quantity", cancellationToken);
        return Results.BadRequest(new ErrorResponse(requestId, "Invalid itemId or quantity"));
    }

    PricingResponse? pricing;
    InventoryResponse? inventory;

    try
    {
        var pricingClient = httpClientFactory.CreateClient("pricing");

        using var pricingRequest = new HttpRequestMessage(HttpMethod.Post, "/price")
        {
            Content = JsonContent.Create(new PricingRequest(request.ItemId, request.Quantity))
        };
        pricingRequest.Headers.Add("X-Request-Id", requestId);

        var pricingResponse = await pricingClient.SendAsync(pricingRequest, cancellationToken);

        if (!pricingResponse.IsSuccessStatusCode)
        {
            await SaveAudit(db, requestId, request.ItemId, request.Quantity, null, "PricingFailed", "Pricing service returned non-success", cancellationToken);
            return Results.Json(new ErrorResponse(requestId, "pricing unavailable"), statusCode: 503);
        }

        pricing = await pricingResponse.Content.ReadFromJsonAsync<PricingResponse>(cancellationToken: cancellationToken);

        if (pricing is null)
        {
            await SaveAudit(db, requestId, request.ItemId, request.Quantity, null, "PricingFailed", "Pricing response was null", cancellationToken);
            return Results.Json(new ErrorResponse(requestId, "pricing unavailable"), statusCode: 503);
        }
    }
    catch (TaskCanceledException)
    {
        await SaveAudit(db, requestId, request.ItemId, request.Quantity, null, "PricingTimeout", "Pricing timed out", cancellationToken);
        return Results.Json(new ErrorResponse(requestId, "pricing timeout"), statusCode: 503);
    }
    catch (Exception ex)
    {
        await SaveAudit(db, requestId, request.ItemId, request.Quantity, null, "PricingError", ex.Message, cancellationToken);
        return Results.Json(new ErrorResponse(requestId, "pricing error"), statusCode: 503);
    }

    try
    {
        var inventoryClient = httpClientFactory.CreateClient("inventory");

        using var inventoryRequest = new HttpRequestMessage(HttpMethod.Post, "/reserve")
        {
            Content = JsonContent.Create(new InventoryRequest(request.ItemId, request.Quantity))
        };
        inventoryRequest.Headers.Add("X-Request-Id", requestId);

        var inventoryResponse = await inventoryClient.SendAsync(inventoryRequest, cancellationToken);

        if (!inventoryResponse.IsSuccessStatusCode)
        {
            await SaveAudit(db, requestId, request.ItemId, request.Quantity, pricing.Total, "InventoryFailed", "Inventory service returned non-success", cancellationToken);
            return Results.Json(new ErrorResponse(requestId, "inventory unavailable"), statusCode: 503);
        }

        inventory = await inventoryResponse.Content.ReadFromJsonAsync<InventoryResponse>(cancellationToken: cancellationToken);

        if (inventory is null)
        {
            await SaveAudit(db, requestId, request.ItemId, request.Quantity, pricing.Total, "InventoryFailed", "Inventory response was null", cancellationToken);
            return Results.Json(new ErrorResponse(requestId, "inventory unavailable"), statusCode: 503);
        }
    }
    catch (TaskCanceledException)
    {
        await SaveAudit(db, requestId, request.ItemId, request.Quantity, pricing.Total, "InventoryTimeout", "Inventory timed out", cancellationToken);
        return Results.Json(new ErrorResponse(requestId, "inventory timeout"), statusCode: 503);
    }
    catch (Exception ex)
    {
        await SaveAudit(db, requestId, request.ItemId, request.Quantity, pricing.Total, "InventoryError", ex.Message, cancellationToken);
        return Results.Json(new ErrorResponse(requestId, "inventory error"), statusCode: 503);
    }

    if (!inventory.InStock)
    {
        await SaveAudit(db, requestId, request.ItemId, request.Quantity, pricing.Total, "OutOfStock", inventory.Message, cancellationToken);
        return Results.Json(new ErrorResponse(requestId, inventory.Message), statusCode: 409);
    }

    await SaveAudit(db, requestId, request.ItemId, request.Quantity, pricing.Total, "Success", null, cancellationToken);

    return Results.Ok(new CheckoutResponse(
        requestId,
        request.ItemId,
        request.Quantity,
        pricing.Total,
        "Success"));
});

app.Run();

static async Task SaveAudit(
    AppDbContext db,
    string requestId,
    string itemId,
    int quantity,
    decimal? total,
    string status,
    string? error,
    CancellationToken cancellationToken)
{
    db.CheckoutAudits.Add(new CheckoutAudit
    {
        RequestId = requestId,
        ItemId = itemId,
        Quantity = quantity,
        Total = total,
        Status = status,
        Error = error,
        CreatedAtUtc = DateTime.UtcNow
    });

    await db.SaveChangesAsync(cancellationToken);
}

static async Task EnsureDatabaseAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseStartup");

    for (var attempt = 1; attempt <= 20; attempt++)
    {
        try
        {
            await db.Database.OpenConnectionAsync();

            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS checkout_audit (
                    id SERIAL PRIMARY KEY,
                    request_id TEXT NOT NULL,
                    item_id TEXT NOT NULL,
                    quantity INTEGER NOT NULL,
                    total NUMERIC(18,2),
                    status TEXT NOT NULL,
                    error TEXT,
                    created_at_utc TIMESTAMPTZ NOT NULL
                );
            """);

            await db.Database.ExecuteSqlRawAsync("""
                CREATE INDEX IF NOT EXISTS ix_checkout_audit_request_id
                ON checkout_audit (request_id);
            """);

            await db.Database.CloseConnectionAsync();

            logger.LogInformation("Database schema ready on attempt {Attempt}", attempt);
            return;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Database startup attempt {Attempt} failed", attempt);
            await Task.Delay(2000);
        }
    }

    throw new InvalidOperationException("Database schema was not ready after retries.");
}