using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;
using Weaver.Infrastructure.Realtime;

namespace Weaver.Api.Realtime;

/// <summary>
/// The other half of IRunStatusPublisher: subscribes to the Redis channel both Api and Worker
/// publish run-status changes to, and relays each one to the SignalR group for whichever user
/// owns that run. Runs for the lifetime of the Api process; the subscription itself is cheap
/// (Redis pub/sub, not polling) so there's no tick-based loop here.
/// </summary>
public class RunStatusRelayService : IHostedService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IHubContext<RunStatusHub> _hub;
    private ChannelMessageQueue? _queue;

    public RunStatusRelayService(IConnectionMultiplexer redis, IHubContext<RunStatusHub> hub)
    {
        _redis = redis;
        _hub = hub;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _queue = await _redis.GetSubscriber().SubscribeAsync(RedisChannel.Literal(RunStatusPublisher.ChannelName));
        _queue.OnMessage(async channelMessage =>
        {
            RunStatusMessage? message;
            try
            {
                message = JsonSerializer.Deserialize<RunStatusMessage>(channelMessage.Message!);
            }
            catch (JsonException)
            {
                return;
            }

            if (message is null)
            {
                return;
            }

            await _hub.Clients.Group(RunStatusHub.GroupName(message.OwnerUserId))
                .SendAsync("runStatusChanged", new { message.Kind, message.RunId, message.Status });
        });
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_queue is not null)
        {
            await _queue.UnsubscribeAsync();
        }
    }
}
