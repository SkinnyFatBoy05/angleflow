using AngleFlow.Infrastructure.Checks;
using AngleFlow.Infrastructure.Persistence;
using AngleFlow.Infrastructure.Services;
using AngleFlow.Worker.Workers;
using Camunda.Orchestration.Sdk;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.AddJsonConsole();

var connectionString = builder.Configuration.GetConnectionString("AngleFlow")
    ?? "Host=localhost;Port=5432;Database=angleflow;Username=angleflow;Password=angleflow";

builder.Services.AddDbContext<AngleFlowDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ICheckEngine, DeterministicCheckEngine>();
builder.Services.AddScoped<WorkerStateService>();
builder.Services.AddScoped<ExternalOperationService>();
builder.Services.AddScoped<CheckWorkerHandlers>();
builder.Services.AddScoped<OutcomeWorkerHandlers>();
builder.Services.AddSingleton(_ => CamundaClient.Create());
builder.Services.AddHostedService<CamundaWorkerHost>();

using var host = builder.Build();
await using (var scope = host.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AngleFlowDbContext>();
    await db.Database.EnsureCreatedAsync();
}

await host.RunAsync();
