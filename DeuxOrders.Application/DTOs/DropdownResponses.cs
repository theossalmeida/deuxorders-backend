namespace DeuxOrders.Application.DTOs
{
    public class DropdownItemDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
    }
    public class ProductDropdownDto : DropdownItemDto
    {
        public decimal Price { get; set; }
    }
}