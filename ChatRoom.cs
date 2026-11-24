using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text.Json;

namespace VaporChat;

public sealed class ChatRoom(
    ChatMessageStore messageStore,
    ILogger<ChatRoom> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ConcurrentDictionary<Guid, ChatPeer> _peers = new();

    public async Task JoinAsync(ChatPeer peer, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var history = messageStore.GetRecentMessages();
            _peers.TryAdd(peer.Id, peer);
            await SendAsync(peer, new ServerEvent("history", Messages: history), cancellationToken);
            await BroadcastAsync(
                new ServerEvent("system", Text: $"{peer.Nickname} joined the room."),
                peer.Id,
                cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task PublishAsync(ChatPeer peer, string text, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var message = new ChatMessage(peer.Nickname, text, DateTimeOffset.UtcNow);
            messageStore.Add(message);
            await BroadcastAsync(new ServerEvent("message", Message: message), null, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task LeaveAsync(ChatPeer peer)
    {
        await _gate.WaitAsync(CancellationToken.None);
        try
        {
            if (_peers.TryRemove(peer.Id, out _))
            {
                await BroadcastAsync(
                    new ServerEvent("system", Text: $"{peer.Nickname} left the room."),
                    peer.Id,
                    CancellationToken.None);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task BroadcastAsync(ServerEvent serverEvent, Guid? excludedPeerId, CancellationToken cancellationToken)
    {
        foreach (var peer in _peers.Values)
        {
            if (peer.Id != excludedPeerId &&
                !await SendAsync(peer, serverEvent, cancellationToken) &&
                _peers.TryRemove(peer.Id, out _))
            {
                logger.LogInformation("Removed an unreachable chat connection.");
            }
        }
    }

    private static async Task<bool> SendAsync(
        ChatPeer peer,
        ServerEvent serverEvent,
        CancellationToken cancellationToken)
    {
        if (peer.Socket.State != WebSocketState.Open)
        {
            return false;
        }

        var payload = JsonSerializer.SerializeToUtf8Bytes(serverEvent, JsonOptions);
        await peer.SendLock.WaitAsync(cancellationToken);
        try
        {
            if (peer.Socket.State != WebSocketState.Open)
            {
                return false;
            }

            await peer.Socket.SendAsync(payload, WebSocketMessageType.Text, true, cancellationToken);
            return true;
        }
        catch (WebSocketException)
        {
            return false;
        }
        finally
        {
            peer.SendLock.Release();
        }
    }
}

public sealed class ChatPeer(WebSocket socket, string nickname)
{
    public Guid Id { get; } = Guid.NewGuid();
    public WebSocket Socket { get; } = socket;
    public string Nickname { get; } = nickname;
    public SemaphoreSlim SendLock { get; } = new(1, 1);
}

public sealed record ClientRequest(string? Type, string? Nickname, string? Text);

public sealed record ServerEvent(
    string Type,
    string? Text = null,
    ChatMessage? Message = null,
    IReadOnlyList<ChatMessage>? Messages = null);
