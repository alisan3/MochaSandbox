namespace OrderService;

// Internal saga signal: the saga sends this to itself on entering
// AwaitingResponses once every response has arrived (see OrderSaga). Correlation
// back to the running instance is carried by the saga-id header that the saga
// stamps on its own sends, so this type does not need to implement ICorrelatable.
public sealed record OrderReadyToComplete(Guid OrderId);
