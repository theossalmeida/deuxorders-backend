using DeuxERP.Domain.Cash;
using DeuxERP.Domain.Cash.Enums;
using FluentAssertions;

namespace DeuxERP.UnitTests.Cash;

public class CashFlowEntryTests
{
    [Fact]
    [Trait("TestSet", "Cash")]
    public void CreateManual_NormalizesInputAndBillingDate()
    {
        var entry = CashFlowEntry.CreateManual(
            new DateTime(2026, 8, 5, 23, 10, 0, DateTimeKind.Utc),
            CashFlowType.Outflow,
            CashFlowCategory.Other,
            " Supplier ",
            2_500,
            " invoice ",
            Guid.NewGuid(),
            "Tester");

        entry.Counterparty.Should().Be("Supplier");
        entry.Notes.Should().Be("invoice");
        entry.BillingDate.Should().Be(new DateTime(2026, 8, 5, 12, 0, 0, DateTimeKind.Utc));
        entry.Source.Should().Be(CashFlowSource.Manual);
    }

    [Fact]
    [Trait("TestSet", "Cash")]
    public void SoftDelete_WithShortReason_DoesNotMutateEntry()
    {
        var entry = CashFlowEntry.CreateManual(
            DateTime.UtcNow, CashFlowType.Inflow, CashFlowCategory.Other,
            "Customer", 1_000, null, Guid.NewGuid(), "Tester");

        var act = () => entry.SoftDelete(Guid.NewGuid(), "Tester", "no");

        act.Should().Throw<ArgumentException>();
        entry.DeletedAt.Should().BeNull();
    }
}
