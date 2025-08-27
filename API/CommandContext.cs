using System;
using ProjectM.Network;

namespace Commands.API;

public class CommandContext
{
    public CommandEnvironment Environment;
    public string? CommandId;

    public CommandContext(CommandEnvironment environment)
    {
        Environment = environment;
    }

    public ulong? GetPlatformId()
    {
        switch (Environment)
        {
            case CommandEnvironment.Chat:
                return ((ChatCommandContext)this).User.PlatformId;
            case CommandEnvironment.Console:
                return ((ConsoleCommandContext)this).User.PlatformId;
            default:
                return null;
        }
    }
}
public class ChatCommandContext : CommandContext
{
    public User User;

    public ChatCommandContext(User user) : base(CommandEnvironment.Chat)
    {
        User = user;
    }
}
public class ConsoleCommandContext: CommandContext
{
    public User User;
    public ConsoleCommandContext(User user) : base(CommandEnvironment.Console)
    {
        User = user;
    }
}
public class RconCommandContext: CommandContext
{
    public RCONServerLib.RemoteConPacket Packet;
    public RconCommandContext(RCONServerLib.RemoteConPacket packet) : base(CommandEnvironment.Rcon)
    {
        Packet = packet;
    }
}