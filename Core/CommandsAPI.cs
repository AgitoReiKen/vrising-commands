using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Commands.API;
using Commands.Resources;
using Commands.TypeParsers;
using Commands.Utils;
using F23.StringSimilarity;
using Il2CppSystem.Linq;
using Newtonsoft.Json.Linq;
using Unity.Collections;

namespace Commands.Core;

public class CommandsAPI : ICommandsAPI
{
    private const double MaxDistanceForSimilarity = 0.5;
     
    private Dictionary<string, List<CommandData>> Commands;
    private Dictionary<string, ICommandMiddlewareProvider> MiddlewareProviders;
    private Dictionary<Type, ITypeParser> TypeParsers;
    private Dictionary<string, ICommandMiddleware> MiddlewareTemplates;
    public Config Config;
    public CommandsAPI()
    {
        Config = Core.Config.Load();
        Commands = new();
        MiddlewareTemplates = new();
        TypeParsers = new();
        
        Type[] genericTypes =
        {
            typeof(char),
            typeof(sbyte), typeof(byte),
            typeof(short), typeof(ushort),
            typeof(int), typeof(uint),
            typeof(long), typeof(ulong),
            typeof(float), typeof(double), typeof(decimal)
        };
       
        foreach (var type in genericTypes)
        {
            var parserType = typeof(GenericParser<>).MakeGenericType(type);
            var parserInstance = Activator.CreateInstance(parserType);
            if (parserInstance == null)
            {
                throw new Exception($"GenericParser<{type}> was not created");
            }
            TypeParsers.Add(type, (ITypeParser)parserInstance);
        }
        
        TypeParsers.Add(typeof(bool), new BoolParser());
        TypeParsers.Add(typeof(string), new StringParser());
        TypeParsers.Add(typeof(TimeSpan), new TimeSpanParser());
        
        MiddlewareProviders = new();
    }

    /*
     * Returns plugin alias if exists
     */
    public string GetUserFriendlyPluginAlias(string pluginId)
    {
        if (Config.PluginAliases.ContainsKey(pluginId) && Config.PluginAliases[pluginId].Count > 0)
        {
            return Config.PluginAliases[pluginId].First();
        }

        return pluginId;
    }
    
    public CommandData? GetCommandData(CommandLookup lookup)
    {
        return Commands[lookup.PluginId].FirstOrDefault(x => x.UniqueId == lookup.CommandId);
    }
    
    /*
     * Tries to parse command from user input
     * .command "spaced param1" param2 ...
     * .plugin!command param1 param2 ...
     */
    public ParsedCommand? ParseCommand(string input)
    {
        ParsedCommand? command = null;
        string? commandPrefix = null;
        foreach (var p in Config.Prefixes)
        {
            if (input.StartsWith(p))
            {
                commandPrefix = p;
                break;
            }
        }

        if (commandPrefix == null)
        {
            // Not a command, skipping
            return command;
        }

        // Split command into words, preserving quotes
        var matches = Regex.Matches(input, @"[\""].+?[\""]|\S+");
        var tokens = matches
            .Select(m => m.Value.Trim().Trim('"'))
            .ToList();
    
        string rawCommand = tokens[0];

        // Make sure to remove double prefixes like .. or !!
        while (rawCommand.StartsWith(commandPrefix))
        {
            rawCommand = rawCommand.Substring(commandPrefix.Length);
        }
        
        string? pluginDelim = null;
        foreach (var d in Config.PluginDelimiters)
        {
            if (rawCommand.Contains(d))
            {
                pluginDelim = d;
                break;
            }
        }

        string? commandName = null;
        string? pluginId = null;
        
        // Executes command from plugin
        if (pluginDelim != null)
        {
            var parts = rawCommand.Split(pluginDelim, 2);
            pluginId = parts[0];
            commandName = parts[1];
        }
        else
        {
            commandName = rawCommand;
        }

        List<string> args = new();
        if (tokens.Count > 1)
        {
            args.AddRange(tokens.GetRange(1, tokens.Count - 1));
        }
        command = new ParsedCommand(pluginId,  commandName, args);
        return command;
    }

