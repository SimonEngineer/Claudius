using Weaver.Domain;

namespace Weaver.Infrastructure.Queue;

/// <summary>Wire payload carried on the Redis stream; ScrapeJobId correlates back to the durable ScrapeJob row.</summary>
public record ScrapeJobMessage(Guid ScrapeJobId, Guid ScrapingProjectId, TriggerKind TriggeredBy, Guid? WorkflowRunId);

/// <summary>A message read off the stream, still awaiting acknowledgement.</summary>
public record QueuedScrapeJob(string StreamMessageId, ScrapeJobMessage Message, int DeliveryCount);
