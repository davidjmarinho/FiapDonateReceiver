using FiapDonateReceiver.Infrastructure;
using FiapDonateReceiver.Worker.Consumers;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Prometheus;
using RabbitMQ.Client;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("ReceiverDb")
    ?? throw new InvalidOperationException("ConnectionStrings:ReceiverDb nao configurada.");

var rabbitHost = builder.Configuration["RabbitMq:Host"] ?? "localhost";
var rabbitVirtualHost = builder.Configuration["RabbitMq:VirtualHost"] ?? "/";
var rabbitUsername = builder.Configuration["RabbitMq:Username"] ?? "guest";
var rabbitPassword = builder.Configuration["RabbitMq:Password"] ?? "guest";
var rabbitUri = $"amqp://{rabbitUsername}:{rabbitPassword}@{rabbitHost}{rabbitVirtualHost}";

builder.Services.AddDbContext<ReceiverDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddScoped<DoacaoRepository>();

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<DoacaoRecebidaConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(rabbitHost, rabbitVirtualHost, h =>
        {
            h.Username(rabbitUsername);
            h.Password(rabbitPassword);
        });

        cfg.ReceiveEndpoint("doacao-recebida-queue", e =>
        {
            e.ConfigureConsumer<DoacaoRecebidaConsumer>(context);
            e.UseMessageRetry(r => r.Immediate(3));
        });
    });
});

// Conexao dedicada ao health check (a conexao interna do MassTransit nao e
// exposta via DI), conforme recomendado pelo README do AspNetCore.HealthChecks.Rabbitmq.
builder.Services.AddSingleton<IConnection>(sp =>
{
    var factory = new ConnectionFactory { Uri = new Uri(rabbitUri) };
    return factory.CreateConnectionAsync().GetAwaiter().GetResult();
});

builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString, name: "postgresql")
    .AddRabbitMQ(name: "rabbitmq");

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new { service = "FiapDonateReceiver.Worker", status = "running" }));
app.MapHealthChecks("/health");
app.UseHttpMetrics();
app.MapMetrics();

app.Run();
