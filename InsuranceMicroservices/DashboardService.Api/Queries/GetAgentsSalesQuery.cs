using System;
using MediatR;

namespace DashboardService.Api.Queries;

public class GetAgentsSalesQuery : SalesQuery, IRequest<GetAgentsSalesResult>
{
    public string? AgentLogin { get; set; }
    public string? ProductCode { get; set; }
}