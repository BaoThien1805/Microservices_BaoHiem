using System.ComponentModel.DataAnnotations;
namespace DashboardService.Api.Queries;
public abstract class SalesQuery : IValidatableObject
{
    public DateTime SalesDateFrom { get; set; }
    public DateTime SalesDateTo { get; set; }
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (SalesDateFrom != default && SalesDateTo != default && SalesDateFrom > SalesDateTo)
            yield return new ValidationResult("SalesDateFrom must not exceed SalesDateTo.",
                new[] { nameof(SalesDateFrom), nameof(SalesDateTo) });
    }
}
