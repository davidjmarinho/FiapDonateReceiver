using FiapDonateReceiver.Infrastructure;
using FiapDonateReceiver.Worker.Consumers;
using MassTransit;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("ReceiverDb")
    ?? throw new InvalidOperationException("ConnectionStrings:ReceiverDb nao configurada.");

var rabbitHost = builder.Configuration["RabbitMq:Host"] ?? "localhost";
var rabbitVirtualHost = builder.Configuration["RabbitMq:VirtualHost"] ?? "/";
var rabbitUsername = builder.Configuration["RabbitMq:Username"] ?? "guest";
var rabbitPassword = builder.Configuration["RabbitMq:Password"] ?? "guest";

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

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new { service = "FiapDonateReceiver.Worker", status = "running" }));

app.Run();
