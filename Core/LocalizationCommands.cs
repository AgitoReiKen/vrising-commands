using System;
using System.Linq;
using System.Text;
using Commands.API;
using Commands.Utils;
using Localization.Core;
using Localization.Resources;

namespace Commands.Core;

public static class LocalizationWrapper
{
    public static LocalizationAPI LocalizationAPI => (LocalizationAPI)Localization.Plugin.Instance.API;
    public static string ReloadCommand(CommandContext context, string? pluginId = null)
    {
        string? pluginPath = null;
        if (pluginId != null && !LocalizationAPI.PluginRegistrationHistory.TryGetValue(pluginId, out pluginPath))
        {
            Log.Warning($"[Localization] Registered plugins ids: {String.Join(", ", LocalizationAPI.PluginRegistrationHistory.Keys)}");
            return $"ReloadCommand used with plugin id that wasn't registered before {pluginId}. Registered Plugins has been printed in the console.";
        }
        if (pluginPath != null)
        {
            if (LocalizationAPI.RegisterPlugin(pluginId!, pluginPath))
            {
                return "Success";
            }
            return "Reload failed. Check the console";
        }
        
        if (LocalizationAPI.PluginRegistrationHistory.Any(
                x=>!LocalizationAPI.RegisterPlugin(x.Key, x.Value))
            )
        {
            return "Reload partially or completely failed. Check the console";
        }
        return "Success";
    }
    public static string LocaleCommand(CommandContext context, string? locale = null)
    {
        var registeredLocales = LocalizationAPI.GetRegisteredLocales();
        ulong platformId = context.GetPlatformId() ?? 0;
        if (locale == null)
        {
            return Localized.Locales(platformId).Replace("{locales}", String.Join(", ", registeredLocales));
        }
    
        string? localeId = null;
        foreach (var regLocale in registeredLocales)
        {
            if (regLocale.Equals(locale, StringComparison.OrdinalIgnoreCase))
            {
                localeId = regLocale;
                break;
            }
    
            var localeAliases = LocalizationAPI.GetLocaleAliases(regLocale);
            if (localeAliases == null) continue;
            bool found = false;
            foreach (var localeAlias in localeAliases)
            {
                if (localeAlias.Equals(locale, StringComparison.OrdinalIgnoreCase))
                {
                    found = true;
                    localeId = localeAlias;
                    break;
                }
            }
    
            if (found) break;
        }
    
        if (localeId == null)
        {
            StringBuilder builder = new();
            builder.AppendLine(Localized.LocaleNotFound(platformId).Replace("{input}", locale));
            builder.AppendLine(Localized.Locales(platformId).Replace("{locales}",
                String.Join(", ", registeredLocales)));
            return builder.ToString();
        }
    
        if (LocalizationAPI.SetUserLocale(platformId, localeId))
        {
            return Localized.LocaleSet(platformId);
        }
        throw new Exception($"Failed to set locale for user {platformId}, locale {localeId}");
    }
    public static void RegisterCommands()
    { 
        try
        {
            if (LocalizationAPI.Config.LocaleCommand != null)
            {
                Plugin.Instance.API.RegisterCommand(Localization.MyPluginInfo.PLUGIN_GUID,
                    new CommandData("Locale", LocaleCommand, LocalizationAPI.Config.LocaleCommand!));
            }
        }
        catch (Exception ex)
        {
            Log.Error("[Localization] Couldn't register Locale command");
            Log.Error(ex.Message);
        }
        
        try
        {
            if (LocalizationAPI.Config.ReloadCommand != null)
            {
                Plugin.Instance.API.RegisterCommand(Localization.MyPluginInfo.PLUGIN_GUID,
                    new CommandData("Reload", ReloadCommand, LocalizationAPI.Config.ReloadCommand!));
            }
        }
        catch (Exception ex)
        {
            Log.Error("[Localization] Couldn't register Reload command");
            Log.Error(ex.Message);
        }
    }
}