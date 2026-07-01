using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Weaver.Domain;
using Weaver.Infrastructure.Persistence;
using Weaver.Infrastructure.Realtime;
using Weaver.Workflows;
using Weaver.Workflows.Nodes;

namespace Weaver.Tests.Support;

/// <summary>Wires a real WorkflowExecutionEngine against an EF Core InMemory database and whatever
/// fake node handlers a test supplies, so the dataflow scheduler (merge/branch/retry/error-edge
/// routing) can be exercised without Postgres, Redis, or real node side effects.</summary>
public class WorkflowEngineHarness
{
    public IWorkflowExecutionEngine Engine { get; }
    private readonly IServiceProvider _provider;

    public WorkflowEngineHarness(params INodeHandler[] handlers)
    {
        var databaseName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddDbContext<WeaverDbContext>(o => o.UseInMemoryDatabase(databaseName));
        _provider = services.BuildServiceProvider();

        // The real trigger handlers are trivial passthroughs with no external dependencies, and
        // every test workflow needs one registered for its entry node -- same as production DI.
        var registry = new NodeHandlerRegistry(handlers.Append(new ManualTriggerNode()));
        Engine = new WorkflowExecutionEngine(
            new ScopeFactoryWithProtector(_provider),
            registry,
            NullLogger<WorkflowExecutionEngine>.Instance,
            new NoOpRunStatusPublisher());
    }

    private class NoOpRunStatusPublisher : IRunStatusPublisher
    {
        public Task PublishAsync(Guid ownerUserId, string kind, Guid runId, string status, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    public void Seed(Action<WeaverDbContext> seed)
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WeaverDbContext>();
        seed(db);
        db.SaveChanges();
    }

    public T Query<T>(Func<WeaverDbContext, T> query)
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WeaverDbContext>();
        return query(db);
    }

    /// <summary>
    /// The engine resolves WeaverDbContext (and ISensitiveConfigProtector) from the scope it
    /// creates internally via IServiceScopeFactory, so this wraps the DI container's real scope
    /// factory just to also make the passthrough protector available without requiring the whole
    /// Data Protection stack in every scheduling test.
    /// </summary>
    private class ScopeFactoryWithProtector : IServiceScopeFactory
    {
        private readonly IServiceProvider _root;
        public ScopeFactoryWithProtector(IServiceProvider root) => _root = root;

        public IServiceScope CreateScope()
        {
            var inner = _root.CreateScope();
            return new ScopeWithProtector(inner);
        }

        private class ScopeWithProtector : IServiceScope
        {
            private readonly IServiceScope _inner;
            private readonly PassthroughSensitiveConfigProtector _protector = new();

            public ScopeWithProtector(IServiceScope inner) => _inner = inner;

            public IServiceProvider ServiceProvider => new ProviderWithProtector(_inner.ServiceProvider, _protector);

            public void Dispose() => _inner.Dispose();
        }

        private class ProviderWithProtector : IServiceProvider
        {
            private readonly IServiceProvider _inner;
            private readonly PassthroughSensitiveConfigProtector _protector;

            public ProviderWithProtector(IServiceProvider inner, PassthroughSensitiveConfigProtector protector)
            {
                _inner = inner;
                _protector = protector;
            }

            public object? GetService(Type serviceType) =>
                serviceType == typeof(Weaver.Infrastructure.Security.ISensitiveConfigProtector)
                    ? _protector
                    : _inner.GetService(serviceType);
        }
    }
}