    /*
     * Searches for exact PluginId or one of its Aliases.
     * Performs a check if such plugin has any commands.
     * Returns true if exact plugin or exact alias is found and has plugin has registered any commands assigned.
     * Returns false if plugin either not found or doesn't have any commands registered
     */
    public bool TryToFindPlugin(string input, out string? pluginId)
    {
        pluginId = null;
        // If input is plugin id
        if (Commands.ContainsKey(input))
        {
            pluginId = input;
            return true;
        }

        // If input is alias
        foreach (var plugin in Config.PluginAliases)
        {
            foreach (var a in plugin.Value)
            {
                if (input.Equals(a, StringComparison.OrdinalIgnoreCase) &&
                    Commands.ContainsKey(plugin.Key))
                {
                    pluginId = plugin.Key;
                    return true;
                }
            }
        }

        return false;
    }
    
    // FindAllCandidates returns true when command name fully matches an alias
    // While searching, fills aliasByDistance so later it can be shown to user if his query can not be executed
    private bool FindCandidate(CommandEnvironment env, string pluginId, string commandName, 
        out CommandLookup? candidate, out List<KeyValuePair<string, double>> aliasByDistance)
    {
        candidate = null;
        aliasByDistance = new();
        var datas = Commands[pluginId]; 
        foreach (var data in datas)
        {
            if (!data.Environment.Contains(env)) continue;
            foreach (var a in data.Aliases)
            {
                if (commandName.Equals(a, StringComparison.OrdinalIgnoreCase))
                {
                    candidate = new CommandLookup(pluginId, data.UniqueId);
                    return true;
                }

                if (commandName.IsSimilarTo(a, MaxDistanceForSimilarity, out double distance))
                {
                    aliasByDistance.Add(new KeyValuePair<string, double>(a, distance));
                }
                Log.Debug($"\"{commandName}\" similarity distance to \"{a}\" is {distance}");
            }
        }

        return false;
    }
 
    /*
     * if exactCandidates has 0 item, then we didnt find exact command. Show similarCandidates to the user
     * if exactCandidates has 1 item, then we found exact command. Execute the command
     * if exactCandidates has more items, then we can not decide what command to use. Show exactCandidates to the user
     *
     * if similarCandidates is empty, show "no commands found" message to the user
     * similarCandidates (pluginId to similar aliases):
     * PluginA: Candidate1, Candidate2, Candidate3
     * PluginB: Candidate1, Candidate2
     *
     */
    public void FindCommand(ulong platformId, CommandEnvironment env, ParsedCommand input, out List<CommandLookup> exactCandidates,
        out Dictionary<string, HashSet<string>> similarCandidates)
    {
        List<KeyValuePair<string, double>> aliasByDistance;
        CommandLookup? candidate;
        similarCandidates = new();
        exactCandidates = new();
        
        // If user tries to call command from specific plugin
        if (input.PluginId != null)
        {
            // If it exists, try to find command candidates there
            if (TryToFindPlugin(input.PluginId, out string? pluginId))
            {
                if (FindCandidate(env, pluginId!, input.CommandName!, out candidate, out aliasByDistance))
                {
                    exactCandidates.Add(candidate!);
                    return;
                }
                
                if (aliasByDistance.Count > 0)
                {
                    aliasByDistance.Sort((x, y)=>x.Value.CompareTo(y.Value));
                    similarCandidates.Add(pluginId!, new HashSet<string>(aliasByDistance.Select(x=>x.Key)));
                }
            
                // If no candidate found, try to find command candidates in other plugins
                foreach (var plugin in Commands.Keys.Where(x=>!x.Equals(pluginId))) 
                {
                    if (FindCandidate(env, plugin, input.CommandName!, out candidate, out aliasByDistance))
                    {
                        exactCandidates.Add(candidate!);
                    }
                    else
                    {
                        if (aliasByDistance.Count > 0)
                        {
                            aliasByDistance.Sort((x, y) => x.Value.CompareTo(y.Value));
                            similarCandidates.Add(plugin, new HashSet<string>(aliasByDistance.Select(x => x.Key)));
                        }
                    }
                }   
                
                return;
            }
            
            // If plugin doesn't exist, try to find command candidates from all plugins
            foreach (var plugin in Commands.Keys) 
            {
                if (FindCandidate(env, plugin, input.CommandName!, out candidate, out aliasByDistance))
                {
                    exactCandidates.Add(candidate!);
                }
                else
                {
                    if (aliasByDistance.Count > 0)
                    {
                        aliasByDistance.Sort((x, y) => x.Value.CompareTo(y.Value));
                        similarCandidates.Add(plugin, new HashSet<string>(aliasByDistance.Select(x => x.Key)));
                    }
                }
            }

            return;
        }
        // Try to find command candidates from all plugins
        foreach (var plugin in Commands.Keys)
        {
            if (FindCandidate(env, plugin, input.CommandName!, out candidate, out aliasByDistance))
            {
                exactCandidates.Add(candidate!);
            }
            else
            {
                if (aliasByDistance.Count > 0)
                {
                    aliasByDistance.Sort((x, y) => x.Value.CompareTo(y.Value));
                    similarCandidates.Add(plugin, new HashSet<string>(aliasByDistance.Select(x => x.Key)));
                }
            }
        }
    }
    
