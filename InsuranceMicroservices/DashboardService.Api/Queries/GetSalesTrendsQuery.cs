using System;
using DashboardService.Api.Queries.Dtos;
using MediatR;

namespace DashboardService.Api.Queries;

public class GetSalesTrendsQuery : SalesQuery, IRequest<GetSalesTrendsResult>
{
    public string? ProductCode { get; set; }
    [System.ComponentModel.DataAnnotations.EnumDataType(typeof(TimeUnit))]
    public TimeUnit Unit { get; set; }
}