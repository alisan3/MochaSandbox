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
        t.BindHandlersExplicitly(); // no per-handler auto queues/consumers
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
            //! Multiple routing keys can't be added to the same queue, multiple routes work only with wildcards
            .RoutingKey(WellKnown.RoutingKeys.All)
            // .RoutingKey(WellKnown.RoutingKeys.OrderPlaced)
            // .RoutingKey(WellKnown.RoutingKeys.OrderCancelled)
            .AutoProvision(true);

        //! Only if this is set, there is a consumer on the queue
        t.Endpoint("my-endpoint")
            .Handler<OrderPlacedHandler>()
            .Handler<OrderCancelledHandler>()
            .Queue(WellKnown.Queues.OrderServiceQueue);

        // ── OrderSaga: triggered by StartOrderSagaCommand from the Api ──────────
        t.DeclareExchange(WellKnown.Exchanges.Commands)
            .Type(RabbitMQExchangeType.Direct)
            .Durable()
            .AutoProvision(true);

        t.DeclareQueue(WellKnown.Queues.OrderSagaQueue)
            .Durable()
            .WithArgument("x-queue-type", RabbitMQQueueType.Quorum)
            .AutoProvision(true);

        t.DeclareBinding(WellKnown.Exchanges.Commands, WellKnown.Queues.OrderSagaQueue)
            .RoutingKey(WellKnown.RoutingKeys.StartOrderSaga)
            .AutoProvision(true);

        t.DeclareBinding(WellKnown.Exchanges.Events, WellKnown.Queues.OrderSagaQueue)
            .RoutingKey(WellKnown.RoutingKeys.GetStockInfoResult)
            .AutoProvision(true);

        // The saga consumer identity is the saga type itself.
        t.Endpoint("order-saga-endpoint")
            .Consumer(typeof(OrderSaga))
            //.Receives<GetStockInfoResult>()
            .Queue(WellKnown.Queues.OrderSagaQueue);

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

        t.DeclareBinding(WellKnown.Exchanges.Rpc, WellKnown.Queues.OrderSagaQueue)
            .RoutingKey(WellKnown.RoutingKeys.GetStockInfoResult)
            .AutoProvision(true);

        t.Endpoint("stock-endpoint")
            .Handler<GetStockInfoRequestHandler>()
            .Queue(WellKnown.Queues.StockQueue);

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
        //     .ToQueue(WellKnown.Queues.OrderSagaQueue)
        //     .Send<GetStockInfoResult>();
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
        //d.Publish(r => r.ToExchange(WellKnown.Exchanges.Rpc));
    });
;

// Saga state persistence (development/in-memory).
builder.Services.AddInMemorySagas();

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new { Service = "OrderService", Status = "Running" }));

app.Run();
