using Contracts;
using Mocha;
using Mocha.Sagas;
using Mocha.Transport.RabbitMQ;
using OrderService;

var builder = WebApplication.CreateBuilder(args);

builder.AddRabbitMQClient("rabbitmq");

builder
    .Services.AddMessageBus()
    .AddOrderService()
    .AddRabbitMQ(t =>
    {
        t.BindExplicitly(); // no per-handler auto queues/consumers
        t.AutoProvision(false); // don't declare anything not opted-in

        t.DeclareExchange(WellKnown.Exchanges.Events)
            .Type(RabbitMQExchangeType.Topic)
            .Durable()
            .AutoProvision(true);

        t.DeclareQueue(WellKnown.Queues.OrderServiceQueue)
            .Durable()
            .WithArgument("x-queue-type", RabbitMQQueueType.Quorum)
            .AutoProvision(true);

        t.DeclareBinding(WellKnown.Exchanges.Events, WellKnown.Queues.OrderServiceQueue)
            .RoutingKey(WellKnown.RoutingKeys.OrderPlaced)
            .AutoProvision(true);

        t.DeclareBinding(WellKnown.Exchanges.Events, WellKnown.Queues.OrderServiceQueue)
            .RoutingKey(WellKnown.RoutingKeys.OrderCancelled)
            .AutoProvision(true);

        t.Queue(WellKnown.Queues.OrderServiceQueue)
            .Durable()
            .WithArgument("x-queue-type", RabbitMQQueueType.Quorum)
            .AutoProvision(true)
            .BindExplicitly()
            .Handler<OrderPlacedHandler>()
            .Handler<OrderCancelledHandler>()
            .Handler<GetPriceInfoRequestHandler>();

        // ── OrderSaga: triggered by StartOrderSagaCommand from the Api ──────────
        t.DeclareExchange(WellKnown.Exchanges.Commands)
            .Type(RabbitMQExchangeType.Direct)
            .Durable()
            .AutoProvision(true);

        // t.DeclareQueue(WellKnown.Queues.OrderSagaQueue)
        //     .Durable()
        //     .WithArgument("x-queue-type", RabbitMQQueueType.Quorum)
        //     .AutoProvision(true);

        t.DeclareBinding(WellKnown.Exchanges.Commands, WellKnown.Queues.OrderSagaQueue)
            .RoutingKey(WellKnown.RoutingKeys.StartOrderSaga)
            .AutoProvision(true);

        t.Queue(WellKnown.Queues.OrderSagaQueue)
            .Durable()
            .WithArgument("x-queue-type", RabbitMQQueueType.Quorum)
            .AutoProvision(true)
            .BindExplicitly()
            .Consumer(typeof(OrderSaga));

        // ── GetStockInfo RPC: the saga calls OrderService itself ────────────────
        t.DeclareExchange(WellKnown.Exchanges.Rpc)
            .Type(RabbitMQExchangeType.Direct)
            .Durable()
            .AutoProvision(true);

        t.DeclareQueue(WellKnown.Queues.StockQueue)
            .Durable()
            .WithArgument("x-queue-type", RabbitMQQueueType.Quorum)
            .AutoProvision(true);

        t.DeclareBinding(WellKnown.Exchanges.Rpc, WellKnown.Queues.StockQueue)
            .RoutingKey(WellKnown.RoutingKeys.GetStockInfoRequest)
            .AutoProvision(true);

        t.DeclareBinding(WellKnown.Exchanges.Events, WellKnown.Queues.OrderSagaQueue)
            .RoutingKey(WellKnown.RoutingKeys.GetStockInfoResult)
            .AutoProvision(true);

        t.DeclareBinding(WellKnown.Exchanges.Events, WellKnown.Queues.OrderServiceQueue)
            .RoutingKey(WellKnown.RoutingKeys.GetPriceInfoRequest)
            .AutoProvision(true);

        t.DeclareBinding(WellKnown.Exchanges.Events, WellKnown.Queues.OrderSagaQueue)
            .RoutingKey(WellKnown.RoutingKeys.GetPriceInfoResult)
            .AutoProvision(true);

        // The saga self-sends OrderReadyToComplete once all responses are in.
        t.DeclareBinding(WellKnown.Exchanges.Events, WellKnown.Queues.OrderSagaQueue)
            .RoutingKey(WellKnown.RoutingKeys.OrderReadyToComplete)
            .AutoProvision(true);

        t.Queue(WellKnown.Queues.StockQueue)
            .Durable()
            .WithArgument("x-queue-type", RabbitMQQueueType.Quorum)
            .AutoProvision(true)
            .BindExplicitly()
            .Handler<GetStockInfoRequestHandler>();

        // Routes the saga's outbound GetStockInfo to the stock queue. With
        // AutoProvision(false) + BindHandlersExplicitly() an explicit dispatch
        // endpoint is REQUIRED: the saga's .Send resolves via the convention
        // (CreateEndpointConfiguration(OutboundRoute)) which ignores any
        // AddMessage(...).Send(ToRabbitMQQueue(...)) destination and would route to
        // an unbound convention exchange (message dropped). ToQueue publishes to the
        // default exchange with the routing key set to the queue name, delivering
        // the message straight to the handler's queue.
        // t.DispatchEndpoint("stock-dispatch")
        //     .ToQueue(WellKnown.Queues.StockQueue)
        //     .Send<GetStockInfo>();

        // The handler sends GetStockInfoResult back to the saga's own queue,
        // where the saga's OnSend<GetStockInfoResult> route picks it up and
        // correlates it to the running instance via ICorrelatable.
        // t.DispatchEndpoint("saga-result-dispatch")
        //     .ToExchange(WellKnown.Exchanges.Events)
        //     //.ToQueue(WellKnown.Queues.OrderSagaQueue)
        //     .Send<GetStockInfoResult>()
        // ;
    })
    .AddMessage<GetStockInfoRequest>(d =>
    {
        d.UseRabbitMQRoutingKey<GetStockInfoRequest>(_ =>
            WellKnown.RoutingKeys.GetStockInfoRequest
        );
        d.Send(r => r.ToExchange(WellKnown.Exchanges.Rpc));
    })
    .AddMessage<GetStockInfoResult>(d =>
    {
        d.UseRabbitMQRoutingKey<GetStockInfoResult>(_ => WellKnown.RoutingKeys.GetStockInfoResult);
        //d.Send(r => r.ToExchange(WellKnown.Exchanges.Events));
    })
    .AddMessage<GetPriceInfoRequest>(d =>
    {
        d.UseRabbitMQRoutingKey<GetPriceInfoRequest>(_ =>
            WellKnown.RoutingKeys.GetPriceInfoRequest
        );
        d.Send(r => r.ToExchange(WellKnown.Exchanges.Events));
    })
    .AddMessage<GetPriceInfoResult>(d =>
    {
        d.UseRabbitMQRoutingKey<GetPriceInfoResult>(_ => WellKnown.RoutingKeys.GetPriceInfoResult);
        d.Send(r => r.ToExchange(WellKnown.Exchanges.Events));
    })
    .AddMessage<OrderReadyToComplete>(d =>
    {
        d.UseRabbitMQRoutingKey<OrderReadyToComplete>(_ =>
            WellKnown.RoutingKeys.OrderReadyToComplete
        );
        d.Send(r => r.ToExchange(WellKnown.Exchanges.Events));
    });

// Saga state persistence (development/in-memory).
builder.Services.AddInMemorySagas();

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new { Service = "OrderService", Status = "Running" }));

app.Run();
