using System;
using System.Reflection;
using Commands.API;
using Commands.Resources;

namespace Commands.TypeParsers;

public class GenericParser<T> : ITypeParser
{
    public static System.Type Type => typeof(T);
    // Finds: public static bool TryParse(string, out T)
    private static readonly MethodInfo? TryParse = Type.GetMethod(
        "TryParse",
        BindingFlags.Public | BindingFlags.Static,
        null,
        new[] { typeof(string), typeof(T).MakeByRefType() },
        null);
    
    static GenericParser()
    {
        System.Diagnostics.Debug.Assert(TryParse != null, $"[GenericParser] Type {Type.Namespace}.{Type.Name} doesn't support TryParse");
    }
    
    public bool Parse(string input, out object? parsed)
    {
        var parameters = new object[] { input, null! };
        
        if ((bool)(TryParse!.Invoke(null, parameters) ?? false))
        {
            parsed = parameters[1];
            return true;
        }

        parsed = null;
        return false;
    }

    public string? GetRules(ulong platformId)
    {
        return null;
    }
}
 
public class StringParser : ITypeParser
{
    public bool Parse(string input, out object? parsed)
    {
        parsed = input;
        return true;
    }
    public string? GetRules(ulong platformId)
    {
        return null;
    }
}
