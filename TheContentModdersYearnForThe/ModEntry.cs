using System.Diagnostics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using TheContentModdersYearnForThe.Foreach;

namespace TheContentModdersYearnForThe;

public sealed class ModEntry : Mod
{
#if DEBUG
    private const LogLevel DEFAULT_LOG_LEVEL = LogLevel.Debug;
#else
    private const LogLevel DEFAULT_LOG_LEVEL = LogLevel.Trace;
#endif

    public const string ModNamespace = "TheContentModdersYearnForThe";
    public const string ModId = $"mushymato.{ModNamespace}";
    private static IMonitor? mon;
    internal static IGameContentHelper content = null!;
    internal static IModRegistry registry = null!;
    internal const AssetEditPriority ReallyLateEdit = AssetEditPriority.Late + 100;

    internal static IAssetName foreachAssetName = null!;
    internal ForeachManager foreachManager = null!;

    public override void Entry(IModHelper helper)
    {
        mon = Monitor;
        content = helper.GameContent;
        foreachAssetName = content.ParseAssetName($"{ModNamespace}/Foreach");
        registry = helper.ModRegistry;

        foreachManager = new(foreachAssetName);

        helper.Events.Content.AssetRequested += OnAssetRequested;
        helper.Events.Content.AssetsInvalidated += OnAssetsInvalidated;
        helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;

        Log(
            """
:
          _____                   _______                   _____                    _____                    _____                    _____                    _____          
         /\    \                 /::\    \                 /\    \                  /\    \                  /\    \                  /\    \                  /\    \         
        /::\    \               /::::\    \               /::\    \                /::\    \                /::\    \                /::\    \                /::\____\        
       /::::\    \             /::::::\    \             /::::\    \              /::::\    \              /::::\    \              /::::\    \              /:::/    /        
      /::::::\    \           /::::::::\    \           /::::::\    \            /::::::\    \            /::::::\    \            /::::::\    \            /:::/    /         
     /:::/\:::\    \         /:::/~~\:::\    \         /:::/\:::\    \          /:::/\:::\    \          /:::/\:::\    \          /:::/\:::\    \          /:::/    /          
    /:::/__\:::\    \       /:::/    \:::\    \       /:::/__\:::\    \        /:::/__\:::\    \        /:::/__\:::\    \        /:::/  \:::\    \        /:::/____/           
   /::::\   \:::\    \     /:::/    / \:::\    \     /::::\   \:::\    \      /::::\   \:::\    \      /::::\   \:::\    \      /:::/    \:::\    \      /::::\    \           
  /::::::\   \:::\    \   /:::/____/   \:::\____\   /::::::\   \:::\    \    /::::::\   \:::\    \    /::::::\   \:::\    \    /:::/    / \:::\    \    /::::::\    \   _____  
 /:::/\:::\   \:::\    \ |:::|    |     |:::|    | /:::/\:::\   \:::\____\  /:::/\:::\   \:::\    \  /:::/\:::\   \:::\    \  /:::/    /   \:::\    \  /:::/\:::\    \ /\    \ 
/:::/  \:::\   \:::\____\|:::|____|     |:::|    |/:::/  \:::\   \:::|    |/:::/__\:::\   \:::\____\/:::/  \:::\   \:::\____\/:::/____/     \:::\____\/:::/  \:::\    /::\____\
\::/    \:::\   \::/    / \:::\    \   /:::/    / \::/   |::::\  /:::|____|\:::\   \:::\   \::/    /\::/    \:::\  /:::/    /\:::\    \      \::/    /\::/    \:::\  /:::/    /
 \/____/ \:::\   \/____/   \:::\    \ /:::/    /   \/____|:::::\/:::/    /  \:::\   \:::\   \/____/  \/____/ \:::\/:::/    /  \:::\    \      \/____/  \/____/ \:::\/:::/    / 
          \:::\    \        \:::\    /:::/    /          |:::::::::/    /    \:::\   \:::\    \               \::::::/    /    \:::\    \                       \::::::/    /  
           \:::\____\        \:::\__/:::/    /           |::|\::::/    /      \:::\   \:::\____\               \::::/    /      \:::\    \                       \::::/    /   
            \::/    /         \::::::::/    /            |::| \::/____/        \:::\   \::/    /               /:::/    /        \:::\    \                      /:::/    /    
             \/____/           \::::::/    /             |::|  ~|               \:::\   \/____/               /:::/    /          \:::\    \                    /:::/    /     
                                \::::/    /              |::|   |                \:::\    \                  /:::/    /            \:::\    \                  /:::/    /      
                                 \::/____/               \::|   |                 \:::\____\                /:::/    /              \:::\____\                /:::/    /       
                                  ~~                      \:|   |                  \::/    /                \::/    /                \::/    /                \::/    /        
                                                           \|___|                   \/____/                  \/____/                  \/____/                  \/____/         
""",
            LogLevel.Alert
        );
    }

    private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        foreachManager.OnUpdateTicked(e);
    }

    private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        foreachManager.AssetRequested(e);
    }

    private void OnAssetsInvalidated(object? sender, AssetsInvalidatedEventArgs e)
    {
        foreachManager.AssetsInvalidated(e);
    }

    /// <summary>SMAPI static monitor Log wrapper</summary>
    /// <param name="msg"></param>
    /// <param name="level"></param>
    internal static void Log(string msg, LogLevel level = DEFAULT_LOG_LEVEL)
    {
        mon!.Log(msg, level);
    }

    /// <summary>SMAPI static monitor LogOnce wrapper</summary>
    /// <param name="msg"></param>
    /// <param name="level"></param>
    internal static void LogOnce(string msg, LogLevel level = DEFAULT_LOG_LEVEL)
    {
        mon!.LogOnce(msg, level);
    }

    /// <summary>SMAPI static monitor Log wrapper, debug only</summary>
    /// <param name="msg"></param>
    /// <param name="level"></param>
    [Conditional("DEBUG")]
    internal static void LogDebug(string msg, LogLevel level = DEFAULT_LOG_LEVEL)
    {
        mon!.Log(msg, level);
    }
}
