using VaporChat;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddOptions<ChatOptions>()
    .BindConfiguration("Chat")
    .Validate(
        options => options.MessageRetentionMinutes is >= 1 and <= 60,
        "Chat:MessageRetentionMinutes must be between 1 and 60.")
    .ValidateOnStart();

builder.Services.AddSingleton<ChatMessageStore>();
builder.Services.AddHostedService<MessageExpirationService>();
builder.Services.AddSingleton<ChatRoom>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseWebSockets();
app.Map("/chat", ChatWebSocketHandler.HandleAsync);
app.MapFallbackToFile("index.html");

app.Run();
