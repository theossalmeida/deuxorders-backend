using System.ComponentModel.DataAnnotations;

public record CreateProduct(
    [Required] string Name,
    string? Description
);