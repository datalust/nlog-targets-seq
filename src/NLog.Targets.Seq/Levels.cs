
using System;
using System.Collections.Generic;

namespace NLog.Targets.Seq
{
    internal static class Levels
    {
        static readonly Dictionary<string, (string, LogLevel)> LevelsByName =
            new Dictionary<string, (string, LogLevel)>(StringComparer.OrdinalIgnoreCase)
            {
                ["t"] = ("Trace", LogLevel.Trace),
                ["tr"] = ("Trace", LogLevel.Trace),
                ["trc"] = ("Trace", LogLevel.Trace),
                ["trce"] = ("Trace", LogLevel.Trace),
                ["trace"] = ("Trace", LogLevel.Trace),
                ["v"] = ("Verbose", LogLevel.Trace),
                ["ver"] = ("Verbose", LogLevel.Trace),
                ["vrb"] = ("Verbose", LogLevel.Trace),
                ["verb"] = ("Verbose", LogLevel.Trace),
                ["verbose"] = ("Verbose", LogLevel.Trace),
                ["d"] = ("Debug", LogLevel.Debug),
                ["de"] = ("Debug", LogLevel.Debug),
                ["dbg"] = ("Debug", LogLevel.Debug),
                ["deb"] = ("Debug", LogLevel.Debug),
                ["dbug"] = ("Debug", LogLevel.Debug),
                ["debu"] = ("Debug", LogLevel.Debug),
                ["debug"] = ("Debug", LogLevel.Debug),
                ["i"] = ("Information", LogLevel.Info),
                ["in"] = ("Information", LogLevel.Info),
                ["inf"] = ("Information", LogLevel.Info),
                ["info"] = ("Information", LogLevel.Info),
                ["information"] = ("Information", LogLevel.Info),
                ["notice"] = ("Notice", LogLevel.Info),
                ["w"] = ("Warning", LogLevel.Warn),
                ["wa"] = ("Warning", LogLevel.Warn),
                ["war"] = ("Warning", LogLevel.Warn),
                ["wrn"] = ("Warning", LogLevel.Warn),
                ["warn"] = ("Warning", LogLevel.Warn),
                ["warning"] = ("Warning", LogLevel.Warn),
                ["e"] = ("Error", LogLevel.Error),
                ["er"] = ("Error", LogLevel.Error),
                ["err"] = ("Error", LogLevel.Error),
                ["erro"] = ("Error", LogLevel.Error),
                ["eror"] = ("Error", LogLevel.Error),
                ["error"] = ("Error", LogLevel.Error),
                ["f"] = ("Fatal", LogLevel.Fatal),
                ["fa"] = ("Fatal", LogLevel.Fatal),
                ["ftl"] = ("Fatal", LogLevel.Fatal),
                ["fat"] = ("Fatal", LogLevel.Fatal),
                ["fatl"] = ("Fatal", LogLevel.Fatal),
                ["fatal"] = ("Fatal", LogLevel.Fatal),
                ["c"] = ("Critical", LogLevel.Fatal),
                ["cr"] = ("Critical", LogLevel.Fatal),
                ["crt"] = ("Critical", LogLevel.Fatal),
                ["cri"] = ("Critical", LogLevel.Fatal),
                ["crit"] = ("Critical", LogLevel.Fatal),
                ["critical"] = ("Critical", LogLevel.Fatal),
                ["emerg"] = ("Emergency", LogLevel.Fatal),
                ["panic"] = ("Panic", LogLevel.Fatal)
            };

        const string LevelMarker = "\"MinimumLevelAccepted\":\"";

        /// <summary>Maps a level to an NLog equivalent level.</summary>
        /// <returns>NLog level; gauranteed not null</returns>
        public static LogLevel ToNLogLevel(this string level)
        {
            return LevelsByName.TryGetValue(level, out var m) ? m.Item2 : LogLevel.Info;
        }

        /// <summary>
        /// Looks for a minimum accpted level response header and (if found)
        /// maps it to an NLog level.
        /// </summary>
        /// <returns>NLog level; guaranteed not null</returns>
        public static LogLevel ReadMinimumAcceptedLevel(this string eventInputResult)
        {
            if (eventInputResult == null) return null;

            // Seq 1.5 servers will return JSON including "MinimumLevelAccepted":x, where
            // x may be null or a JSON string representation of the equivalent LogEventLevel
            var startProp = eventInputResult.IndexOf(LevelMarker, StringComparison.Ordinal);
            if (startProp == -1)
                return null;

            var startValue = startProp + LevelMarker.Length;
            if (startValue >= eventInputResult.Length)
                return null;

            var endValue = eventInputResult.IndexOf('"', startValue);
            if (endValue == -1)
                return null;

            var value = eventInputResult.Substring(startValue, endValue - startValue);
            var minimumAcceptedLevel = value.ToNLogLevel();

            return minimumAcceptedLevel;
        }
    }
}