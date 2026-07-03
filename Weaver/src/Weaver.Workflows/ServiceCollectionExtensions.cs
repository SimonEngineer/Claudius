using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Weaver.Scraping;
using Weaver.Workflows.Nodes;
using Weaver.Workflows.Scripting;

namespace Weaver.Workflows;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWeaverWorkflows(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SmtpOptions>(configuration.GetSection("Smtp"));
        services.AddHttpClient("discord-webhook");
        services.AddHttpClient("slack-webhook");
        services.AddHttpClient("http-request-node");
        services.AddHttpClient("failure-notification");

        services.AddScoped<IScriptDbAccess, ScriptDbAccess>();
        services.AddScoped<Weaver.Infrastructure.Realtime.IFailureNotifier, FailureNotifier>();
        services.AddSingleton<INodeHandlerRegistry, NodeHandlerRegistry>();
        services.AddSingleton<IWorkflowExecutionEngine, WorkflowExecutionEngine>();
        services.AddSingleton<IWorkflowEventPublisher, WorkflowEventBus>();

        services.AddSingleton<INodeHandler, CronTriggerNode>();
        services.AddSingleton<INodeHandler, HttpTriggerNode>();
        services.AddSingleton<INodeHandler, EventTriggerNode>();
        services.AddSingleton<INodeHandler, ManualTriggerNode>();
        services.AddSingleton<INodeHandler, ConditionNode>();
        services.AddSingleton<INodeHandler, ScrapeActionNode>();
        services.AddSingleton<INodeHandler, SendEmailNode>();
        services.AddSingleton<INodeHandler, SendDiscordNode>();
        services.AddSingleton<INodeHandler, SendSlackNode>();
        services.AddSingleton<INodeHandler, FileLoggerNode>();
        services.AddSingleton<INodeHandler, CodeBlockNode>();
        services.AddSingleton<INodeHandler, HttpRequestNode>();
        services.AddSingleton<INodeHandler, DelayNode>();
        services.AddSingleton<INodeHandler, SplitIntoBatchesNode>();

        return services;
    }
}
