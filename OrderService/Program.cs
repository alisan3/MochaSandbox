using Contracts;
using Microsoft.EntityFrameworkCore;
using Mocha;
using Mocha.EntityFrameworkCore;
using Mocha.Sagas;
using Mocha.Transport.RabbitMQ;
using OrderService;

var builder = WebApplication.CreateBuilder(args);

builder.AddRabbitMQClient("rabbitmq");
builder.Services.AddDbContext<OrderSagaDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("sagadb"))
);

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
            .Handler<GetPriceInfoRequestHandler>()
            .Handler<GetShippingInfoRequestHandler>()
            .Handler<GetTaxInfoRequestHandler>()
            .Handler<GetDiscountInfoRequestHandler>();

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

        // Shipping / Tax / Discount requests -> OrderServiceQueue (handlers),
        // their results -> OrderSagaQueue (saga OnSend transitions).
        t.DeclareBinding(WellKnown.Exchanges.Events, WellKnown.Queues.OrderServiceQueue)
            .RoutingKey(WellKnown.RoutingKeys.GetShippingInfoRequest)
            .AutoProvision(true);

        t.DeclareBinding(WellKnown.Exchanges.Events, WellKnown.Queues.OrderSagaQueue)
            .RoutingKey(WellKnown.RoutingKeys.GetShippingInfoResult)
            .AutoProvision(true);

        t.DeclareBinding(WellKnown.Exchanges.Events, WellKnown.Queues.OrderServiceQueue)
            .RoutingKey(WellKnown.RoutingKeys.GetTaxInfoRequest)
            .AutoProvision(true);

        t.DeclareBinding(WellKnown.Exchanges.Events, WellKnown.Queues.OrderSagaQueue)
            .RoutingKey(WellKnown.RoutingKeys.GetTaxInfoResult)
            .AutoProvision(true);

        t.DeclareBinding(WellKnown.Exchanges.Events, WellKnown.Queues.OrderServiceQueue)
            .RoutingKey(WellKnown.RoutingKeys.GetDiscountInfoRequest)
            .AutoProvision(true);

        t.DeclareBinding(WellKnown.Exchanges.Events, WellKnown.Queues.OrderSagaQueue)
            .RoutingKey(WellKnown.RoutingKeys.GetDiscountInfoResult)
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
    })
    .AddEntityFramework<OrderSagaDbContext>(p =>
    {
        p.UseTransaction();
        p.AddSagaCore();
    })
    .AddMessage<StartOrderSagaCommand>(d =>
    {
        // Register the command type so the inbound URN
        // (urn:message:contracts:start-order-saga-command) resolves to the CLR
        // type and the saga's OnSend<StartOrderSagaCommand> route matches.
        d.UseRabbitMQRoutingKey<StartOrderSagaCommand>(_ => WellKnown.RoutingKeys.StartOrderSaga);
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
    .AddMessage<GetShippingInfoRequest>(d =>
    {
        d.UseRabbitMQRoutingKey<GetShippingInfoRequest>(_ =>
            WellKnown.RoutingKeys.GetShippingInfoRequest
        );
        d.Send(r => r.ToExchange(WellKnown.Exchanges.Events));
    })
    .AddMessage<GetShippingInfoResult>(d =>
    {
        d.UseRabbitMQRoutingKey<GetShippingInfoResult>(_ =>
            WellKnown.RoutingKeys.GetShippingInfoResult
        );
        d.Send(r => r.ToExchange(WellKnown.Exchanges.Events));
    })
    .AddMessage<GetTaxInfoRequest>(d =>
    {
        d.UseRabbitMQRoutingKey<GetTaxInfoRequest>(_ => WellKnown.RoutingKeys.GetTaxInfoRequest);
        d.Send(r => r.ToExchange(WellKnown.Exchanges.Events));
    })
    .AddMessage<GetTaxInfoResult>(d =>
    {
        d.UseRabbitMQRoutingKey<GetTaxInfoResult>(_ => WellKnown.RoutingKeys.GetTaxInfoResult);
        d.Send(r => r.ToExchange(WellKnown.Exchanges.Events));
    })
    .AddMessage<GetDiscountInfoRequest>(d =>
    {
        d.UseRabbitMQRoutingKey<GetDiscountInfoRequest>(_ =>
            WellKnown.RoutingKeys.GetDiscountInfoRequest
        );
        d.Send(r => r.ToExchange(WellKnown.Exchanges.Events));
    })
    .AddMessage<GetDiscountInfoResult>(d =>
    {
        d.UseRabbitMQRoutingKey<GetDiscountInfoResult>(_ =>
            WellKnown.RoutingKeys.GetDiscountInfoResult
        );
        d.Send(r => r.ToExchange(WellKnown.Exchanges.Events));
    })
    .AddMessage<OrderReadyToComplete>(d =>
    {
        d.UseRabbitMQRoutingKey<OrderReadyToComplete>(_ =>
            WellKnown.RoutingKeys.OrderReadyToComplete
        );
        d.Send(r => r.ToExchange(WellKnown.Exchanges.Events));
    });

// Saga state persistence: EF Core + Postgres (see AddEntityFramework above).

var app = builder.Build();

// Apply saga schema migrations at startup.
using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<OrderSagaDbContext>().Database.MigrateAsync();
}

app.MapGet("/", () => Results.Ok(new { Service = "OrderService", Status = "Running" }));

app.Run();
