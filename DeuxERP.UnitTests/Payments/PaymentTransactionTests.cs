using DeuxERP.Domain.Payments;
using FluentAssertions;

namespace DeuxERP.UnitTests.Payments;

public class PaymentTransactionTests
{
    [Fact]
    [Trait("TestSet", "PaymentsAndCash")]
    public void PaymentLifecycle_ConfirmsIdempotentlyThenAllowsRefundOnlyFromPaid()
    {
        var payment = NewPayment();

        payment.ConfirmPayment(1800, 200, "Customer", "***123", "4242", "Visa", "receipt", "paid");
        payment.ConfirmPayment(9999, null, null, null, null, null, null, "duplicate");

        payment.Status.Should().Be(PaymentStatus.Paid);
        payment.PaidAmountCents.Should().Be(1800);

        payment.MarkAsRefunded("customer request", "refunded");
        payment.Status.Should().Be(PaymentStatus.Refunded);
        payment.Invoking(x => x.MarkAsDisputed("late dispute", "disputed"))
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    [Trait("TestSet", "PaymentsAndCash")]
    public void TerminalPendingTransitions_CannotBeAppliedTwiceOrConfirmedLater()
    {
        var failed = NewPayment();
        failed.MarkAsFailed("declined");
        failed.Invoking(x => x.ConfirmPayment(1000, null, null, null, null, null, null, "paid"))
            .Should().Throw<InvalidOperationException>();

        var expired = NewPayment();
        expired.MarkAsExpired("expired");
        expired.Invoking(x => x.MarkAsCancelled("cancel", "cancelled"))
            .Should().Throw<InvalidOperationException>();

        var canceled = NewPayment();
        canceled.MarkAsCancelled("customer request", "cancelled");
        canceled.Invoking(x => x.MarkAsFailed("declined"))
            .Should().Throw<InvalidOperationException>();
    }

    private static PaymentTransaction NewPayment() =>
        new(Guid.NewGuid(), $"billing-{Guid.NewGuid():N}", "PIX", 2000, null, Guid.NewGuid().ToString("N"), true);
}
