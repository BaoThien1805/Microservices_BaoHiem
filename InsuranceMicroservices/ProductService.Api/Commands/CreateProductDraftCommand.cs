using MediatR;
using ProductService.Api.Commands.Dtos;

namespace ProductService.Api.Commands;

public class CreateProductDraftCommand : IRequest<CreateProductDraftResult>
{
    [System.ComponentModel.DataAnnotations.Required]
    public ProductDraftDto ProductDraft { get; set; }
}