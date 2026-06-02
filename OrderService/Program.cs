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
        t.DeclareExchange(WellKnown.Exchanges.Events).Type(RabbitMQExchangeType.Topic).Durable();
        t.DeclareQueue(WellKnown.Queues.OrderServiceEvents)
            .Durable()
            .WithArgument("x-queue-type", RabbitMQQueueType.Quorum);
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
    })
    .AddMessage<OrderPlaced>(d =>
    {
        d.UseRabbitMQRoutingKey<OrderPlaced>(_ => WellKnown.RoutingKeys.OrderPlaced);
    })
    .AddMessage<OrderCancelled>(d =>
    {
        d.UseRabbitMQRoutingKey<OrderCancelled>(_ => WellKnown.RoutingKeys.OrderCancelled);
    });

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new { Service = "OrderService", Status = "Running" }));

app.Run();
