using System.ComponentModel.DataAnnotations;

public record CreateProduct(
    [Required] string Name,
    [Required] int Price,
    string? Description
);