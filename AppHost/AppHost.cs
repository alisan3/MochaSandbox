var builder = DistributedApplication.CreateBuilder(args);

var rabbitMqUserName = builder.AddParameter(
    "rabbitmq-username",
    "guest",
    publishValueAsDefault: true
);
var rabbitMqPassword = builder.AddParameter(
    "rabbitmq-password",
    "guest",
    publishValueAsDefault: true
);

var rabbitmq = builder
    .AddRabbitMQ("rabbitmq", rabbitMqUserName, rabbitMqPassword, port: 5672)
    .WithManagementPlugin(port: 15672)
    .WithLifetime(ContainerLifetime.Persistent);

builder.AddProject<Projects.Api>("api").WithReference(rabbitmq).WaitFor(rabbitmq);

builder.AddProject<Projects.OrderService>("orderservice").WithReference(rabbitmq).WaitFor(rabbitmq);

builder.Build().Run();
