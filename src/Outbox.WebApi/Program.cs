using System.Diagnostics;
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
using Outbox.WebApi.Offset;
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

builder.Services.AddScoped<IOutboxOffsetMessageContext, OutboxOffsetMessageContext>();

LinqToDBForEFTools.Implementation = new OutboxLinqToDBForEFToolsImpl(builder.Configuration.GetConnectionString("Outbox")!);
LinqToDBForEFTools.Initialize();

builder.Services.Configure<OutboxConfiguration>(builder.Configuration.GetSection("Outbox"));

builder.Services.AddSingleton<OutboxInterceptor>();

builder.Services.AddSingleton<OneLongTransactionBackgroundService>();
builder.Services.AddSingleton<OneLongTransactionPartitionBackgroundService>();
builder.Services.AddSingleton<TwoShortTransactionsUpdatableBackgroundService>();
builder.Services.AddSingleton<TwoShortTransactionsAppendOnlyBackgroundService>();
builder.Services.AddSingleton<OutboxOffsetBackgroundService>();

builder.Services.AddHostedService(sp => sp.GetRequiredService<OutboxOffsetBackgroundService>());
//builder.Services.AddHostedService(sp => sp.GetRequiredService<TwoShortTransactionsAppendOnlyBackgroundService>());
//builder.Services.AddHostedService(sp => sp.GetRequiredService<TwoShortTransactionsUpdatableBackgroundService>());
//builder.Services.AddHostedService(sp => sp.GetRequiredService<OneLongTransactionPartitionBackgroundService>());
//builder.Services.AddHostedService(sp => sp.GetRequiredService<OneLongTransactionBackgroundService>());

builder.Services.AddSingleton<IOutboxMessagesProcessor>(sp => sp.GetRequiredService<OneLongTransactionPartitionBackgroundService>());

builder.Services.AddOpenTelemetry()
    .WithTracing(bld =>
    {
        bld.AddSource(ActivitySources.OutboxSource);
    });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.Services.CreateScope().ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
}

app.UseHttpsRedirection();

app.MapPost("/partitions", async (CreatePartitionDto dto, AppDbContext dbContext, CancellationToken ct) =>
    {
        dbContext.VirtualPartitions.Add(new VirtualPartition
        {
            Topic = dto.Topic,
            Partition = dto.Partition
        });
        await dbContext.SaveChangesAsync(ct);
    })
    .WithName("CreatePartition");

app.MapPost("/messages", async (CreateMessageDto dto, AppDbContext dbContext, CancellationToken ct) =>
    {
        var activityContext = Activity.Current?.Context;
        var message = new OutboxMessage
        {
            Topic = dto.Topic,
            Partition = 0, //hash payload
            Type = dto.Type,
            Key = dto.Key,
            Payload = dto.Payload,
            Headers = activityContext.GetHeaders()
        };
        
        dbContext.OutboxMessages.Add(message);

        await dbContext.SaveChangesAsync(ct);
    })
    .WithName("CreateMessage");

app.MapPost("/offset-messages", async (CreateMessageDto dto, AppDbContext dbContext, IOutboxOffsetMessageContext outboxContext, CancellationToken ct) =>
    {
        var activityContext = Activity.Current?.Context;

        await using var trx = await dbContext.Database.BeginTransactionAsync(ct);
        
        await dbContext.SaveChangesAsync(ct); //save entities

        var message = new OutboxOffsetMessage
        {
            Topic = "offset",
            Partition = 0,
            Type = dto.Type,
            Key = dto.Key,
            Payload = dto.Payload,
            Headers = activityContext.GetHeaders()
        };
        
        outboxContext.AddMessage(message);
        await outboxContext.SaveChangesAsync(trx.GetDbTransaction(), ct);
        
        await trx.CommitAsync(ct);
    })
    .WithName("CreateOffsetMessage");

app.Run();

public record CreateMessageDto(
    string Topic, 
    string Type,
    string? Key,
    string Payload);
public record CreatePartitionDto(string Topic, int Partition);