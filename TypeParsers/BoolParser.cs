using System;
using System.Collections.Generic;
using Commands.API;
using Commands.Resources;

namespace Commands.TypeParsers;

public class BoolParser : ITypeParser
{
    public static System.Type Type => typeof(bool);

    private static readonly HashSet<string> TrueValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "true", "1", "yes", "on", "enabled", "enable", "+"
    };

    private static readonly HashSet<string> FalseValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "false", "0", "no", "off", "disabled", "disable", "-"
    };

    public bool Parse(string input, out object? parsed)
    {
        if (TrueValues.Contains(input))
        {
            parsed = true;
            return true;
        }

        if (FalseValues.Contains(input))
        {
            parsed = false;
            return true;
        }

        parsed = null;
        return false;
    }
    
    public string? GetRules(ulong platformId)
    {
        return Localized.ParsingRules_Bool(platformId);
    }
}