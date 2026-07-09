namespace Contracts;

public sealed record GetDiscountInfoRequest(Guid OrderId, string ProductName, int Quantity);
