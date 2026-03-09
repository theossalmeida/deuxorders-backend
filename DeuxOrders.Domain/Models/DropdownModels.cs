namespace DeuxOrders.Domain.Models
{
    public class DropdownItemModel
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
    }

    public class ProductDropdownModel : DropdownItemModel
    {
        public decimal Price { get; set; }
    }
}