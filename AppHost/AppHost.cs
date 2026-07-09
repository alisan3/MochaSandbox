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

var postgresPassword = builder.AddParameter(
    "postgres-password",
    "postgres",
    publishValueAsDefault: true
);

var postgres = builder
    .AddPostgres("postgres", password: postgresPassword, port: 6432)
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);

var sagaDb = postgres.AddDatabase("sagadb");

builder.AddProject<Projects.Api>("api").WithReference(rabbitmq).WaitFor(rabbitmq);

builder
    .AddProject<Projects.OrderService>("orderservice")
    .WithReference(rabbitmq)
    .WaitFor(rabbitmq)
    .WithReference(sagaDb)
    .WaitFor(sagaDb);

builder.Build().Run();
