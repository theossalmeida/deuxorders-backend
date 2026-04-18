using System.ComponentModel.DataAnnotations;

public record UpdateClient(
    [Required] string Name,
    string? Mobile,
    bool? Status = null
);
