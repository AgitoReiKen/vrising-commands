using Commands.API;
using Commands.Resources;
using Commands.Utils;

namespace Commands.TypeParsers;

public class TimeSpanParser  : ITypeParser
{
    public bool Parse(string input, out object? parsed)
    {
        if (input.TryParseTimespan(out var time))
        {
            parsed = time;
            return true;
        }

        parsed = null;
        return false; 
    }
    public string? GetRules(ulong platformId)
    {
        return Localized.ParsingRules_TimeSpan(platformId);
    }

}