using Mocha.Sagas;

namespace Contracts;

public sealed record GetDiscountInfoResult(Guid OrderId, decimal DiscountPercentage) : ICorrelatable
{
    public Guid? CorrelationId => OrderId;
}
