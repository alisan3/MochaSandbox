using Mocha.Sagas;

namespace Contracts;

public sealed record GetPriceInfoResult(Guid OrderId, decimal Price) : ICorrelatable
{
    public Guid? CorrelationId => OrderId;
};
