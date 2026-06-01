namespace Contracts;

public sealed record OrderPlaced(Guid OrderId, string ProductName, decimal Amount);
