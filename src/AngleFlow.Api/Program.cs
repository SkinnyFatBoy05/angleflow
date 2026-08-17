using System.Text.Json.Serialization;
using AngleFlow.Api.Endpoints;
using AngleFlow.Api.Middleware;
using AngleFlow.Infrastructure.Persistence;
using AngleFlow.Infrastructure.Services;
using AngleFlow.Infrastructure.Workflow;
using Camunda.Orchestration.Sdk;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins(builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? ["http://localhost:5173"])
        .AllowAnyHeader()
        .AllowAnyMethod()));

var connectionString = builder.Configuration.GetConnectionString("AngleFlow")
    ?? "Host=localhost;Port=5432;Database=angleflow;Username=angleflow;Password=angleflow_dev";
builder.Services.AddDbContext<AngleFlowDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(_ => CamundaClient.Create());
builder.Services.AddSingleton<ICamundaOperationsClient, SdkCamundaOperationsClient>();
builder.Services.AddScoped<IWorkflowGateway, CamundaWorkflowGateway>();
builder.Services.AddScoped<IApplicationService, ApplicationService>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseCors();
app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/health/live", () => Results.Ok(new { status = "Healthy" }));
app.MapGet("/health/ready", () => Results.Ok(new { status = "Ready" }));
app.MapApplicationEndpoints();

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AngleFlowDbContext>();
    await db.Database.EnsureCreatedAsync();
}

app.Run();

public partial class Program;
