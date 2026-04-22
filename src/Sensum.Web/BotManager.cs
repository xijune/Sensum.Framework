using Sensum.Framework.Entities;
using Sensum.Framework.Growtopia;
using Sensum.Framework.Growtopia.Authentications;
using Sensum.Framework.Growtopia.Managers;
using Sensum.Framework.Growtopia.Player;
using Sensum.Framework.Proton;
using ZeroLog;
using ZeroLog.Appenders;
using ZeroLog.Configuration;

namespace Sensum.Web;

public class BotInstance
{
    public Bot Bot { get; set; }
    public string Name { get; set; }
    public bool IsConnected { get; set; }
    public string WorldName { get; set; } = "";
    public List<string> ConsoleMessages { get; set; } = new();
    
    public BotInstance(string growid, string password)
    {
        Name = growid;
        Bot = new Bot(new Proxy(App.IGNORED_PROXY_HOST, 0))
        {
            ConnectedCallback = () => 
            {
                IsConnected = true;
                OnStatusChanged?.Invoke(this);
            },
            DisconnectedCallback = () => 
            {
                IsConnected = false;
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
        ConsoleMessages.Add(msg);
        if (ConsoleMessages.Count > 200) ConsoleMessages.RemoveAt(0);
        OnConsoleUpdated?.Invoke(this);
    }
    
    public void Connect() => Bot.Connect();
    public void Disconnect() => Bot.Disconnect();
    
    public event Action<BotInstance>? OnStatusChanged;
    public event Action<BotInstance>? OnConsoleUpdated;
}

public class BotManager
{
    public static readonly BotManager Instance = new();
    public List<BotInstance> Bots { get; } = new();
    
    public event Action? OnBotsChanged;
    
    public BotInstance AddBot(string growid, string password)
    {
        var bot = new BotInstance(growid, password);
        bot.OnStatusChanged += _ => OnBotsChanged?.Invoke();
        bot.OnConsoleUpdated += _ => OnBotsChanged?.Invoke();
        Bots.Add(bot);
        OnBotsChanged?.Invoke();
        return bot;
    }
    
    public void RemoveBot(BotInstance bot)
    {
        bot.Disconnect();
        Bots.Remove(bot);
        OnBotsChanged?.Invoke();
    }
}
