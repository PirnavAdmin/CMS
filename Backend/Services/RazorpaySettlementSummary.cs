namespace AuthDemo.Services;
public sealed record RazorpaySettlementSummary(string Id, string Status, decimal Amount, decimal Fees, decimal Tax, string? Utr, DateTime CreatedAt);