    public static string GetPrintableCommandParameter(ulong platformId, ParameterInfo parameterInfo)
    {
        string typeStr = GetPrintableTypeName(platformId, parameterInfo.ParameterType);
        var paramName = parameterInfo.Name != null ? GetUserFriendlyParameterName(parameterInfo.Name) : "paramName";
        return parameterInfo.HasDefaultValue ? 
            Localized.OptionalParameterFormat(platformId).Replace("{name}", paramName).Replace("{type}", typeStr) :
            Localized.ParameterFormat(platformId).Replace("{name}", paramName).Replace("{type}", typeStr) ;
    }

    public static string GetPrintableTypeName(ulong platformId, Type originalType)
    {
        var nullableType = Nullable.GetUnderlyingType(originalType);
        var type = nullableType ?? originalType;
        string typeStr;
        
        if (type == typeof(bool)) typeStr = Localized.Type_Bool(platformId);
        else if (IsNumericType(type)) typeStr = Localized.Type_Numeric(platformId);
        else if (type == typeof(string)) typeStr = Localized.Type_String(platformId);
        else if (type == typeof(char)) typeStr = Localized.Type_Char(platformId);
        else if (type == typeof(TimeSpan)) typeStr = Localized.Type_TimeSpan(platformId);
        else typeStr = Localized.Type_Custom(platformId).Replace("{type}", type.Name);
        return typeStr;
    }
    private static bool IsNumericType(Type type)
    {
        return type == typeof(byte) || type == typeof(sbyte) ||
               type == typeof(short) || type == typeof(ushort) ||
               type == typeof(int) || type == typeof(uint) ||
               type == typeof(long) || type == typeof(ulong) ||
               type == typeof(float) || type == typeof(double) ||
               type == typeof(decimal);
    }
    
    // playerName, player_name as input will result to Player Name
    public static string GetUserFriendlyParameterName(string originalName)
    {
        // Replace underscores with spaces (for snake_case)
        originalName = originalName.Replace("_", " ");

        // Insert spaces before capital letters (for camelCase)
        var withSpaces = Regex.Replace(originalName, @"(?<!^)([A-Z])", " $1");

        // Capitalize each word
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(withSpaces.ToLower());
    }
    public string? GetPrintableCommandSignature(ulong platformId, CommandData data)
    {
        if (data.Aliases.Count == 0) return null;

        // command [Player Id: Number] [Player Name: Text]

        StringBuilder str = new();
        str.Append($"{Config.Prefixes.First()}{data.Aliases.First()} ");

        var parameters = data.Handler.Method.GetParameters().Skip(1);
        
        str.AppendJoin(" ", parameters.Select(x => GetPrintableCommandParameter(platformId, x)));
        
        return str.ToString();
    }

