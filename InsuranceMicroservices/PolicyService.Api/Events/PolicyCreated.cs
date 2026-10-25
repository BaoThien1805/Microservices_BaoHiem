using MediatR;
namespace PolicyService.Api.Events;
public class PolicyCreated : INotification
{
    public string PolicyNumber { get; set; } = "";
    public DateTime PolicyFrom { get; set; }
    public DateTime PolicyTo { get; set; }
    public PolicyHolderData PolicyHolder { get; set; } = new();
    public string ProductCode { get; set; } = "";
    public decimal TotalPremium { get; set; }
    public string AgentLogin { get; set; } = "";
}
public class PolicyHolderData
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
}
