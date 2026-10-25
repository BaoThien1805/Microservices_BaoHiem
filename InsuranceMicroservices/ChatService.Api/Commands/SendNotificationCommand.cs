using MediatR;
using System.ComponentModel.DataAnnotations;

namespace ChatService.Api.Commands;

public class SendNotificationCommand : IRequest<Unit>
{
    [Required, StringLength(4000)]
    public string Message { get; set; } = "";
}