using System.ComponentModel.DataAnnotations;

public record UpdateProduct(
    [Required] string Name,
    [Required] int Price,
    string? Description = null
);
