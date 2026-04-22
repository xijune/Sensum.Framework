using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.Tokens;
using Serilog;
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
    public DateTime CreatedAt { get; set; }
    public DateTime? LastConnectedAt { get; set; }
}

public class AuthRequest
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
}

public class AuthResponse
{
    public string Token { get; set; } = "";
    public string Username { get; set; } = "";
    public bool Success { get; set; }
    public string? Error { get; set; }
}

public class ApiResponse<T>
{
    public T? Data { get; set; }
    public bool Success { get; set; } = true;
    public string? Error { get; set; }
}

public static class ApiEndpoints
{
    public static void MapBotApi(this WebApplication app)
    {
        // Health check endpoint (no auth required)
        app.MapGet("/api/health", () =>
        {
            return Results.Ok(new 
            { 
                status = "healthy", 
                timestamp = DateTime.UtcNow,
                botCount = BotManager.Instance.GetBotCount()
            });
        });
        
        // Google Authentication endpoints
        app.MapGet("/api/auth/google", () =>
        {
            return Results.Challenge(new AuthenticationProperties { RedirectUri = "/api/auth/google/callback" }, new[] { GoogleDefaults.AuthenticationScheme });
        });
        
        app.MapGet("/api/auth/google/callback", async (HttpContext context, [FromServices] ILogger<Program> logger) =>
        {
            try
            {
                var result = await context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                
                if (result.Succeeded && result.Principal?.Identity?.IsAuthenticated == true)
                {
                    var email = result.Principal.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? 
                               result.Principal.FindFirst("email")?.Value ?? "unknown";
                    
                    logger.LogInformation("Google login successful for user: {Email}", email);
                    
                    return Results.Redirect("/?auth=success&email=" + Uri.EscapeDataString(email));
                }
                
                return Results.Redirect("/?auth=failed");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during Google authentication callback");
                return Results.Redirect("/?auth=error");
            }
        });
        
        app.MapPost("/api/auth/logout", async (HttpContext context) =>
        {
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.Ok(new ApiResponse<object> { Success = true, Data = new { message = "Logged out successfully" } });
        });
        
        app.MapGet("/api/auth/me", (HttpContext context) =>
        {
            if (context.User.Identity?.IsAuthenticated == true)
            {
                var email = context.User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? 
                           context.User.FindFirst("email")?.Value ?? "unknown";
                var name = context.User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? 
                          context.User.FindFirst("name")?.Value ?? email;
                
                return Results.Ok(new ApiResponse<object> 
                { 
                    Success = true, 
                    Data = new { 
                        isAuthenticated = true, 
                        email, 
                        name,
                        provider = "google"
                    } 
                });
            }
            
            return Results.Ok(new ApiResponse<object> 
            { 
                Success = true, 
                Data = new { isAuthenticated = false } 
            });
        });
        
        // Legacy username/password authentication (kept for backward compatibility)
        app.MapPost("/api/auth/register", async (HttpContext context) =>
        {
            try
            {
                var request = await JsonSerializer.DeserializeAsync<AuthRequest>(context.Request.Body);
                if (request == null || string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
                {
                    return Results.BadRequest(new ApiResponse<object> 
                    { 
                        Success = false, 
                        Error = "Username and password are required" 
                    });
                }
                
                if (request.Username.Length < 3 || request.Username.Length > 50)
                {
                    return Results.BadRequest(new ApiResponse<object> 
                    { 
                        Success = false, 
                        Error = "Username must be between 3 and 50 characters" 
                    });
                }
                
                if (request.Password.Length < 6)
                {
                    return Results.BadRequest(new ApiResponse<object> 
                    { 
                        Success = false, 
                        Error = "Password must be at least 6 characters" 
                    });
                }
                
                if (!AuthService.Instance.RegisterUser(request.Username, request.Password))
                {
                    return Results.Conflict(new ApiResponse<object> 
                    { 
                        Success = false, 
                        Error = "Username already exists" 
                    });
                }
                
                var token = AuthService.Instance.GenerateJwtToken(request.Username);
                return Results.Ok(new AuthResponse 
                { 
                    Success = true, 
                    Token = token, 
                    Username = request.Username 
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during registration");
                return Results.StatusCode(500);
            }
        });
        
        app.MapPost("/api/auth/login", async (HttpContext context) =>
        {
            try
            {
                var request = await JsonSerializer.DeserializeAsync<AuthRequest>(context.Request.Body);
                if (request == null || string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
                {
                    return Results.BadRequest(new ApiResponse<object> 
                    { 
                        Success = false, 
                        Error = "Username and password are required" 
                    });
                }
                
                if (!AuthService.Instance.ValidateUser(request.Username, request.Password))
                {
                    return Results.Unauthorized();
                }
                
                var token = AuthService.Instance.GenerateJwtToken(request.Username);
                return Results.Ok(new AuthResponse 
                { 
                    Success = true, 
                    Token = token, 
                    Username = request.Username 
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during login");
                return Results.StatusCode(500);
            }
        });
        
        // Protected API endpoints
        app.MapGet("/api/bots", () =>
        {
            var bots = BotManager.Instance.Bots.Select(b => new BotDto
            {
                Id = b.Id,
                Name = b.Name,
                IsConnected = b.IsConnected,
                WorldName = b.WorldName,
                Ping = b.Bot.Ping,
                ConsoleMessages = b.ConsoleMessages.ToList(),
                CreatedAt = b.CreatedAt,
                LastConnectedAt = b.LastConnectedAt
            }).ToList();
            return Results.Ok(new ApiResponse<List<BotDto>> { Data = bots });
        });

        app.MapPost("/api/bots", async (HttpContext context) =>
        {
            try
            {
                using var reader = new StreamReader(context.Request.Body);
                var json = await reader.ReadToEndAsync();
                var data = JsonSerializer.Deserialize<JsonElement>(json);
                
                var growid = data.GetProperty("growid").GetString() ?? "";
                var password = data.GetProperty("password").GetString() ?? "";
                
                if (string.IsNullOrEmpty(growid) || string.IsNullOrEmpty(password))
                {
                    return Results.BadRequest(new ApiResponse<object> 
                    { 
                        Success = false, 
                        Error = "GrowID and Password are required" 
                    });
                }
                
                if (growid.Length < 3 || growid.Length > 50)
                {
                    return Results.BadRequest(new ApiResponse<object> 
                    { 
                        Success = false, 
                        Error = "GrowID must be between 3 and 50 characters" 
                    });
                }
                
                if (password.Length < 1)
                {
                    return Results.BadRequest(new ApiResponse<object> 
                    { 
                        Success = false, 
                        Error = "Password cannot be empty" 
                    });
                }
                
                var bot = BotManager.Instance.AddBot(growid, password);
                return Results.Ok(new ApiResponse<object> 
                { 
                    Success = true, 
                    Data = new { id = bot.Id, message = "Bot added successfully" } 
                });
            }
            catch (JsonException ex)
            {
                Log.Warning(ex, "Invalid JSON in request");
                return Results.BadRequest(new ApiResponse<object> 
                { 
                    Success = false, 
                    Error = "Invalid JSON format" 
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error adding bot");
                return Results.StatusCode(500);
            }
        });

        app.MapPost("/api/bots/{id}/connect", (string id) =>
        {
            var bot = BotManager.Instance.GetBot(id);
            if (bot == null)
            {
                return Results.NotFound(new ApiResponse<object> 
                { 
                    Success = false, 
                    Error = "Bot not found" 
                });
            }
            
            try
            {
                bot.Connect();
                return Results.Ok(new ApiResponse<object> 
                { 
                    Success = true, 
                    Data = new { message = "Connection initiated" } 
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error connecting bot {BotId}", id);
                return Results.StatusCode(500);
            }
        });

        app.MapPost("/api/bots/{id}/disconnect", (string id) =>
        {
            var bot = BotManager.Instance.GetBot(id);
            if (bot == null)
            {
                return Results.NotFound(new ApiResponse<object> 
                { 
                    Success = false, 
                    Error = "Bot not found" 
                });
            }
            
            try
            {
                bot.Disconnect();
                return Results.Ok(new ApiResponse<object> 
                { 
                    Success = true, 
                    Data = new { message = "Disconnected" } 
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error disconnecting bot {BotId}", id);
                return Results.StatusCode(500);
            }
        });

        app.MapDelete("/api/bots/{id}", (string id) =>
        {
            if (BotManager.Instance.RemoveBot(id))
            {
                return Results.Ok(new ApiResponse<object> 
                { 
                    Success = true, 
                    Data = new { message = "Bot removed" } 
                });
            }
            
            return Results.NotFound(new ApiResponse<object> 
            { 
                Success = false, 
                Error = "Bot not found" 
            });
        });
        
        // SignalR hub for real-time updates
        app.MapHub<BotHub>("/hubs/bots");
    }
}

public class BotHub : Hub
{
    private readonly ILogger<BotHub> _logger;
    
    public BotHub(ILogger<BotHub> logger)
    {
        _logger = logger;
    }
    
    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }
    
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
    
    public async Task SubscribeToBot(string botId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"bot-{botId}");
        _logger.LogInformation("Client {ConnectionId} subscribed to bot {BotId}", Context.ConnectionId, botId);
    }
    
    public async Task UnsubscribeFromBot(string botId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"bot-{botId}");
        _logger.LogInformation("Client {ConnectionId} unsubscribed from bot {BotId}", Context.ConnectionId, botId);
    }
}
