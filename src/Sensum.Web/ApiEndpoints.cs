using System.Text.Json;
using Sensum.Framework.Growtopia.Network;

namespace Sensum.Web;

public class BotDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsConnected { get; set; }
    public string WorldName { get; set; } = "";
    public uint Ping { get; set; }
    public List<string> ConsoleMessages { get; set; } = new();
}

public static class ApiEndpoints
{
    public static void MapBotApi(this WebApplication app)
    {
        app.MapGet("/api/bots", () =>
        {
            var bots = BotManager.Instance.Bots.Select(b => new BotDto
            {
                Id = b.Name,
                Name = b.Name,
                IsConnected = b.IsConnected,
                WorldName = b.WorldName,
                Ping = b.Bot.Ping,
                ConsoleMessages = b.ConsoleMessages
            }).ToList();
            return Results.Json(bots);
        });

        app.MapPost("/api/bots", async (HttpContext context) =>
        {
            using var reader = new StreamReader(context.Request.Body);
            var json = await reader.ReadToEndAsync();
            var data = JsonSerializer.Deserialize<JsonElement>(json);
            
            var growid = data.GetProperty("growid").GetString() ?? "";
            var password = data.GetProperty("password").GetString() ?? "";
            
            if (string.IsNullOrEmpty(growid) || string.IsNullOrEmpty(password))
            {
                return Results.BadRequest(new { error = "GrowID and Password are required" });
            }
            
            var bot = BotManager.Instance.AddBot(growid, password);
            return Results.Ok(new { id = bot.Name, message = "Bot added successfully" });
        });

        app.MapPost("/api/bots/{id}/connect", (string id) =>
        {
            var bot = BotManager.Instance.Bots.FirstOrDefault(b => b.Name == id);
            if (bot == null)
            {
                return Results.NotFound(new { error = "Bot not found" });
            }
            
            bot.Connect();
            return Results.Ok(new { message = "Connection initiated" });
        });

        app.MapPost("/api/bots/{id}/disconnect", (string id) =>
        {
            var bot = BotManager.Instance.Bots.FirstOrDefault(b => b.Name == id);
            if (bot == null)
            {
                return Results.NotFound(new { error = "Bot not found" });
            }
            
            bot.Disconnect();
            return Results.Ok(new { message = "Disconnected" });
        });

        app.MapDelete("/api/bots/{id}", (string id) =>
        {
            var bot = BotManager.Instance.Bots.FirstOrDefault(b => b.Name == id);
            if (bot == null)
            {
                return Results.NotFound(new { error = "Bot not found" });
            }
            
            BotManager.Instance.RemoveBot(bot);
            return Results.Ok(new { message = "Bot removed" });
        });
    }
}
