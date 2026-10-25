using System;

namespace DashboardService.Api.Queries.Dtos;

public class PeriodSalesDto
{
    public DateTime PeriodDate { get; set; }

    public string Period { get; set; } = "";

    public SalesDto Sales { get; set; } = new();
}
public class PeriodSaleDto : PeriodSalesDto { }
