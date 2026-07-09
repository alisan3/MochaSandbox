namespace Contracts;

public sealed record GetTaxInfoRequest(Guid OrderId, string ProductName, int Quantity);
