namespace VeaMarketplace.Shared.Models;

public enum ProductReportReason
{
    Counterfeit = 0,
    ProhibitedItem = 1,
    MisleadingDescription = 2,
    Scam = 3,
    InappropriateContent = 4,
    IntellectualProperty = 5,
    PriceGouging = 6,
    Other = 7
}

public class ProductReport
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ProductId { get; set; } = string.Empty;
    public string ProductTitle { get; set; } = string.Empty;
    public string SellerId { get; set; } = string.Empty;
    public string SellerUsername { get; set; } = string.Empty;
    public string ReporterId { get; set; } = string.Empty;
    public string ReporterUsername { get; set; } = string.Empty;
    public ProductReportReason Reason { get; set; }
    public string? AdditionalInfo { get; set; }
    public ReportStatus Status { get; set; } = ReportStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? Resolution { get; set; }
    public string? ModeratorNotes { get; set; }
}
