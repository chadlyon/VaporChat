# VaporChat

A lightweight ASP.NET Core chat app. Every WebSocket client joins one public room after choosing a nickname. Messages are held in memory only and expire automatically.

## Run

```powershell
dotnet run
```

Open the local URL printed by ASP.NET Core and choose a nickname when prompted.

## Message retention

Configure `Chat:MessageRetentionMinutes` in `appsettings.json` or with the `Chat__MessageRetentionMinutes` environment variable. The value must be between 1 and 60 minutes (inclusive); the default is 60 minutes. Messages are never persisted to disk or another storage service.

The chat uses a native WebSocket endpoint at `/chat`. The browser first sends a `join` request with a nickname, then exchanges `message` requests. New clients receive the still-retained messages in the room.