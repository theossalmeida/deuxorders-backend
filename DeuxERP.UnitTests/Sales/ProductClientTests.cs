using DeuxERP.Domain.Sales;
using FluentAssertions;

namespace DeuxERP.UnitTests.Sales;

public class ProductClientTests
{
    [Fact]
    [Trait("TestSet", "ClientsAndProducts")]
    public void ProductUpdate_RejectsInvalidStateWithoutMutatingExistingValues()
    {
        var product = new Product("Cake", 2000, "Dessert", "M");

        product.Invoking(x => x.Update("", 1000, null, null, null, null))
            .Should().Throw<ArgumentException>();
        product.Invoking(x => x.Update("Cake", -1, null, null, null, null))
            .Should().Throw<ArgumentException>();

        product.Name.Should().Be("Cake");
        product.Price.Should().Be(2000);
    }

    [Fact]
    [Trait("TestSet", "ClientsAndProducts")]
    public void ClientUpdate_NormalizesBlankMobileAndAppliesExplicitStatus()
    {
        var client = new Client("Original");

        client.Update("Updated", "  ", false);

        client.Name.Should().Be("Updated");
        client.Mobile.Should().BeNull();
        client.Status.Should().BeFalse();
    }
}
