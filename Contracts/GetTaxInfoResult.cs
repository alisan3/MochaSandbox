using Mocha.Sagas;

namespace Contracts;

public sealed record GetTaxInfoResult(Guid OrderId, decimal TaxAmount) : ICorrelatable
{
    public Guid? CorrelationId => OrderId;
}
