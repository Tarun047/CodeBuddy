using System.Security.Cryptography.X509Certificates;
using CodeBuddy.WebApp.Utils;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using StackExchange.Redis;

namespace CodeBuddy.WebApp;

public class Program
{
    static readonly X509Certificate2 certificate = CertUtils.GenerateManualCertificate();
    
    static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(certificate);
         services.AddSingleton(
             ConnectionMultiplexer.Connect(configuration.GetConnectionString("Redis")!));
         services.AddScoped<RedisWebTransportConnector>();
         services.AddLogging();
         services.AddHttpLogging(options =>
         {
             options.CombineLogs = true;
         });
        services.AddControllers();
    }

    static void ConfigureWebHost(IWebHostBuilder webHostBuilder)
    {
        webHostBuilder.ConfigureKestrel((context, options) =>
        {
            options.ListenAnyIP(5001, listenOptions =>
            {
                listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
            });
            
            options.ListenAnyIP(4433, listenOptions =>
            {
                listenOptions.UseHttps(certificate);
                listenOptions.UseConnectionLogging();
                listenOptions.Protocols = HttpProtocols.Http1AndHttp2AndHttp3;
            });
        });
    }
    
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        ConfigureServices(builder.Services, builder.Configuration);
        ConfigureWebHost(builder.WebHost);

        var app = builder.Build();

        app.UseCors(policyBuilder => policyBuilder
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader()
        );
        
        app.MapControllers();
        app.UseHttpLogging();
        
        app.UseMiddleware<RedisWebTransportMiddleware>();

        await app.RunAsync();
    }
}
