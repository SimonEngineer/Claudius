using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Orchestrator.Api.Auth;
using Xunit;

namespace Orchestrator.Tests.Auth;

public class SharedSecretAuthMiddlewareTests
{
    private static DefaultHttpContext MakeContext(string path = "/api/tasks")
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        return context;
    }

    [Fact]
    public async Task NoSecretConfigured_AllowsRequestThrough()
    {
        var called = false;
        RequestDelegate next = _ => { called = true; return Task.CompletedTask; };
        var config = new ConfigurationBuilder().Build();
        var middleware = new SharedSecretAuthMiddleware(next, config);

        await middleware.InvokeAsync(MakeContext());

        Assert.True(called);
    }

    [Fact]
    public async Task SecretConfigured_RejectsRequestWithoutCredential()
    {
        var called = false;
        RequestDelegate next = _ => { called = true; return Task.CompletedTask; };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection([new KeyValuePair<string, string?>("Auth:SharedSecret", "s3cr3t")])
            .Build();
        var middleware = new SharedSecretAuthMiddleware(next, config);
        var context = MakeContext();

        await middleware.InvokeAsync(context);

        Assert.False(called);
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task SecretConfigured_AllowsRequestWithMatchingApiKeyHeader()
    {
        var called = false;
        RequestDelegate next = _ => { called = true; return Task.CompletedTask; };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection([new KeyValuePair<string, string?>("Auth:SharedSecret", "s3cr3t")])
            .Build();
        var middleware = new SharedSecretAuthMiddleware(next, config);
        var context = MakeContext();
        context.Request.Headers["X-Api-Key"] = "s3cr3t";

        await middleware.InvokeAsync(context);

        Assert.True(called);
    }

    [Fact]
    public async Task SecretConfigured_AllowsRequestWithMatchingBearerToken()
    {
        var called = false;
        RequestDelegate next = _ => { called = true; return Task.CompletedTask; };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection([new KeyValuePair<string, string?>("Auth:SharedSecret", "s3cr3t")])
            .Build();
        var middleware = new SharedSecretAuthMiddleware(next, config);
        var context = MakeContext();
        context.Request.Headers["Authorization"] = "Bearer s3cr3t";

        await middleware.InvokeAsync(context);

        Assert.True(called);
    }

    [Fact]
    public async Task SecretConfigured_AllowsRequestWithMatchingAccessTokenQueryParam()
    {
        var called = false;
        RequestDelegate next = _ => { called = true; return Task.CompletedTask; };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection([new KeyValuePair<string, string?>("Auth:SharedSecret", "s3cr3t")])
            .Build();
        var middleware = new SharedSecretAuthMiddleware(next, config);
        var context = MakeContext("/hubs/tasks/negotiate");
        context.Request.QueryString = new QueryString("?access_token=s3cr3t");

        await middleware.InvokeAsync(context);

        Assert.True(called);
    }

    [Theory]
    [InlineData("/api/health")]
    [InlineData("/swagger/index.html")]
    [InlineData("/hangfire")]
    public async Task SecretConfigured_StillExemptsHealthSwaggerAndHangfire(string path)
    {
        var called = false;
        RequestDelegate next = _ => { called = true; return Task.CompletedTask; };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection([new KeyValuePair<string, string?>("Auth:SharedSecret", "s3cr3t")])
            .Build();
        var middleware = new SharedSecretAuthMiddleware(next, config);

        await middleware.InvokeAsync(MakeContext(path));

        Assert.True(called);
    }
}
