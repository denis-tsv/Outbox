using System.Diagnostics;
using LinqToDB.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Outbox;
using Outbox.Configurations;
using Outbox.WebApi.BackgroundServices;
using Outbox.WebApi.EFCore;
using Outbox.WebApi.Linq2db;
using Outbox.WebApi.Telemetry;

var builder = WebApplication.CreateBuilder(args);

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddDbContext<AppDbContext>((serviceProvider, optionsBuilder) =>
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
});

LinqToDBForEFTools.Implementation = new OutboxLinqToDBForEFToolsImpl();
LinqToDBForEFTools.Initialize();

builder.Services.Configure<OutboxConfiguration>(builder.Configuration.GetSection("Outbox"));

builder.Services.AddSingleton<OutboxInterceptor>();

builder.Services.AddSingleton<Linq2DbOutboxBackgroundService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<Linq2DbOutboxBackgroundService>());
//builder.Services.AddHostedService<EFOutboxBackgroundService>();

builder.Services.AddSingleton<IOutboxMessagesProcessor>(sp => sp.GetRequiredService<Linq2DbOutboxBackgroundService>());

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

app.Run();

public record CreateMessageDto(
    string Topic, 
    string Type,
    string? Key,
    string Payload);
public record CreatePartitionDto(string Topic, int Partition);