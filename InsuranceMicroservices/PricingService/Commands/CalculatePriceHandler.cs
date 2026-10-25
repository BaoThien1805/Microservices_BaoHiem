using FluentValidation;
using MediatR;
using PricingService.Api.Commands;
using PricingService.Domain;
namespace PricingService.Commands;

public sealed class CalculatePriceHandler(IDataStore dataStore) : IRequestHandler<CalculatePriceCommand, CalculatePriceResult>
{
    public async Task<CalculatePriceResult> Handle(CalculatePriceCommand request, CancellationToken cancellationToken)
    {
        await new CalculatePriceCommandValidator().ValidateAndThrowAsync(request, cancellationToken);
        if (request.Answers.Any(answer => answer is null || string.IsNullOrWhiteSpace(answer.QuestionCode)))
            throw new ArgumentException("Every answer requires a question code.");
        var answers = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var answer in request.Answers)
            if (!answers.TryAdd(answer.QuestionCode, answer.GetAnswer()))
                throw new ArgumentException("Question codes must be unique.");
        var tariff = await dataStore.Tariffs[request.ProductCode].WaitAsync(cancellationToken)
            ?? throw new KeyNotFoundException("No tariff exists for the requested product.");
        var price = tariff.CalculatePrice(new Calculation(request.ProductCode, request.PolicyFrom, request.PolicyTo,
            request.SelectedCovers, answers));
        return new CalculatePriceResult
        {
            TotalPrice = price.TotalPremium,
            CoverPrices = price.Covers.ToDictionary(cover => cover.Key, cover => cover.Value.Price)
        };
    }
}
