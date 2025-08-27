using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using BepInEx;
using Commands.API;
using Commands.Utils;
using Il2CppSystem.Linq;
using Unity.Collections;
using UnityEngine;

namespace Commands.Core;
public class Config
{
    public List<string> Prefixes;
    public List<string> PluginDelimiters;
    public Dictionary<string, List<string>> PluginAliases;

    public Dictionary<string, JObject> MiddlewareTemplates;
    public JObject? HelpCommand;
    public JObject? Test1Command;
    public JObject? Test2Command;
    public Dictionary<string, JObject> InfoCommands;
    public Config(JObject json)
    {
        // Check if " and ' are not used for prefixes or delimeters

        Prefixes = new();
        PluginDelimiters = new();
        PluginAliases = new();
        MiddlewareTemplates = new();
        HelpCommand = null;
        InfoCommands = new();
        var prefixes = json["Prefixes"].Cast<JArray>().Values<string>().ToList();
        foreach (var prefix in prefixes)
        {
            if (ContainsComma(prefix))
            {
                Log.Warning("Ignored \", \' commas in command prefix");
                continue;
            }
            
            Prefixes.Add(prefix);
        }

        var delimiters = json["PluginDelimiters"].Cast<JArray>().Values<string>().ToList();
        foreach (var delim in delimiters)
        {
            if (ContainsComma(delim))
            {
                Log.Warning("Ignored \", \' commas in plugin delimiter");
                continue;
            }

            PluginDelimiters.Add(delim);
        }

        var pluginAliases = json["PluginAliases"].Cast<JObject>().Properties().ToList();
        foreach (var plugin in pluginAliases)
        {
            var id = plugin.Name;
            List<string> aliases = new();
            
            plugin.Value.TryParseIntoStringArray(aliases);
            
            foreach (var alias in aliases.Where(alias => !IsConflictingAlias(alias)))
            {
                if (PluginAliases.ContainsKey(id))
                {
                    PluginAliases[id].Add(alias);   
                }
                else
                {
                    PluginAliases.Add(id, new List<string>(new [] { alias }));
                }
            }
        }
        
        if (Prefixes.Count == 0)
        {
            Prefixes.Add(".");
        }

        if (PluginDelimiters.Count == 0)
        {
            PluginDelimiters.Add("!");
        }

        if (json.TryGetValue("MiddlewareTemplates", out JToken middlewareTemplates))
        {
            var props = middlewareTemplates.Cast<JObject>().Properties().ToArray();
            for (int i = 0; i < props.Count; ++i)
            {
                var mw = props[i];
                if (mw.Value.Type != JTokenType.Object)
                {
                    throw new Exception($"Unsupported MiddlewareTemplate type {mw.Value.Type.ToString()} found for {mw.Name}. Only Object is supported right now");
                }
                if (mw.Value.Cast<JObject>().ContainsKey("Template"))
                {
                    Log.Error($"Template {mw.Name} should not contain nested Template");
                    continue;
                }
                MiddlewareTemplates.Add(mw.Name, mw.Value.Cast<JObject>());
            }
        }

        if (json.TryGetValue("Commands", out JToken _commands))
        {
            var commands = _commands.Cast<JObject>();
            if (commands.TryGetValue("Help", out JToken help))
            {
                HelpCommand = help.Cast<JObject>();
            }
            
            if (commands.TryGetValue("Test1", out JToken test1))
            {
                Test1Command = test1.Cast<JObject>();
            }
            
            if (commands.TryGetValue("Test2", out JToken test2))
            {
                Test2Command = test2.Cast<JObject>();
            } 
        }

        if (json.TryGetValue("InfoCommands", out JToken _infoCommands))
        {
            var infoCommands = _infoCommands.Cast<JObject>();
            var properties = infoCommands.Properties().ToArray();
            for (int i = 0; i < properties.Count; ++i)
            {
                InfoCommands.Add(properties[i].Name, properties[i].Value.Cast<JObject>());
            }
        }

    }

    public static Config Load()
    {
        var configPath = $"{Paths.ConfigPath}/{MyPluginInfo.PLUGIN_GUID}/config.json";
        string configText;
        try
        {
            configText = File.ReadAllText(configPath);
        }
        catch (Exception)
        {
            Log.Error($"Couldn't read config at {configPath}");
            throw;
        }

        JObject json;
        try
        {
            json = JObject.Parse(configText);
        }
        catch (Exception)
        {
            Log.Error("Couldn't parse config");
            throw;
        }

        return new Config(json);
    }
    private bool IsConflictingAlias(string alias)
    {
        foreach (var pa in PluginAliases)
        {
            foreach (var a in pa.Value)
            {
                if (alias.Equals(a, StringComparison.OrdinalIgnoreCase))
                {
                    Log.Error($"Found conflicting aliases {alias} and {a}. Alias {a} will be ignored");
                    return true;
                }
            }
        }

        return false;
    }
    private bool ContainsComma(string text)
    {
        return text.Contains("\"") || text.Contains('\'');
    }
}