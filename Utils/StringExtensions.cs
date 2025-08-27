using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using F23.StringSimilarity;

namespace Commands.Utils;

public static class StringExtensions
{
    /*
     * Accepts format like: "1d 23h 59m 59s"
     */
    public static bool TryParseTimespan(this string that, out TimeSpan? timespan)
    {
        var pattern = @"(?:(?<days>\d+)d)?\s*(?:(?<hours>\d+)h)?\s*(?:(?<minutes>\d+)m)?\s*(?:(?<seconds>\d+)s)?";
        var match = Regex.Match(that, pattern, RegexOptions.IgnoreCase);

        if (!match.Success)
        {
            timespan = null;
            return false;
        }
        
        int days = match.Groups["days"].Success ? int.Parse(match.Groups["days"].Value) : 0;
        int hours = match.Groups["hours"].Success ? int.Parse(match.Groups["hours"].Value) : 0;
        int minutes = match.Groups["minutes"].Success ? int.Parse(match.Groups["minutes"].Value) : 0;
        int seconds = match.Groups["seconds"].Success ? int.Parse(match.Groups["seconds"].Value) : 0;

        timespan = new TimeSpan(days, hours, minutes, seconds);
        return true;
    }
    public static bool IsSimilarTo(this string source, string other, double maxDistance, out double distance)
    {
        var lev = new NormalizedLevenshtein();
        distance = lev.Distance(source, other);
        return distance <= maxDistance;
    }
    
}