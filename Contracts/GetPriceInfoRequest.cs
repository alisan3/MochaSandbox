using Mocha;

namespace Contracts;

public sealed record GetPriceInfoRequest(Guid OrderId, string ProductName, int Quantity);
