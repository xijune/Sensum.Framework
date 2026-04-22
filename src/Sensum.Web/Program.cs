using Serilog;
using Serilog.Events;

namespace Sensum.Web;

internal static class Program
{
    private static void Main(string[] args)
    {
        // Configure Serilog
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.File("logs/sensum-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7)
            .CreateLogger();

        try
        {
            Log.Information("Starting Sensum Web Interface");

            var builder = WebApplication.CreateBuilder(args);
            
            // Add Serilog
            builder.Host.UseSerilog();
            
            // Configure web host
            var port = Environment.GetEnvironmentVariable("SENSUM_PORT") ?? "5000";
            builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
            
            // Add CORS
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAll", policy =>
                {
                    policy.AllowAnyOrigin()
                          .AllowAnyMethod()
                          .AllowAnyHeader();
                });
            });
            
            // Add SignalR
            builder.Services.AddSignalR();
            
            // Add authentication (commented out until fully configured)
            // builder.Services.AddAuthentication(options =>
            // {
            //     options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            //     options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            // })
            // .AddJwtBearer(options =>
            // {
            //     options.TokenValidationParameters = new TokenValidationParameters
            //     {
            //         ValidateIssuer = false,
            //         ValidateAudience = false,
            //         ValidateLifetime = true,
            //         ValidateIssuerSigningKey = true,
            //         IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
            //             Environment.GetEnvironmentVariable("SENSUM_JWT_SECRET") ?? GenerateSecureKey()))
            //     };
            // });
            
            // Add health checks
            builder.Services.AddHealthChecks();
            
            var app = builder.Build();
            
            // Use Serilog request logging
            app.UseSerilogRequestLogging(options =>
            {
                options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000}ms";
            });
            
            // Use CORS
            app.UseCors("AllowAll");
            
            // Serve static files (index.html)
            app.UseDefaultFiles();
            app.UseStaticFiles();
            
            // Map API endpoints
            app.MapBotApi();
            
            // Map health checks
            app.MapHealthChecks("/health");
            
            Log.Information("Sensum Web Interface starting on http://localhost:{Port}", port);
            Log.Information("Press Ctrl+C to stop");
            
            app.Run();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
    
    private static string GenerateSecureKey()
    {
        var bytes = new byte[32];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }
}
