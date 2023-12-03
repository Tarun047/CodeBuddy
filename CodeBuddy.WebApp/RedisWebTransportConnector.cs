using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.Http.Features;
using StackExchange.Redis;

namespace CodeBuddy.WebApp;

public class RedisWebTransportConnector(ConnectionMultiplexer connectionMultiplexer, ILogger<RedisWebTransportConnector> logger)
{
    async Task<ConnectionContext> WaitForStream(IWebTransportSession session)
    {
        ConnectionContext? stream = null;
        while (true)
        {
            // wait until we get a stream
            stream = await session.AcceptStreamAsync(CancellationToken.None);
            if (stream is not null)
            {
                logger.LogInformation("Got stream!");
                return stream;
            }
        }
    }
    public async Task HandleWebTransportSession(IWebTransportSession session)
    {
        logger.LogInformation("Inside Handle Web Transport session: {sessionId}", session.SessionId);
        var stream = await WaitForStream(session);
        var inputPipe = stream.Transport.Input;
        var outputPipe = stream.Transport.Output;
        await using var inputStream = inputPipe.AsStream();
        await using var outputStream = outputPipe.AsStream();
        var memory = new Memory<byte>(new byte[8192]);
        var subscriber = connectionMultiplexer.GetSubscriber();
        while (!stream.ConnectionClosed.IsCancellationRequested)
        {
            if (inputStream.CanRead)
            {
                var length = await inputStream.ReadAsync(memory);
                var outputMemory = memory[..length];
                var codeGram = JsonSerializer.Deserialize<CodeGram>(outputMemory.Span, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                if (codeGram == null)
                {
                    continue;
                }
                logger.LogInformation(codeGram.Type.ToString());
                switch (codeGram.Type)
                {
                    case EventType.Message:
                    {
                        if (!string.IsNullOrEmpty(codeGram.Board))
                        {
                            await subscriber.PublishAsync(codeGram.Board, outputMemory);
                        }

                        break;
                    }
                    case EventType.Connect:
                    {
                        codeGram.Board ??= Guid.NewGuid().ToString();
                        codeGram.UserId = Guid.NewGuid().ToString();
                        await subscriber.SubscribeAsync(codeGram.Board,
                            (_, value) => outputPipe.AsStream().WriteAsync(value));
                        await outputStream.WriteAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(codeGram,
                            new JsonSerializerOptions
                            {
                                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                            })));
                        break;
                    }
                    case EventType.Disconnect:
                    {
                        if (!string.IsNullOrEmpty(codeGram.Board))
                        {
                            await subscriber.UnsubscribeAsync(codeGram.Board);
                        }

                        break;
                    }
                }
            }
        }
    }
}