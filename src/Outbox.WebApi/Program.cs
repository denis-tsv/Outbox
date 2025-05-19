using System.Diagnostics;
using Confluent.Kafka;
using EFCore.MigrationExtensions.PostgreSQL;
using LinqToDB.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Outbox;
using Outbox.Configurations;
using Outbox.Entities;
using Outbox.WebApi.BackgroundServices;
using Outbox.WebApi.EFCore;
using Outbox.WebApi.Linq2db;
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
        .AddInterceptors(new ForUpdateInterceptor());
    
    optionsBuilder.UseSqlObjects();
});

builder.Services.AddScoped<IOutboxMessageContext, OutboxMessageContext>();

LinqToDBForEFTools.Implementation = new OutboxLinqToDBForEFToolsImpl(builder.Configuration.GetConnectionString("Outbox")!);
LinqToDBForEFTools.Initialize();

builder.Services.AddKafkaClient();

builder.Services.Configure<OutboxConfiguration>(builder.Configuration.GetSection("Outbox"));

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

app.MapPost("/messages", async (CreateMessageDto dto, AppDbContext dbContext, IOutboxMessageContext outboxMessageContext, CancellationToken ct) =>
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
        outboxMessageContext.Add(message);

        var transaction = await dbContext.Database.BeginTransactionAsync(ct);

        await dbContext.SaveChangesAsync(ct); //save business entities

        await outboxMessageContext.SaveChangesAsync(transaction.GetDbTransaction(), ct);
        
        await transaction.CommitAsync(ct);
    })
    .WithName("CreateMessage");

app.Run();

public record CreateMessageDto(
    string Topic, 
    string Type,
    string? Key,
    string Payload);