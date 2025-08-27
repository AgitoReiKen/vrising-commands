using Commands.Core;
using HarmonyLib;
using ProjectM;

namespace Commands.Patches;


[HarmonyPatch(typeof(GameBootstrap), "Start")]
[HarmonyPriority(Priority.VeryHigh)]
public class GameBootstrap_Patch
{
    public static void Postfix()
    {
        var api = (CommandsAPI)Plugin.Instance.API;
        
        api.OnStartup();
    }
}