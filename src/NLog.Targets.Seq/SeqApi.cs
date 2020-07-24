// Seq Target for NLog - Copyright 2014-2020 Datalust and contributors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//    http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;

namespace NLog.Targets.Seq
{
    static class SeqApi
    {
        public const string BulkUploadResource = "api/events/raw";
        public const string ApiKeyHeaderName = "X-Seq-ApiKey";
        public const string CompactLogEventFormatContentType = "application/vnd.serilog.clef; charset=utf-8";
        public const string NoPayload = "";

        // Why not use a JSON parser here? For a very small case, it's not
        // worth taking on the extra payload/dependency management issues that
        // a full-fledged parser will entail. If things get more sophisticated
        // we'll reevaluate.
        const string LevelMarker = "\"MinimumLevelAccepted\":\"";

        public static SeqLogLevel? ReadMinimumAcceptedLevel(string eventInputResult)
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
            if (!Enum.TryParse(value, out SeqLogLevel minimumLevel))
                return null;

            return minimumLevel;
        }
    }
}
