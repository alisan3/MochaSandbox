using Mocha.Sagas;

namespace Contracts;

public sealed record GetShippingInfoResult(Guid OrderId, decimal ShippingCost) : ICorrelatable
{
    public Guid? CorrelationId => OrderId;
}
