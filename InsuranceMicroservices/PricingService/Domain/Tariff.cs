using DynamicExpresso;
using DynamicExpresso.Exceptions;

namespace PricingService.Domain;

public sealed class Tariff
{
    public Tariff(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Code = code;
    }

    public string Code { get; }
    public BasePremiumRules BasePremiumRules { get; } = new();
    public DiscountMarkupRules DiscountMarkupRules { get; } = new();

    public Price CalculatePrice(Calculation calculation)
    {
        ArgumentNullException.ThrowIfNull(calculation);
        if (!StringComparer.Ordinal.Equals(Code, calculation.ProductCode))
            throw new ArgumentException("The tariff does not match the product.");

        // Expressions come from server-owned tariff definitions. Only scalar answers are exposed.
        var interpreter = new Interpreter().EnableAssignment(AssignmentOperators.None);
        foreach (var (code, value) in calculation.Subject)
        {
            if (value is not (string or decimal or int or bool))
                throw new ArgumentException("Unsupported answer type.");
            interpreter.SetVariable(code, value);
        }

        try
        {
            var multiplier = 1m;
            foreach (var rule in DiscountMarkupRules.Rules)
                if (interpreter.Eval<bool>(rule.Condition)) multiplier = checked(multiplier * rule.Multiplier);

            var rulesByCover = BasePremiumRules.Rules.ToLookup(rule => rule.CoverCode, StringComparer.Ordinal);
            var prices = new Dictionary<string, CoverPrice>(StringComparer.Ordinal);
            foreach (var cover in calculation.SelectedCovers)
            {
                var matchingRules = rulesByCover[cover].Where(rule =>
                    string.IsNullOrWhiteSpace(rule.Condition) || interpreter.Eval<bool>(rule.Condition)).ToArray();
                if (matchingRules.Length != 1)
                    throw new ArgumentException("Each selected cover must have exactly one matching price rule.");
                var premium = checked(interpreter.Eval<decimal>(matchingRules[0].Expression) * multiplier);
                if (premium < 0) throw new ArgumentException("A cover premium cannot be negative.");
                prices.Add(cover, new CoverPrice(decimal.Round(premium, 2, MidpointRounding.AwayFromZero)));
            }
            return new Price(prices);
        }
        catch (ParseException exception)
        {
            throw new ArgumentException("A tariff answer is missing or has an invalid type.", nameof(calculation), exception);
        }
    }
}

public sealed record BasePremiumRule(string CoverCode, string? Condition, string Expression);
public sealed class BasePremiumRules
{
    private readonly List<BasePremiumRule> rules = [];
    public IReadOnlyList<BasePremiumRule> Rules => rules.AsReadOnly();
    public void AddBasePriceRule(string coverCode, string? condition, string expression)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(coverCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(expression);
        rules.Add(new BasePremiumRule(coverCode, condition, expression));
    }
}

public sealed record DiscountMarkupRule(string Condition, decimal Multiplier);
public sealed class DiscountMarkupRules
{
    private readonly List<DiscountMarkupRule> rules = [];
    public IReadOnlyList<DiscountMarkupRule> Rules => rules.AsReadOnly();
    public void AddPercentMarkup(string condition, decimal multiplier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(condition);
        if (multiplier <= 0) throw new ArgumentOutOfRangeException(nameof(multiplier));
        rules.Add(new DiscountMarkupRule(condition, multiplier));
    }
}
