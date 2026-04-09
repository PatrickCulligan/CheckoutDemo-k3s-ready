namespace Checkout.Data;

public class CheckoutAudit
{
    public int Id { get; set; }
    public string RequestId { get; set; } = string.Empty;
    public string ItemId { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal? Total { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Error { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
