using Mocha;
using Mocha.Sagas;

namespace OrderService;

// One-way command the saga sends to OrderService itself to check stock.
public sealed record GetStockInfoRequest(Guid OrderId, string ProductName, int Quantity)
    : IEventRequest<GetStockInfoResult>;

// The stock-check result is sent back to the saga. It is correlated to the
// running saga instance via ICorrelatable.CorrelationId (the saga state's Id is
// set to the OrderId), because saga request/reply (OnReply) is not yet supported
// over RabbitMQ in Mocha 16.0.3.
public sealed record GetStockInfoResult(Guid OrderId, bool InStock, int AvailableQuantity)
    : ICorrelatable
{
    public Guid? CorrelationId => OrderId;
}
