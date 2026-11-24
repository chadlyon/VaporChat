namespace VaporChat;

public sealed record ChatMessage(string Nickname, string Text, DateTimeOffset SentAtUtc);
