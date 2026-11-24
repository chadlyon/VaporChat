using Microsoft.Extensions.Options;

namespace VaporChat;

public sealed class ChatMessageStore(IOptions<ChatOptions> options)
{
    private readonly object _lock = new();
    private readonly List<StoredMessage> _messages = [];
    private readonly TimeSpan _retention = TimeSpan.FromMinutes(options.Value.MessageRetentionMinutes);

    public IReadOnlyList<ChatMessage> GetRecentMessages()
    {
        lock (_lock)
        {
            RemoveExpiredMessages(DateTimeOffset.UtcNow);
            return _messages.Select(message => message.Message).ToArray();
        }
    }

    public void Add(ChatMessage message)
    {
        lock (_lock)
        {
            var now = DateTimeOffset.UtcNow;
            RemoveExpiredMessages(now);
            _messages.Add(new StoredMessage(message, message.SentAtUtc + _retention));
        }
    }

    public void RemoveExpiredMessages()
    {
        lock (_lock)
        {
            RemoveExpiredMessages(DateTimeOffset.UtcNow);
        }
    }

    private void RemoveExpiredMessages(DateTimeOffset now)
    {
        _messages.RemoveAll(message => message.ExpiresAtUtc <= now);
    }

    private sealed record StoredMessage(ChatMessage Message, DateTimeOffset ExpiresAtUtc);
}
