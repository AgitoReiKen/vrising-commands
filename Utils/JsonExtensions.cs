using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Il2CppSystem.Linq;
using Newtonsoft.Json.Linq;

namespace Commands.Utils;

public static class JsonExtensions
{
    public static bool TryParseIntoStringArray(this JToken token, System.Collections.Generic.List<string> result)
    {
        if (token.Type == JTokenType.Array)
        {
            var array = token.Cast<JArray>().Values<string>().AsEnumerable().ToList();
            for (int i = 0; i < array.Count; ++i)
            {
                var str = array._items[i];
                result.Add(str);
            }

            return true;
        }
        
        if (token.Type == JTokenType.String)
        {
            var array = ((string)token).Split(',', StringSplitOptions.TrimEntries).ToList();
            result.AddRange(array);
            return true;
        }
        return false;
    }
    public static bool TryParseIntoStringHashset(this JToken token, System.Collections.Generic.HashSet<string> result)
    {
        if (token.Type == JTokenType.Array)
        {
            var array = token.Cast<JArray>().Values<string>().AsEnumerable().ToList();
            for (int i = 0; i < array.Count; ++i)
            {
                var str = array._items[i];
                result.Add(str);
            }

            return true;
        }
        
        if (token.Type == JTokenType.String)
        {
            var hashset = ((string)token).Split(',', StringSplitOptions.TrimEntries).ToHashSet();
            result.UnionWith(hashset);
            return true;
        }
        return false;
    }
 
     
}