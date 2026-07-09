using Mocha;

namespace Contracts;

public sealed record GetStockInfoRequest(Guid OrderId, string ProductName, int Quantity)
    : IEventRequest<GetStockInfoResult>;
