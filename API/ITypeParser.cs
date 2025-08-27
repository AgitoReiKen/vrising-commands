using System;

namespace Commands.API;

public interface ITypeParser
{
    public bool Parse(string input, out object? parsed);
    public string? GetRules(ulong platformId);
}

 