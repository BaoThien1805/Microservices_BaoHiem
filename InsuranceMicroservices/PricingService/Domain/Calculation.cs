namespace PricingService.Domain;

public sealed class Calculation
{
    public Calculation(string productCode, DateTimeOffset policyFrom, DateTimeOffset policyTo,
        List<string> selectedCovers, Dictionary<string, object> subject)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productCode);
        ArgumentNullException.ThrowIfNull(selectedCovers);
        ArgumentNullException.ThrowIfNull(subject);
        if (policyFrom == default || policyTo < policyFrom)
            throw new ArgumentException("The policy dates are invalid.");
        if (selectedCovers.Any(string.IsNullOrWhiteSpace) || selectedCovers.Distinct(StringComparer.Ordinal).Count() != selectedCovers.Count)
            throw new ArgumentException("Cover codes must be non-empty and unique.");
        ProductCode = productCode;
        PolicyFrom = policyFrom;
        PolicyTo = policyTo;
        SelectedCovers = selectedCovers.AsReadOnly();
        Subject = new System.Collections.ObjectModel.ReadOnlyDictionary<string, object>(subject);
    }

    public string ProductCode { get; }
    public DateTimeOffset PolicyFrom { get; }
    public DateTimeOffset PolicyTo { get; }
    public IReadOnlyList<string> SelectedCovers { get; }
    public IReadOnlyDictionary<string, object> Subject { get; }
}

public sealed record CoverPrice(decimal Price);
public sealed record Price(IReadOnlyDictionary<string, CoverPrice> Covers)
{
    public decimal TotalPremium => Covers.Values.Sum(cover => cover.Price);
}