    public bool ExecuteCommand(CommandContext context, CommandLookup lookup, List<string> args, out string? message)
    {
        message = null;
        var data = GetCommandData(lookup);
        if (data == null)
        {
            message = "Couldn't find command";
            return false;
        }

        context.CommandId = data.UniqueId;
        ulong platformId = context.GetPlatformId() ?? 0;
        StringBuilder msgBuilder = new();
        foreach (var mw in data.Middleware)
        {
            bool result = mw.Prefix(context, out message);
            if (message != null)
            {
                msgBuilder.AppendLine(message);
            }
            if (!result)
            {
                message = msgBuilder.ToString();
                return false;
            }
        }
        
        var argInfos = data.Handler.Method.GetParameters();
        object[] invokeArgs = new object[argInfos.Length];
        invokeArgs[0] = context;
        
        for (int i = 1; i < argInfos.Length; ++i)
        {
            var argIt = i - 1;
            var info = argInfos[i];

            if (args.Count > argIt)
            {
                var type = Nullable.GetUnderlyingType(info.ParameterType) ?? info.ParameterType;
                if (!TypeParsers.TryGetValue(type, out var parser))
                {
                    Log.Error($"Type Parser is not registered for parameter \"{type} {info.Name}\" " +
                              $"(Method {data.Handler.Method.Name}, Plugin {lookup.PluginId})");
                    msgBuilder.AppendLine(Localized.InternalError(platformId));
                    message = msgBuilder.ToString();
                    return false;
                }
                
                if (!parser.Parse(args[argIt], out var value))
                {
                    string parsingError = Localized.ParsingError(platformId)
                        .Replace("{input}", args[argIt])
                        .Replace("{type}", GetPrintableTypeName(platformId, type));    
                    msgBuilder.AppendLine(parsingError);
                    msgBuilder.AppendLine(parser.GetRules(platformId));
                    message = msgBuilder.ToString();
                    return false;
                }

                invokeArgs[i] = value!;
            }
            else if (info.HasDefaultValue)
            {
                invokeArgs[i] = info.DefaultValue!;
            }
            else
            {
                var sign = GetPrintableCommandSignature(platformId, data);
                msgBuilder.AppendLine(Localized.NotEnoughArguments(platformId));
                if (sign != null) msgBuilder.AppendLine(sign);
                message = msgBuilder.ToString();
                return false;
            } 
        }

        var returnsMessage = data.Handler.Method.ReturnType == typeof(string);
        try
        {
            var m = data.Handler.Method.Invoke(null, invokeArgs);
            if (returnsMessage && m != null)
            {
                msgBuilder.AppendLine((string)m);
                message = msgBuilder.ToString();
            }
        }
        catch (Exception ex)
        {
            Log.Error($"Command {data.UniqueId} thrown an exception for user {platformId}");
            if (args.Count != 0)
            {
                string argsStr = String.Join(" ", args);
                Log.Error($"Args: {argsStr}");
            }
            Log.Error(ex.Message);
            msgBuilder.AppendLine(Localized.InternalError(platformId));
            message = msgBuilder.ToString();
            return false;
        }
        return true;
    }
    
    public bool RegisterCommand(string pluginId, CommandData commandData)
    {
        Log.Info($"[Commands] RegisterCommand {pluginId} - {commandData.UniqueId}");
        if (Commands.TryGetValue(pluginId, out var commands))
        {
            commands.RemoveAll(x => x.UniqueId == commandData.UniqueId);
            commands.Add(commandData);
        }
        else
        {
            Commands[pluginId] = new List<CommandData>(new[] { commandData });
        }

        return true;
    }

    public bool UnregisterCommand(string pluginId, string commandId)
    {
        if (Commands.TryGetValue(pluginId, out var commands))
        {
            commands.RemoveAll(x => x.UniqueId == commandId);
        }
        
        return true;
    }

    public void RegisterMiddlewareProvider(string middlewareId, ICommandMiddlewareProvider provider)
    {
        MiddlewareProviders[middlewareId] = provider;
    }

    public ICommandMiddlewareProvider? GetMiddlewareProvider(string middlewareId)
    {
        MiddlewareProviders.TryGetValue(middlewareId, out var value);
        return value;
    }

    public JObject? GetMiddlewareTemplate(string templateId)
    {
         Config.MiddlewareTemplates.TryGetValue(templateId, out var value);
         return value;
    }

    public ICommandMiddleware ResolveMiddlewareTemplateById(string templateId)
    {
        var template = GetMiddlewareTemplate(templateId);
        if (template == null) throw new Exception($"Couldn't find template with id {templateId}");
        return ResolveMiddlewareTemplate(template);
    }

    public ICommandMiddleware ResolveMiddlewareTemplate(JObject template)
    {
        if (!template.TryGetValue("Id", out JToken _id))
        {
            throw new Exception("[ResolveMiddlewareTemplate] Template lacks Id field");
        }

        var id = (string)_id;
        if (!MiddlewareProviders.TryGetValue(id, out var provider))
        {
            throw new Exception($"[ResolveMiddlewareTemplate] No middleware provider registered for id {id}");
        }

        return provider.Create(template);
    }

