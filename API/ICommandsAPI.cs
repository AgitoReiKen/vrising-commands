using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Commands.Core;
using Commands.Utils;
using Il2CppSystem.Linq;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;
using ProjectM.Network;

namespace Commands.API;

public enum CommandResult
{
    Succeeded,
    Failed,
    Denied
}

public enum CommandEnvironment
{
    Chat,
    Console,
    Rcon
}

public class CommandData
{
    public string UniqueId;
    public Delegate Handler;

    public HashSet<string> Aliases;
    public List<ICommandMiddleware> Middleware;
    public List<CommandEnvironment> Environment;

    private void AssertHandlerSignature()
    {
        if ((Handler.Method.Attributes & MethodAttributes.Static) == 0)
        {
            throw new Exception($"Command {UniqueId} should be static");
        }

        var parameters = Handler.Method.GetParameters();
        if (parameters.Length == 0)
        {
            throw new Exception($"Command {UniqueId} should have at least 1 parameter. Method(CommandContext context)");
        }
        if (parameters[0].ParameterType != typeof(CommandContext))
        {
            throw new Exception($"Command {UniqueId} should have CommandContext as its first parameter, found {parameters[0].ParameterType} instead");
        }

    }
    public CommandData(string uniqueId, Delegate handler, HashSet<string> aliases, List<ICommandMiddleware> middleware, List<CommandEnvironment> environment)
    {
        UniqueId = uniqueId;
        Handler = handler;
        AssertHandlerSignature();

        Aliases = aliases;
        Middleware = middleware;
        Environment = environment;
    }

    public CommandData(string uniqueId, Delegate handler, JObject json)
    {
        UniqueId = uniqueId;
        Handler = handler;
        AssertHandlerSignature();
         
        Aliases = new();
        Middleware = new();
        Environment = new(new CommandEnvironment[] { CommandEnvironment.Chat });

        if (json.TryGetValue("Environment", out JToken environment))
        {
            Environment.Clear();

            HashSet<string> envsStr = new();
            if (environment.TryParseIntoStringHashset(envsStr))
            {
                foreach (var envStr in envsStr)
                {
                    if (envStr.Equals("Chat", StringComparison.OrdinalIgnoreCase))
                    {
                        Environment.Add(CommandEnvironment.Chat);
                    }
                    else if (envStr.Equals("Console", StringComparison.OrdinalIgnoreCase))
                    {
                        Environment.Add(CommandEnvironment.Console);
                    }
                    else if (envStr.Equals("Rcon", StringComparison.OrdinalIgnoreCase))
                    {
                        Environment.Add(CommandEnvironment.Rcon);
                    }
                    else
                    {
                        Utils.Log.Error($"[CommandData] {uniqueId} - specified environment is not supported {envStr} (Chat, Console, Rcon)");
                    }
                }
            }
        }

        if (json.TryGetValue("Aliases", out JToken aliases))
        {
            if (!aliases.TryParseIntoStringHashset(Aliases))
            {
                Utils.Log.Error($"[CommandData] {uniqueId} - Couldn't parse Aliases");
            }
        }

        if (json.TryGetValue("Middleware", out JToken middleware))
        {
            Middleware.AddRange(Plugin.Instance.API.ResolveMiddleware(middleware));
        }
    }
}

public class ParsedCommand
{
    public string? PluginId = null;
    public string? CommandName;
    public List<string> Arguments = new();

    public ParsedCommand(string? pluginId, string commandName, List<string> args)
    {
        PluginId = pluginId;
        CommandName = commandName;
        Arguments = args;
    }
}
    
public class CommandLookup
{
    public readonly string PluginId;
    public readonly string CommandId;

    public CommandLookup(string pluginId, string commandId)
    {
        PluginId = pluginId;
        CommandId = commandId;
    }
}

public interface ICommandsAPI
{
    public ParsedCommand? ParseCommand(string input);
    public void FindCommand(ulong platformId, 
        CommandEnvironment env, ParsedCommand input,
        out List<CommandLookup> exactCandidates, out Dictionary<string, HashSet<string>> similarCandidates);

    
    public bool ExecuteCommand(CommandContext context, CommandLookup lookup, List<string> args, out string? message);
    public bool RegisterCommand(string pluginId, CommandData commandData);
    public bool UnregisterCommand(string pluginId, string commandId);

    public ITypeParser GetTypeParser(Type type);
    public bool RegisterTypeParser(Type type, ITypeParser parser);
    public bool UnregisterTypeParser(Type type);
    
    public void RegisterMiddlewareProvider(string middlewareId, ICommandMiddlewareProvider provider);
    public ICommandMiddlewareProvider? GetMiddlewareProvider(string middlewareId);

    public JObject? GetMiddlewareTemplate(string templateId);

    public ICommandMiddleware[] ResolveMiddleware(JToken middleware);

    // public ICommandMiddleware? ResolveMiddlewareTemplate(JObject template);
    // public ICommandMiddleware? ResolveMiddlewareTemplateById(string templateId);

}