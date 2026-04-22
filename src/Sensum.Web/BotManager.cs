using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using BCrypt.Net;
using Microsoft.AspNetCore.SignalR;
using Sensum.Console;
using Sensum.Framework;
using Sensum.Framework.Entities;
using Sensum.Framework.Growtopia;
using Sensum.Framework.Growtopia.Player;
using Serilog;

namespace Sensum.Web;

public class BotInstance
{
    public string Id { get; } = Guid.NewGuid().ToString("N")[..8];
    public Bot Bot { get; set; }
    public string Name { get; set; }
    public bool IsConnected { get; set; }
    public string WorldName { get; set; } = "";
    public ConcurrentQueue<string> ConsoleMessages { get; } = new();
    public DateTime CreatedAt { get; } = DateTime.UtcNow;
    public DateTime? LastConnectedAt { get; set; }
    
    private readonly Channel<string> _consoleChannel = Channel.CreateUnbounded<string>();
    
    public BotInstance(string growid, string password)
    {
        Name = growid;
        Bot = new Bot(new Proxy(App.IGNORED_PROXY_HOST, 0))
        {
            ConnectedCallback = () => 
            {
                IsConnected = true;
                LastConnectedAt = DateTime.UtcNow;
                AppendConsole("Connected successfully");
                OnStatusChanged?.Invoke(this);
            },
            DisconnectedCallback = () => 
            {
                IsConnected = false;
                AppendConsole("Disconnected");
                OnStatusChanged?.Invoke(this);
            },
            ConnectionTimeoutCallback = () => 
            {
                IsConnected = false;
                AppendConsole("Connection timeout");
                OnStatusChanged?.Invoke(this);
            },
            AuthenticationErrorCallback = error => 
            {
                AppendConsole($"Authentication error: {error}");
            }
        };
        Bot.LoginBuilder.SetLegacy(growid, password, LoginBuilder.GenerateGuestName());
        Bot.World.JoinedWorldCallback = () => 
        {
            WorldName = Bot.World.Name;
            AppendConsole($"Joined world: {WorldName}");
        };
        Bot.World.LoadFailedCallback = reason => 
        {
            AppendConsole($"Load failed: {reason}");
        };
        Bot.ConsoleManager.MessageAddedCallback = (msg) => AppendConsole(msg);
    }
    
    private void AppendConsole(string msg)
    {
        var timestampedMsg = $"[{DateTime.Now:HH:mm:ss}] {msg}";
        
        // Add to concurrent queue with limit
        ConsoleMessages.Enqueue(timestampedMsg);
        while (ConsoleMessages.Count > 200 && ConsoleMessages.TryDequeue(out _)) { }
        
        // Write to channel for SignalR
        _consoleChannel.Writer.TryWrite(timestampedMsg);
        
        OnConsoleUpdated?.Invoke(this);
    }
    
    public IAsyncEnumerable<string> GetConsoleUpdates()
    {
        return _consoleChannel.Reader.ReadAllAsync();
    }
    
    public void Connect() => Bot.Connect();
    public void Disconnect() => Bot.Disconnect();
    
    public event Action<BotInstance>? OnStatusChanged;
    public event Action<BotInstance>? OnConsoleUpdated;
}

public class BotManager : IDisposable
{
    private static readonly Lazy<BotManager> _lazyInstance = new(() => new BotManager());
    public static BotManager Instance => _lazyInstance.Value;
    
    private readonly ConcurrentDictionary<string, BotInstance> _bots = new();
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _disposed;
    
    public event Action? OnBotsChanged;
    
    public IReadOnlyCollection<BotInstance> Bots => _bots.Values.ToList().AsReadOnly();
    