    public ICommandMiddleware[] ResolveMiddleware(JToken middleware)
    {
        /*
         * Supports:
         * "Middleware":"template-a, template-b, template-c"
         *
         * Policy: Each template should provide complete middleware object
         */
        List<ICommandMiddleware> mws = new();
        
        if (middleware.Type == JTokenType.String)
        {
            var templates = ((string)middleware).Split(",", StringSplitOptions.TrimEntries);
            foreach (var template in templates)
            {
                var templateObj = GetMiddlewareTemplate(template);
                if (templateObj == null)
                {
                    throw new Exception($"Couldn't find template with id {template}");
                }
                
                if (!templateObj.TryGetValue("Id", out JToken _tid))
                {
                    throw new Exception(
                        $"Template {template} doesn't have Id of middleware defined in it when being \"inlined\" (\"Middleware\":\"template\")");
                }
                
                var id = (string)_tid;
                var provider = GetMiddlewareProvider(id);
                if (provider == null)
                {
                    throw new Exception($"Middleware provider for Id {id} is not registered");
                }

                try
                {
                    var mw = provider.Create(templateObj);
                    mws.Add(mw);
                }
                catch (Exception)
                {
                    Log.Error($"Couldn't parse middleware with id {id}");
                    throw;
                }
                
            }

            return mws.ToArray();
        }

        if (middleware.Type != JTokenType.Array)
        {
            throw new Exception($"Unsupported middleware json type {middleware.Type}.");
        }
         
        var middlewareJson = middleware.Cast<JArray>().Values<JObject>().ToArray();
        for (int i = 0; i < middlewareJson.Count; ++i)
        {
            var mwJson = middlewareJson[i];
            /*
             * Supports:
             * {"Template":"Middleware-A", "MiddlewareAData":123}
             *
             * Policy: Merges each template object with the base one, so templates are treated as partial middleware data
             */
            if (mwJson.TryGetValue("Template", out JToken _template))
            {
                var templates = ((string)_template).Split(",", StringSplitOptions.TrimEntries);
                mwJson.RemoveItem(_template);
                foreach (var template in templates)
                {
                    var templateObj = GetMiddlewareTemplate(template);
                    if (templateObj == null)
                    {
                        throw new Exception($"Couldn't find template with id {template}");
                    }
                
                    mwJson.Merge(templateObj);
                }
            }
            /*
             * Supports:
             * {"Id":"Middleware"}
             *
             * Policy: Straight middleware parsing
             */
            if (mwJson.TryGetValue("Id", out JToken _id))
            {
                var id = (string)_id;
                var provider = GetMiddlewareProvider(id);
                if (provider == null)
                {
                    throw new Exception($"Middleware provider for Id {id} is not registered");
                }

                try
                {
                    var mw = provider.Create(mwJson);
                    mws.Add(mw);
                }
                catch (Exception)
                {
                    Log.Error($"Couldn't parse middleware with id {id}");
                    throw;
                }
            }
            else
            {
                throw new Exception("Couldn't resolve Middleware because it had no Id field");
            }

        }

        return mws.ToArray();
    }

    
    public ITypeParser GetTypeParser(Type type)
    {
        return TypeParsers[type];
    }

    public bool RegisterTypeParser(Type type, ITypeParser parser)
    {
        TypeParsers[type] = parser;
        return true;
    }

    public bool UnregisterTypeParser(Type type)
    {
        TypeParsers.Remove(type);
        return true;
    }

    public static bool CanSuggestCommand(CommandContext context, CommandData data)
    {
        return data.Aliases.Count > 0 &&
               data.Middleware.All(mw => mw.CanSuggest(context)) &&
               data.Environment.Contains(context.Environment);
    }

    public static string InfoCommand(CommandContext context)
    {
        ulong platformId = context.GetPlatformId() ?? 0;
        
        return Localized.Get(platformId, context.CommandId!);
    }

    public class CustomType
    {
        
    }
    public static string Test1Command(CommandContext context, 
        int number, 
        char symbol,
        bool boolean,
        TimeSpan time,
        string? optionalString = null,
        CustomType? customType = null)
    {
        return $"You've entered\n" +
               $"Number: {number}, " +
               $"Symbol: {symbol}, " +
               $"Boolean: {boolean}, " +
               $"Text: {optionalString}, " +
               $"Time: {time}";
    }

