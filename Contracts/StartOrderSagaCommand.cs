namespace Contracts;

// Integration command published by the Api over RabbitMQ to start the OrderSaga.
public sealed record StartOrderSagaCommand(Guid OrderId, string ProductName, int Quantity);
