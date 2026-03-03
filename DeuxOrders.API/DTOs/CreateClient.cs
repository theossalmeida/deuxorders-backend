using System.ComponentModel.DataAnnotations;

public record CreateClient(
    [Required] string Name,
    string? Mobile
);