namespace Shared;

public record CheckoutRequest(string ItemId, int Quantity);

public record CheckoutResponse(
    string RequestId,
    string ItemId,
    int Quantity,
    decimal Total,
    string Status);

public record PricingRequest(string ItemId, int Quantity);

public record PricingResponse(
    string ItemId,
    int Quantity,
    decimal UnitPrice,
    decimal Total);

public record InventoryRequest(string ItemId, int Quantity);

public record InventoryResponse(
    string ItemId,
    int Quantity,
    bool InStock,
    string Message);

public record ErrorResponse(string RequestId, string Error);
