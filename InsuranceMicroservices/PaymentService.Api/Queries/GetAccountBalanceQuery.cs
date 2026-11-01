using MediatR;

namespace PaymentService.Api.Queries;

public class GetAccountBalanceQuery : IRequest<GetAccountBalanceQueryResult>
{
    [System.ComponentModel.DataAnnotations.Required]
    public string PolicyNumber { get; set; }
}