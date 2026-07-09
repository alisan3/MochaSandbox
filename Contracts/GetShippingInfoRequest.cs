namespace Contracts;

public sealed record GetShippingInfoRequest(Guid OrderId, string ProductName, int Quantity);
