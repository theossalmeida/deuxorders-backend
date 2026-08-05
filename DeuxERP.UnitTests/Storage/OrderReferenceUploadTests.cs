using DeuxERP.Domain.Storage;
using FluentAssertions;

namespace DeuxERP.UnitTests.Storage;

public class OrderReferenceUploadTests
{
    [Fact]
    [Trait("TestSet", "Storage")]
    public void Consume_EnforcesExpiryBoundOrderAndSingleUse()
    {
        var now = DateTime.UtcNow;
        var expectedOrder = Guid.NewGuid();

        var expired = NewUpload(now.AddMinutes(-1));
        expired.Invoking(x => x.Consume(expectedOrder, now)).Should().Throw<InvalidOperationException>();

        var bound = NewUpload(now.AddMinutes(5), expectedOrder);
        bound.Invoking(x => x.Consume(Guid.NewGuid(), now)).Should().Throw<InvalidOperationException>();

        var valid = NewUpload(now.AddMinutes(5));
        valid.Consume(expectedOrder, now);
        valid.IsConsumed.Should().BeTrue();
        valid.OrderId.Should().Be(expectedOrder);
        valid.Invoking(x => x.Consume(expectedOrder, now.AddSeconds(1))).Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData("order-references/not-a-guid.png", false)]
    [InlineData("order-references/00000000-0000-0000-0000-000000000001.exe", false)]
    [InlineData("wrong/00000000-0000-0000-0000-000000000001.png", false)]
    [InlineData("order-references/00000000-0000-0000-0000-000000000001.webp", true)]
    [Trait("TestSet", "Storage")]
    public void ObjectKeyValidation_RestrictsPrefixIdentityAndExtension(string key, bool expected) =>
        OrderReferenceObjectKey.IsValid(key).Should().Be(expected);

    private static OrderReferenceUpload NewUpload(DateTime expiresAt, Guid? orderId = null) =>
        new($"order-references/{Guid.NewGuid()}.png", Guid.NewGuid(), orderId, "image/png", expiresAt);
}
