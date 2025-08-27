using Newtonsoft.Json.Linq;

namespace Commands.API;
 
public interface ICommandMiddleware
{
    public bool CanSuggest(CommandContext context)
    {
        return true;
    }
    public bool Prefix(CommandContext context, out string? message)
    {
        message = null;
        return true;
    }

    public void Postfix(CommandContext context, CommandResult result)
    {
    }

}

public interface ICommandMiddlewareProvider
{
    public ICommandMiddleware Create(JObject json);
}