    public static void Test2Command(CommandContext context)
    {
        
    }
    public static string HelpCommand(CommandContext context, string? plugin = null)
    {
        var that = (CommandsAPI)Plugin.Instance.API;
        /*
         * Plugin:
         * 1
         * 2
         * Plugin:
         * 1
         * 2
         */
        ulong platformId = context.GetPlatformId() ?? 0;
        StringBuilder builder = new();
        if (plugin != null)
        {
            if (that.TryToFindPlugin(plugin, out var pluginId))
            {
                HashSet<string> allowedCommands = new();
                foreach (var command in that.Commands[pluginId!])
                {
                    if (CanSuggestCommand(context, command))
                    {
                        allowedCommands.Add(command.UniqueId);
                    }
                }

                if (allowedCommands.Count > 0)
                {
                    builder.Append(
                        Localized.HelpPluginFormat(platformId)
                            .Replace("{plugin}",that.GetUserFriendlyPluginAlias(pluginId!)));
                     foreach (var data in that.Commands[pluginId!].Where(x=>allowedCommands.Contains(x.UniqueId)))
                    {
                        builder.AppendLine(that.GetPrintableCommandSignature(platformId, data));
                    }

                    return builder.ToString();
                }
            }
            else
            {
                HashSet<string> plugins = new();

                foreach (var p in that.Commands)
                {
                    bool any = p.Value.Any(command => CanSuggestCommand(context, command));
                    if (any) plugins.Add(that.GetUserFriendlyPluginAlias(p.Key));
                }

                if (plugins.Count > 0)
                {
                    return Localized.ShowSimilarCandidates(platformId).Replace("{candidates}", String.Join(", ", plugins));
                }
            }
        }
        
        foreach (var p in that.Commands)
        {
            string pluginId = p.Key;
            if (p.Value.Count == 0) continue;

            HashSet<string> allowedCommands = new();
            foreach (var command in p.Value)
            {
                if (CanSuggestCommand(context, command))
                {
                    allowedCommands.Add(command.UniqueId);
                }
            }

            if (allowedCommands.Count == 0) continue;
            builder.AppendLine(
                Localized.HelpPluginFormat(platformId)
                    .Replace("{plugin}",that.GetUserFriendlyPluginAlias(pluginId)));

            foreach (var data in p.Value.Where(x=>allowedCommands.Contains(x.UniqueId)))
            {
                builder.AppendLine(that.GetPrintableCommandSignature(platformId, data));
            }

            builder.AppendLine();
        }

        var str = builder.ToString();
        if (str.Length == 0)
        {
            str = "No commands found";
        }
        return str;
    }

    public void OnStartup()
    {
        foreach (var template in Config.MiddlewareTemplates)
        {
            string id;
            if (!template.Value.TryGetValue("Id", out var _id))
            {
                Log.Error($"Template {template.Key} doesn't have Id in its object");
                continue;
            }
            id = (string)_id;

            if (!MiddlewareProviders.ContainsKey(id))
            {
                Log.Error($"There is no middleware provider found for id {id}");
                continue;
            }

            try
            {
                ICommandMiddleware mw = MiddlewareProviders[id].Create(template.Value);
                MiddlewareTemplates.Add(template.Key, mw);
            }
            catch (Exception)
            {
                Log.Error($"Failed to create {id} middleware.");
                throw;
            }
        }
        
        if (Config.HelpCommand != null)
        {
            RegisterCommand(MyPluginInfo.PLUGIN_GUID, new CommandData("Help", HelpCommand, Config.HelpCommand));
        }

        if (Config.Test1Command != null)
        {
            RegisterCommand(MyPluginInfo.PLUGIN_GUID, new CommandData("Test1", Test1Command, Config.Test1Command));
        }
        
        if (Config.Test2Command != null)
        {
            RegisterCommand(MyPluginInfo.PLUGIN_GUID, new CommandData("Test2", Test2Command, Config.Test2Command));
        }

        foreach (var infoCommand in Config.InfoCommands)
        {
            RegisterCommand(MyPluginInfo.PLUGIN_GUID, new CommandData(infoCommand.Key, InfoCommand, infoCommand.Value));
        }
        
        LocalizationWrapper.RegisterCommands();
    }
}

public static class StringExtensions
{
    public static bool StartsWithAny(this string that, HashSet<string> strings, StringComparison comparison = StringComparison.Ordinal)
    {
        foreach (var str in strings)
        {
            if (that.StartsWith(str, comparison)) return true;
        }

        return false;
    }   
    public static bool ContainsAny(this string that, HashSet<string> strings, StringComparison comparison = StringComparison.Ordinal)
    {
        foreach (var str in strings)
        {
            if (that.Contains(str, comparison)) return true;
        }

        return false;
    }
}