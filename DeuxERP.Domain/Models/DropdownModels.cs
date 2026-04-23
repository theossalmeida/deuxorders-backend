namespace DeuxERP.Domain.Models
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

    public record InventoryDropdownModel(Guid Id, string Name, string MeasureUnit);
}
