using DeuxERP.Domain.Inventory;
using FluentAssertions;

namespace DeuxERP.UnitTests.Inventory;

public class InventoryMaterialTests
{
    [Fact]
    [Trait("TestSet", "Inventory")]
    public void Restock_RecalculatesWeightedAverageCost()
    {
        var material = new InventoryMaterial(" Flour ", 10, 10_000, MeasureUnit.G);

        material.Restock(10, 20_000);

        material.Name.Should().Be("Flour");
        material.Quantity.Should().Be(20);
        material.UnitCost.Should().Be(1_500);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [Trait("TestSet", "Inventory")]
    public void Constructor_RejectsNonPositiveQuantity(int quantity)
    {
        var act = () => new InventoryMaterial("Flour", quantity, 1_000, MeasureUnit.G);

        act.Should().Throw<ArgumentException>();
    }
}
