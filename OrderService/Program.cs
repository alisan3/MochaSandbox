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
            .RoutingKey(WellKnown.RoutingKeys.OrderPlaced)
            .AutoProvision(true);

        //! Only if this is set, there is a consumer on the queue
        t.Endpoint("my-endpoint")
            .Handler<OrderPlacedHandler>()
            .Queue(WellKnown.Queues.OrderServiceEvents);
    })
    .AddMessage<OrderPlaced>(d =>
    {
        d.Extend().Configuration.Identity = typeof(OrderPlaced).FullName!;
        d.UseRabbitMQRoutingKey<OrderPlaced>(_ => WellKnown.RoutingKeys.OrderPlaced);
    });
;

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new { Service = "OrderService", Status = "Running" }));

app.Run();
