using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AngleFlow.Domain.Applications;
using AngleFlow.Infrastructure.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using DomainApplicationId = AngleFlow.Domain.Applications.ApplicationId;

namespace AngleFlow.Api.Tests;

public sealed class ApplicationApiTests : IClassFixture<AngleFlowApiFactory>
{
    private readonly HttpClient _client;

    public ApplicationApiTests(AngleFlowApiFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Post_returns_202_location_and_public_application()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/applications");
        request.Headers.Add("Idempotency-Key", "api-demo-001");
        request.Content = JsonContent.Create(TestRequest);

        var response = await _client.SendAsync(request);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal("/api/applications/11111111-1111-1111-1111-111111111111", response.Headers.Location?.OriginalString);
        Assert.Equal("ChecksRunning", payload.GetProperty("status").GetString());
        Assert.False(payload.TryGetProperty("applicantName", out _));
    }

    [Fact]
    public async Task Post_requires_idempotency_key_as_problem_details()
    {
        var response = await _client.PostAsJsonAsync("/api/applications", TestRequest);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("IDEMPOTENCY_KEY_REQUIRED", payload.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Post_maps_idempotency_conflict_to_409()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/applications");
        request.Headers.Add("Idempotency-Key", "conflict");
        request.Content = JsonContent.Create(TestRequest);

        var response = await _client.SendAsync(request);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("IDEMPOTENCY_KEY_REUSED", payload.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Get_returns_404_problem_for_unknown_application()
    {
        var response = await _client.GetAsync("/api/applications/22222222-2222-2222-2222-222222222222");
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("APPLICATION_NOT_FOUND", payload.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Timeline_is_ordered_and_audit_safe()
    {
        var response = await _client.GetAsync("/api/applications/11111111-1111-1111-1111-111111111111/timeline");
        var payload = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new long[] { 1, 2 }, payload!.Select(item => item.GetProperty("sequence").GetInt64()));
        Assert.All(payload!, item => Assert.False(item.TryGetProperty("applicantName", out _)));
    }

    [Fact]
    public async Task List_returns_paginated_public_views()
    {
        var response = await _client.GetAsync("/api/applications?page=1&pageSize=20&status=ChecksRunning");
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, payload.GetProperty("total").GetInt32());
        Assert.Single(payload.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task Withdraw_returns_updated_public_status()
    {
        var response = await _client.PostAsync(
            "/api/applications/11111111-1111-1111-1111-111111111111/withdraw",
            null);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Withdrawn", payload.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Correlation_header_is_preserved_when_safe()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add("X-Correlation-ID", "portfolio-demo-001");

        var response = await _client.SendAsync(request);

        Assert.Equal("portfolio-demo-001", response.Headers.GetValues("X-Correlation-ID").Single());
    }

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task Health_endpoints_are_available(string path)
    {
        var response = await _client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static readonly object TestRequest = new
    {
        applicantName = "Taylor Morgan",
        email = "taylor@example.test",
        annualIncome = 120_000m,
        monthlyExpenses = 3_500m,
        assetType = "vehicle",
        assetValue = 65_000m,
        requestedAmount = 45_000m,
        termMonths = 48,
        scenario = "auto-approve"
    };
}

public sealed class AngleFlowApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IApplicationService>();
            services.AddSingleton<IApplicationService, ApiTestApplicationService>();
        });
    }
}

internal sealed class ApiTestApplicationService : IApplicationService
{
    private static readonly DomainApplicationId KnownId = new(Guid.Parse("11111111-1111-1111-1111-111111111111"));
    private static readonly DateTimeOffset Now = new(2026, 8, 17, 1, 0, 0, TimeSpan.Zero);
    private static readonly ApplicationView Known = new(
        KnownId,
        ApplicationStatus.ChecksRunning,
        "vehicle",
        65_000m,
        45_000m,
        48,
        "auto-approve",
        "process-001",
        Now,
        Now.AddSeconds(1));

    public Task<CreateApplicationResult> CreateAsync(
        CreateApplicationCommand command,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (idempotencyKey == "conflict")
        {
            throw new IdempotencyConflictException();
        }

        return Task.FromResult(new CreateApplicationResult(Known, false));
    }

    public Task<ApplicationView?> GetAsync(DomainApplicationId id, CancellationToken cancellationToken) =>
        Task.FromResult<ApplicationView?>(id == KnownId ? Known : null);

    public Task<IReadOnlyList<TimelineEventView>> GetTimelineAsync(DomainApplicationId id, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<TimelineEventView>>(
        [
            new(1, "APPLICATION_SUBMITTED", ApplicationStatus.Submitted, "SUBMITTED", Now),
            new(2, "CHECKS_STARTED", ApplicationStatus.ChecksRunning, "WORKFLOW_STARTED", Now.AddSeconds(1))
        ]);

    public Task<PagedResult<ApplicationView>> ListAsync(
        int page,
        int pageSize,
        ApplicationStatus? status,
        CancellationToken cancellationToken) =>
        Task.FromResult(new PagedResult<ApplicationView>([Known], 1, pageSize, 1));

    public Task<ApplicationView> WithdrawAsync(DomainApplicationId id, CancellationToken cancellationToken) =>
        Task.FromResult(Known with { Status = ApplicationStatus.Withdrawn });
}
