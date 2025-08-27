using System;
using System.IO;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using Commands.API;
using Commands.Core;
using HarmonyLib;
using Localization.API;
using Newtonsoft.Json.Linq;
 
namespace Commands;
[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency("com.agitoreiken.database", BepInDependency.DependencyFlags.HardDependency)]
[BepInDependency("com.agitoreiken.localization", BepInDependency.DependencyFlags.HardDependency)]
public class Plugin : BasePlugin
{
    public static Harmony _harmony;
    public static Plugin Instance = null!;
    public ICommandsAPI API = null!;
    public override void Load()
    {
        Instance = this;
        Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} version {MyPluginInfo.PLUGIN_VERSION} is loading...");

        

        API = new CommandsAPI();
        
        Localization.Plugin.Instance.API.RegisterPlugin(MyPluginInfo.PLUGIN_GUID, $"{Paths.ConfigPath}/{MyPluginInfo.PLUGIN_GUID}");
        _harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        _harmony.PatchAll(System.Reflection.Assembly.GetExecutingAssembly());
        Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

    }

    public override bool Unload()
    {
        return true;
    } 
}
