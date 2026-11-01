using System.Collections.Generic;

namespace ProductService.Api.Commands.Dtos;

public class ProductDraftDto
{
    [System.ComponentModel.DataAnnotations.Required]
    public string Code { get; set; }
    [System.ComponentModel.DataAnnotations.Required]
    public string Name { get; set; }
    public string Image { get; set; }
    public string Description { get; set; }
    public IList<CoverDto> Covers { get; set; } = new List<CoverDto>();
    public IList<QuestionDto> Questions { get; set; } = new List<QuestionDto>();
    [System.ComponentModel.DataAnnotations.Range(1, int.MaxValue)]
    public int MaxNumberOfInsured { get; set; }

    public string Icon { get; set; }
}