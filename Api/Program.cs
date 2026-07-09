using Contracts;
using Mocha;
using Mocha.Transport.RabbitMQ;

var builder = WebApplication.CreateBuilder(args);

builder.AddRabbitMQClient("rabbitmq");

builder
    .Services.AddMessageBus()
    .AddRabbitMQ(t =>
    {
        t.BindExplicitly(); // no per-handler auto queues/consumers
        t.AutoProvision(false); // don't declare anything not opted-in

        t.DeclareExchange(WellKnown.Exchanges.Events).Type(RabbitMQExchangeType.Topic).Durable();
        t.DeclareExchange(WellKnown.Exchanges.Commands).Type(RabbitMQExchangeType.Direct).Durable();
        t.DeclareExchange(WellKnown.Exchanges.Rpc).Type(RabbitMQExchangeType.Direct).Durable();
    })
    .AddMessage<OrderPlaced>(d =>
    {
        d.UseRabbitMQRoutingKey<OrderPlaced>(_ => WellKnown.RoutingKeys.OrderPlaced);
        d.Publish(r => r.ToExchange(WellKnown.Exchanges.Events));
    })
    .AddMessage<OrderCancelled>(d =>
    {
        d.UseRabbitMQRoutingKey<OrderCancelled>(_ => WellKnown.RoutingKeys.OrderCancelled);
        d.Publish(r => r.ToExchange(WellKnown.Exchanges.Events));
    })
    .AddMessage<StartOrderSagaCommand>(d =>
    {
        d.UseRabbitMQRoutingKey<StartOrderSagaCommand>(_ => WellKnown.RoutingKeys.StartOrderSaga);
        d.Send(r => r.ToExchange(WellKnown.Exchanges.Commands));
    })
    .AddMessage<GetStockInfoRequest>(d =>
    {
        d.UseRabbitMQRoutingKey<GetStockInfoRequest>(_ =>
            WellKnown.RoutingKeys.GetStockInfoRequest
        );
        d.Send(r => r.ToExchange(WellKnown.Exchanges.Rpc));
    });

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new { Service = "Api", Status = "Running" }));

app.MapPost(
    "/orders",
    async (IMessageBus bus, CancellationToken cancellationToken) =>
    {
        var orderPlaced = new OrderPlaced(
            OrderId: Guid.NewGuid(),
            ProductName: "Mechanical Keyboard",
            Amount: 149.99m
        );

        await bus.PublishAsync(orderPlaced, cancellationToken);

        return Results.Accepted(
            $"/orders/{orderPlaced.OrderId}",
            new
            {
                orderPlaced.OrderId,
                orderPlaced.ProductName,
                orderPlaced.Amount,
                Status = "Published",
            }
        );
    }
);

app.MapPost(
    "/orders/{orderId:guid}/cancel",
    async (Guid orderId, IMessageBus bus, CancellationToken cancellationToken) =>
    {
        var orderCancelled = new OrderCancelled(OrderId: orderId);

        await bus.PublishAsync(orderCancelled, cancellationToken);

        return Results.Accepted(
            $"/orders/{orderCancelled.OrderId}/cancel",
            new { orderCancelled.OrderId, Status = "Published" }
        );
    }
);

app.MapPost(
    "/orders/saga",
    async (IMessageBus bus, CancellationToken cancellationToken) =>
    {
        var command = new StartOrderSagaCommand(
            OrderId: Guid.NewGuid(),
            ProductName: "Mechanical Keyboard",
            Quantity: 3
        );

        await bus.SendAsync(command, cancellationToken);

        return Results.Accepted(
            $"/orders/{command.OrderId}",
            new
            {
                command.OrderId,
                command.ProductName,
                command.Quantity,
                Status = "SagaStarted",
            }
        );
    }
);

app.MapPost(
    "/orders/stockinfo",
    async (IMessageBus bus, CancellationToken cancellationToken) =>
    {
        var request = new GetStockInfoRequest(
            OrderId: Guid.NewGuid(),
            ProductName: "Mechanical Keyboard",
            Quantity: 3
        );

        var result = await bus.RequestAsync(request, cancellationToken);

        return Results.Accepted(
            $"/orders/{request.OrderId}/stockinfo",
            new
            {
                result.InStock,
                result.AvailableQuantity,
                Status = "StockInfoRetrieved",
            }
        );
    }
);

app.Run();
