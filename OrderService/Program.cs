using Contracts;
using Mocha;
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

        t.DeclareQueue(WellKnown.Queues.OrderServiceEvents)
            .Durable()
            .WithArgument("x-queue-type", RabbitMQQueueType.Quorum)
            .AutoProvision(true);

        t.DeclareBinding(WellKnown.Exchanges.Events, WellKnown.Queues.OrderServiceEvents)
            //! Multiple routing keys can't be added to the same queue, multiple routes work only with wildcards
            .RoutingKey(WellKnown.RoutingKeys.All)
            // .RoutingKey(WellKnown.RoutingKeys.OrderPlaced)
            // .RoutingKey(WellKnown.RoutingKeys.OrderCancelled)
            .AutoProvision(true);

        //! Only if this is set, there is a consumer on the queue
        t.Endpoint("my-endpoint")
            .Handler<OrderPlacedHandler>()
            .Handler<OrderCancelledHandler>()
            .Queue(WellKnown.Queues.OrderServiceEvents);
    });

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new { Service = "OrderService", Status = "Running" }));

app.Run();
