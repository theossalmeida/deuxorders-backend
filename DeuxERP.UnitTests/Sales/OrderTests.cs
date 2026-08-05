using DeuxERP.Domain.Sales;
using FluentAssertions;

namespace DeuxERP.UnitTests.Sales;

public class OrderTests
{
    [Fact]
    [Trait("TestSet", "Orders")]
    public void AddItem_RecalculatesOrderTotals()
    {
        var order = NewOrder();
        var productId = Guid.NewGuid();

        order.AddItem(productId, 2, 1_500, 2_000, "birthday cake");

        order.TotalPaid.Should().Be(3_000);
        order.TotalValue.Should().Be(4_000);
        order.Items.Should().ContainSingle().Which.Quantity.Should().Be(2);
    }

    [Fact]
    [Trait("TestSet", "Orders")]
    public void CancelItem_RemovesItemFromTotals()
    {
        var order = NewOrder();
        var productId = Guid.NewGuid();
        order.AddItem(productId, 2, 1_500, 2_000, null);

        order.CancelItem(productId);

        order.TotalPaid.Should().Be(0);
        order.TotalValue.Should().Be(0);
        order.Items.Single().ItemCanceled.Should().BeTrue();
    }

    [Fact]
    [Trait("TestSet", "Orders")]
    public void UpdateItemQuantity_WhenResultWouldBeZero_RejectsChange()
    {
        var order = NewOrder();
        var productId = Guid.NewGuid();
        order.AddItem(productId, 2, 1_500, 2_000, null);

        var act = () => order.UpdateItemQuantity(productId, -2);

        act.Should().Throw<InvalidOperationException>();
        order.Items.Single().Quantity.Should().Be(2);
    }

    [Fact]
    [Trait("TestSet", "Orders")]
    public void MarkAsCanceled_WhenCompleted_RejectsTransition()
    {
        var order = NewOrder();
        order.MarkAsCompleted();

        var act = order.MarkAsCanceled;

        act.Should().Throw<InvalidOperationException>();
        order.Status.Should().Be(OrderStatus.Completed);
    }

    private static Order NewOrder() => new(Guid.NewGuid(), DateTime.UtcNow.AddDays(1));
}
