using System.Runtime.CompilerServices;
using Localization.API;

namespace Commands.Resources;

public static class Localized
{
    public static string Type_Numeric(ulong p) => Get(p);
    public static string Type_Char(ulong p) => Get(p);
    public static string Type_String(ulong p) => Get(p);
    public static string Type_Bool(ulong p) => Get(p);
    public static string Type_TimeSpan(ulong p) => Get(p);
    public static string Type_Custom(ulong p) => Get(p);
    public static string HelpPluginFormat(ulong p) => Get(p);
    public static string OptionalParameterFormat(ulong p) => Get(p);
    public static string ParameterFormat(ulong p) => Get(p);
    public static string NotEnoughArguments(ulong p) => Get(p);
    public static string InternalError(ulong p) => Get(p);
    public static string ParsingError(ulong p) => Get(p);
    public static string ParsingRules_Bool(ulong p) => Get(p);
    public static string ParsingRules_TimeSpan(ulong p) => Get(p);
    public static string ShowSimilarCandidates(ulong p) => Get(p);
    public static string CommandNotFound(ulong p) => Get(p);
    public static string ShowExactCandidates(ulong p) => Get(p);
    
    public static string Get(ulong platformId, [CallerMemberName] string key = "")
    {
        return Localization.Plugin.Instance.API.GetLocalizedString(platformId, MyPluginInfo.PLUGIN_GUID, key);
    }
}