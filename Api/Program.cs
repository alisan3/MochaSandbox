using Contracts;
using Mocha;
using Mocha.Transport.RabbitMQ;

var builder = WebApplication.CreateBuilder(args);

builder.AddRabbitMQClient("rabbitmq");

builder
    .Services.AddMessageBus()
    .AddRabbitMQ(t =>
    {
        t.DeclareExchange(WellKnown.Exchanges.Events).Type(RabbitMQExchangeType.Topic).Durable();
    })
    .AddMessage<OrderPlaced>(d =>
    {
        d.Extend().Configuration.Identity = typeof(OrderPlaced).FullName!;
        d.UseRabbitMQRoutingKey<OrderPlaced>(_ => WellKnown.RoutingKeys.OrderPlaced);
        d.Publish(r => r.ToExchange(WellKnown.Exchanges.Events));
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

app.Run();
