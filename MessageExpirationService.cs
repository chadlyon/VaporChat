namespace VaporChat;

public sealed class MessageExpirationService(ChatMessageStore messageStore, ILogger<MessageExpirationService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            messageStore.RemoveExpiredMessages();
            logger.LogDebug("Expired chat messages were removed.");
        }
    }
}
