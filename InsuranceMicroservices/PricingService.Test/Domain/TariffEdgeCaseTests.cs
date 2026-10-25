using PricingService.Domain;

namespace PricingService.Test.Domain;

public class TariffEdgeCaseTests
{
    [Fact]
    public void TravelWorldMarkupAppliesToEverySelectedCover()
    {
        var price = TariffFactory.Travel().CalculatePrice(Calculate("TRI", ["C1", "C2", "C3"], new()
        {
            ["NUM_OF_ADULTS"] = 1m,
            ["NUM_OF_CHILDREN"] = 1m,
            ["DESTINATION"] = "WORLD"
        }));

        Assert.Equal(159m, price.TotalPremium);
        Assert.Equal(51m, price.Covers["C1"].Price);
        Assert.Equal(78m, price.Covers["C2"].Price);
        Assert.Equal(30m, price.Covers["C3"].Price);
    }

    [Fact]
    public void MultipleMatchingMarkupsAreCombined()
    {
        var price = TariffFactory.Farm().CalculatePrice(Calculate("FAI", ["C1", "C2", "C3", "C4"], new()
        {
            ["FLOOD"] = "YES",
            ["NUM_OF_CLAIM"] = 3m
        }));

        Assert.Equal(300m, price.TotalPremium);
    }

    [Fact]
    public void UnknownSelectedCoverIsRejected()
    {
        var calculation = Calculate("CAR", ["UNKNOWN"], new() { ["NUM_OF_CLAIM"] = 0m });
        Assert.Throws<ArgumentException>(() => TariffFactory.Car().CalculatePrice(calculation));
    }

    [Fact]
    public void MissingAnswerIsRejectedInsteadOfReturningAnIncorrectPrice()
    {
        var calculation = Calculate("CAR", ["C1"], new());
        Assert.Throws<ArgumentException>(() => TariffFactory.Car().CalculatePrice(calculation));
    }

    private static Calculation Calculate(string product, List<string> covers, Dictionary<string, object> answers) =>
        new(product, DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddYears(1), covers, answers);
}
