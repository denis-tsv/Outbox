using System.Diagnostics;
using Confluent.Kafka;
using EFCore.MigrationExtensions.PostgreSQL;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Outbox;
using Outbox.Configurations;
using Outbox.Entities;
using Outbox.WebApi.BackgroundServices;
using Outbox.WebApi.EFCore;
using Outbox.WebApi.Telemetry;

var builder = WebApplication.CreateBuilder(args);

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddDbContextPool<AppDbContext>((serviceProvider, optionsBuilder) =>
{
    var dataSource = new NpgsqlDataSourceBuilder(builder.Configuration.GetConnectionString("Outbox"))
        .EnableDynamicJson()
        .Build();
    optionsBuilder
        .UseNpgsql(
            dataSource,
            options => options.MigrationsHistoryTable("_migrations", "outbox"))
        .UseSnakeCaseNamingConvention()
        .AddInterceptors(new ForUpdateInterceptor(), serviceProvider.GetRequiredService<OutboxInterceptor>());
    
    optionsBuilder.UseSqlObjects();
});

builder.Services.AddKafkaClient();

builder.Services.Configure<OutboxConfiguration>(builder.Configuration.GetSection("Outbox"));
builder.Services.AddSingleton<OutboxInterceptor>();

builder.Services.AddSingleton<OutboxBackgroundService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<OutboxBackgroundService>());
builder.Services.AddSingleton<IOutboxMessagesProcessor>(sp => sp.GetRequiredService<OutboxBackgroundService>());

builder.Services.AddOpenTelemetry()
    .WithTracing();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.Services.CreateScope().ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
}

app.UseHttpsRedirection();

app.MapPost("/messages", async (CreateMessageDto dto, AppDbContext dbContext, CancellationToken ct) =>
    {
        var activityContext = Activity.Current?.Context;
        var message = new OutboxMessage
        {
            Topic = dto.Topic,
            Partition = 0, //hash key % partitions count, ...
            Type = dto.Type,
            Key = dto.Key,
            Payload = dto.Payload,
            Headers = activityContext.GetHeaders()
        };
        
        dbContext.OutboxMessages.Add(message);

        await dbContext.SaveChangesAsync(ct);
    })
    .WithName("CreateMessage");

var producer = app.Services.GetRequiredService<IProducer<Null, string>>();
producer.InitTransactions(TimeSpan.FromSeconds(10));

app.Run();

public record CreateMessageDto(
    string Topic, 
    string Type,
    string? Key,
    string Payload);