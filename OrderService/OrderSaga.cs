using Contracts;
using Mocha.Sagas;

namespace OrderService;

public sealed class OrderSagaState : SagaStateBase
{
    public Guid OrderId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public bool InStock { get; set; }
    public int AvailableQuantity { get; set; }
    public decimal Price { get; set; }

    public bool AllTasksFinished => InStock && Price > 0;
}

public sealed class OrderSaga : Saga<OrderSagaState>
{
    private const string AwaitingResponses = nameof(AwaitingResponses);
    private const string Completed = nameof(Completed);

    protected override void Configure(ISagaDescriptor<OrderSagaState> descriptor)
    {
        // Triggered by the StartOrderSagaCommand sent from the Api over RabbitMQ.
        // Creates the saga state and issues a GetStockInfo RPC request back to
        // OrderService itself, then waits for the reply.
        descriptor
            .Initially()
            .OnSend<StartOrderSagaCommand>()
            .StateFactory(command => new OrderSagaState
            {
                // The state Id doubles as the correlation key so the
                // GetStockInfoResult (ICorrelatable by OrderId) maps back here.
                Id = command.OrderId,
                OrderId = command.OrderId,
                ProductName = command.ProductName,
                Quantity = command.Quantity,
            })
            .Send(
                (_, state) =>
                    new GetStockInfoRequest(state.OrderId, state.ProductName, state.Quantity)
            )
            .Send(
                (_, state) =>
                    new GetPriceInfoRequest(state.OrderId, state.ProductName, state.Quantity)
            )
            .TransitionTo(AwaitingResponses);

        // Handles the GetStockInfoResult sent back by GetStockInfoHandler.
        descriptor
            .During(AwaitingResponses)
            .OnReply<GetStockInfoResult>()
            .Then(
                (state, reply) =>
                {
                    state.InStock = reply.InStock;
                    state.AvailableQuantity = reply.AvailableQuantity;
                }
            )
            .TransitionTo(AwaitingResponses);

        descriptor
            .During(AwaitingResponses)
            .OnSend<GetPriceInfoResult>()
            .Then(
                (state, reply) =>
                {
                    state.Price = reply.Price;
                }
            )
            .TransitionTo(AwaitingResponses);

        descriptor
            .During(AwaitingResponses)
            .OnEntry()
            .Send(
                (_, state) =>
                    state.AllTasksFinished ? new OrderReadyToComplete(state.OrderId) : null,
                null
            );

        descriptor.During(AwaitingResponses).OnSend<OrderReadyToComplete>().TransitionTo(Completed);

        descriptor.Finally(Completed);
    }
}
