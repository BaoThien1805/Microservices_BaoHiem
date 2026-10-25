using System;
using MediatR;

namespace DashboardService.Api.Queries;

public class GetTotalSalesQuery : SalesQuery, IRequest<GetTotalSalesResult>
{
    public string? ProductCode { get; set; }
}