    public BotInstance AddBot(string growid, string password)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(BotManager));
        
        var bot = new BotInstance(growid, password);
        
        bot.OnStatusChanged += _ => OnBotsChanged?.Invoke();
        bot.OnConsoleUpdated += _ => OnBotsChanged?.Invoke();
        
        if (!_bots.TryAdd(bot.Id, bot))
        {
            throw new InvalidOperationException("Failed to add bot");
        }
        
        Log.Information("Bot added: {BotId} ({GrowId})", bot.Id, growid);
        OnBotsChanged?.Invoke();
        return bot;
    }
    
    public BotInstance? GetBot(string id)
    {
        _bots.TryGetValue(id, out var bot);
        return bot;
    }
    
    public bool RemoveBot(string id)
    {
        if (_bots.TryRemove(id, out var bot))
        {
            try
            {
                bot.Disconnect();
                Log.Information("Bot removed: {BotId} ({GrowId})", id, bot.Name);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error disconnecting bot {BotId}", id);
            }
            
            OnBotsChanged?.Invoke();
            return true;
        }
        return false;
    }
    
    public int GetBotCount() => _bots.Count;
    
    public void Dispose()
    {
        if (_disposed) return;
        
        foreach (var bot in _bots.Values)
        {
            try
            {
                bot.Disconnect();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error disconnecting bot during disposal");
            }
        }
        
        _lock.Dispose();
        _disposed = true;
    }
}

public class PasswordHasher
{
    public static string Hash(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, BCrypt.Net.BCrypt.GenerateSalt(12));
    }
    
    public static bool Verify(string password, string hash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch
        {
            return false;
        }
    }
}

public class AuthService
{
    private static readonly Lazy<AuthService> _lazyInstance = new(() => new AuthService());
    public static AuthService Instance => _lazyInstance.Value;
    
    private readonly ConcurrentDictionary<string, UserCredentials> _users = new();
    private readonly string _jwtSecret;
    
    public AuthService()
    {
        _jwtSecret = Environment.GetEnvironmentVariable("SENSUM_JWT_SECRET") 
                     ?? GenerateSecureRandomString(32);
    }
    
    public bool RegisterUser(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return false;
            
        if (_users.ContainsKey(username))
            return false;
            
        var hashedPassword = PasswordHasher.Hash(password);
        _users[username] = new UserCredentials { Username = username, PasswordHash = hashedPassword };
        
        Log.Information("User registered: {Username}", username);
        return true;
    }
    
    public bool ValidateUser(string username, string password)
    {
        if (!_users.TryGetValue(username, out var user))
            return false;
            
        return PasswordHasher.Verify(password, user.PasswordHash);
    }
    
    public string GenerateJwtToken(string username)
    {
        // Simple token generation (in production, use proper JWT library)
        var payload = new
        {
            sub = username,
            iat = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            exp = DateTimeOffset.UtcNow.AddHours(24).ToUnixTimeSeconds()
        };
        
        var payloadJson = JsonSerializer.Serialize(payload);
        var payloadBytes = Encoding.UTF8.GetBytes(payloadJson);
        var signatureBytes = HMACSHA256.HashData(Encoding.UTF8.GetBytes(_jwtSecret), payloadBytes);
        
        return $"{Convert.ToBase64String(payloadBytes)}.{Convert.ToBase64String(signatureBytes)}";
    }
    
    public bool ValidateToken(string token)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 2) return false;
            
            var payloadBytes = Convert.FromBase64String(parts[0]);
            var signatureBytes = Convert.FromBase64String(parts[1]);
            
            var expectedSignature = HMACSHA256.HashData(Encoding.UTF8.GetBytes(_jwtSecret), payloadBytes);
            
            if (!signatureBytes.SequenceEqual(expectedSignature))
                return false;
            
            var payloadJson = Encoding.UTF8.GetString(payloadBytes);
            var payload = JsonSerializer.Deserialize<JsonElement>(payloadJson);
            
            var exp = payload.GetProperty("exp").GetInt64();
            if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > exp)
                return false;
            
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    private static string GenerateSecureRandomString(int length)
    {
        var bytes = new byte[length];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }
}

public class UserCredentials
{
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
}
