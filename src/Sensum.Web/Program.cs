using ZeroLog;
using ZeroLog.Appenders;
using ZeroLog.Configuration;

namespace Sensum.Web;

internal static class Program
{
    private static void Main(string[] args)
    {
        // Initialize logging
        LogManager.Initialize(new ZeroLogConfiguration 
        { 
            RootLogger = { Appenders = {new ConsoleAppender()} } 
        });

        var builder = WebApplication.CreateBuilder(args);
        
        // Configure web host
        builder.WebHost.UseUrls("http://0.0.0.0:5000");
        
        var app = builder.Build();
        
        // Serve static files (index.html)
        app.UseDefaultFiles();
        app.UseStaticFiles();
        
        // Map API endpoints
        app.MapBotApi();
        
        app.Run();
    }
}
