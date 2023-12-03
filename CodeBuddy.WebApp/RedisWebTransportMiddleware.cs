using Microsoft.AspNetCore.Http.Features;

namespace CodeBuddy.WebApp;

public class RedisWebTransportMiddleware(RequestDelegate next, RedisWebTransportConnector redisWebTransportConnector, ILogger<RedisWebTransportMiddleware> logger)
{ 
    public async Task InvokeAsync(HttpContext context)
    {
        logger.LogInformation("Inside Middleware!");
        var feature = context.Features.GetRequiredFeature<IHttpWebTransportFeature>();
        logger.LogInformation("Request is to {path}", context.Request.Path);
        if (!feature.IsWebTransportRequest)
        {
            logger.LogInformation("Executing normal request");
            await next(context);
            return;
        }
        
        logger.LogInformation("Starting the WT Con!");
        var session = await feature.AcceptAsync(CancellationToken.None);
        logger.LogInformation("Accepted the WT Con!");
        await redisWebTransportConnector.HandleWebTransportSession(session);
    }
}