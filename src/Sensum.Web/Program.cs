using Serilog;
using Serilog.Events;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;

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
            
            // Add Google Authentication
            builder.Services.AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
            })
            .AddCookie(options =>
            {
                options.LoginPath = "/api/auth/login";
                options.LogoutPath = "/api/auth/logout";
                options.ExpireTimeSpan = TimeSpan.FromHours(24);
                options.SlidingExpiration = true;
            })
            .AddGoogle(options =>
            {
                var googleConfig = builder.Configuration.GetSection("Authentication:Google");
                options.ClientId = googleConfig["ClientId"] ?? Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID") ?? "";
                options.ClientSecret = googleConfig["ClientSecret"] ?? Environment.GetEnvironmentVariable("GOOGLE_CLIENT_SECRET") ?? "";
                options.CallbackPath = "/api/auth/google/callback";
                options.SaveTokens = true;
                options.Scope.Add("email");
                options.Scope.Add("profile");
            });
            
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
            
            // Use authentication
            app.UseAuthentication();
            app.UseAuthorization();
            
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
}
