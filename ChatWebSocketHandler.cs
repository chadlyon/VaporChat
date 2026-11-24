using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace VaporChat;

public static class ChatWebSocketHandler
{
    private const int MaxRequestBytes = 4096;
    private const int MaxNicknameLength = 24;
    private const int MaxMessageLength = 2000;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task HandleAsync(HttpContext context, ChatRoom room, ILoggerFactory loggerFactory)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync("A WebSocket connection is required.");
            return;
        }

        var logger = loggerFactory.CreateLogger("ChatWebSocket");
        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        ChatPeer? peer = null;

        try
        {
            var joinRequest = await ReceiveRequestAsync(socket, context.RequestAborted);
            var nickname = joinRequest?.Type == "join" ? joinRequest.Nickname?.Trim() : null;
            if (string.IsNullOrWhiteSpace(nickname) || nickname.Length > MaxNicknameLength)
            {
                await CloseAsync(socket, WebSocketCloseStatus.PolicyViolation, "A nickname of 1-24 characters is required.");
                return;
            }

            peer = new ChatPeer(socket, nickname);
            await room.JoinAsync(peer, context.RequestAborted);

            while (socket.State == WebSocketState.Open && !context.RequestAborted.IsCancellationRequested)
            {
                var request = await ReceiveRequestAsync(socket, context.RequestAborted);
                if (request is null)
                {
                    break;
                }

                if (request.Type != "message")
                {
                    await CloseAsync(socket, WebSocketCloseStatus.PolicyViolation, "Unsupported chat request.");
                    break;
                }

                var text = request.Text?.Trim();
                if (string.IsNullOrWhiteSpace(text) || text.Length > MaxMessageLength)
                {
                    await CloseAsync(socket, WebSocketCloseStatus.PolicyViolation, "Messages must be 1-2000 characters.");
                    break;
                }

                await room.PublishAsync(peer, text, context.RequestAborted);
            }
        }
        catch (WebSocketException exception)
        {
            logger.LogDebug(exception, "Chat WebSocket disconnected unexpectedly.");
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            logger.LogDebug("Chat WebSocket request was cancelled.");
        }
        finally
        {
            if (peer is not null)
            {
                await room.LeaveAsync(peer);
            }
        }
    }

    private static async Task<ClientRequest?> ReceiveRequestAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        var buffer = new byte[1024];
        using var requestData = new MemoryStream();

        while (true)
        {
            var result = await socket.ReceiveAsync(buffer, cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                return null;
            }

            if (result.MessageType != WebSocketMessageType.Text)
            {
                throw new WebSocketException("Only text WebSocket messages are supported.");
            }

            if (requestData.Length + result.Count > MaxRequestBytes)
            {
                throw new WebSocketException("The WebSocket request is too large.");
            }

            requestData.Write(buffer, 0, result.Count);
            if (result.EndOfMessage)
            {
                return JsonSerializer.Deserialize<ClientRequest>(requestData.ToArray(), JsonOptions);
            }
        }
    }

    private static async Task CloseAsync(WebSocket socket, WebSocketCloseStatus status, string description)
    {
        if (socket.State == WebSocketState.Open)
        {
            await socket.CloseAsync(status, description, CancellationToken.None);
        }
    }
}
