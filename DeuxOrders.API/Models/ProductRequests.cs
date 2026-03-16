using System.ComponentModel.DataAnnotations;

namespace DeuxOrders.API.Models
{
    public class CreateProductRequest
    {
        [Required] public string Name { get; set; } = string.Empty;
        [Required] public int Price { get; set; }
        public string? Description { get; set; }
        public string? Category { get; set; }
        public string? Size { get; set; }
        public IFormFile? Image { get; set; }
    }

    public class UpdateProductRequest
    {
        [Required] public string Name { get; set; } = string.Empty;
        [Required] public int Price { get; set; }
        public string? Description { get; set; }
        public string? Category { get; set; }
        public string? Size { get; set; }
        public IFormFile? Image { get; set; }
    }
}
