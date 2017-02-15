// Seq Target for NLog - Copyright 2014-2017 Datalust and contributors
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

using NLog.StructuredEvents;
using NLog.StructuredEvents.Parts;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace NLog.Targets.Seq
{
    static class LogEventInfoFormatter
    {
        const string InfoLevel = "Info";

        public static void ToCompactJson(IEnumerable<LogEventInfo> logEvents, TextWriter output, IList<SeqPropertyItem> properties)
        {
            foreach (var logEvent in logEvents)
            {
                ToCompactJson(logEvent, output, properties);
                output.Write(Environment.NewLine);
            }
        }

        public static void ToCompactJson(LogEventInfo logEvent, TextWriter output, IList<SeqPropertyItem> properties)
        {
            if (logEvent == null) throw new ArgumentNullException(nameof(logEvent));
            if (output == null) throw new ArgumentNullException(nameof(output));
            if (properties == null) throw new ArgumentNullException(nameof(properties));

            output.Write("{\"@t\":\"");
            output.Write(logEvent.TimeStamp.ToUniversalTime().ToString("O"));

            string message = null;
            Template template = null;
            try
            {
                if (logEvent.Message != null)
                {
                    message = logEvent.Message;
                    template = logEvent.GetMessageTemplate();
                }
            }
            catch (TemplateParserException) { }

            if (message != null || (message == null && logEvent.FormattedMessage == null))
            {
                output.Write("\",\"@mt\":");
                JsonWriter.WriteString(message ?? "(No message)", output);
            }
            else
            {
                output.Write("\",\"@m\":");
                JsonWriter.WriteString(logEvent.FormattedMessage, output);
            }

            Dictionary<string, CaptureType> captureTypes = null;

            if (template != null)
            {
                List<Hole> tokensWithFormat = null;
                for (var i = 0; i < template.Holes.Length; ++i)
                {
                    var hole = template.Holes[i];

                    if (hole.Format != null)
                    {
                        tokensWithFormat = tokensWithFormat ?? new List<Hole>();
                        tokensWithFormat.Add(hole);
                    }

                    if (hole.Name != null && hole.Name != null && hole.CaptureType != CaptureType.Normal)
                    {
                        captureTypes = captureTypes ?? new Dictionary<string, CaptureType>();
                        captureTypes.Add(hole.Name, hole.CaptureType);
                    }
                }

                if (tokensWithFormat != null)
                {
                    output.Write(",\"@r\":[");
                    var delim = "";
                    foreach (var r in tokensWithFormat)
                    {
                        output.Write(delim);
                        delim = ",";
                        var space = new StringWriter();
                        var formatString = "{0:" + r.Format + "}";

                        if (r.Name != null && logEvent.Properties != null && logEvent.Properties.ContainsKey(r.Name))
                            space.Write(formatString, logEvent.Properties[r.Name]);
                        else if (logEvent.Parameters != null && logEvent.Parameters.Length >= r.Index)
                            space.Write(formatString, logEvent.Parameters[r.Index]);

                        JsonWriter.WriteString(space.ToString(), output);
                    }
                    output.Write(']');
                }
            }

            if (logEvent.Level.Name != InfoLevel)
            {
                output.Write(",\"@l\":\"");
                output.Write(logEvent.Level.Name);
                output.Write('\"');
            }

            if (logEvent.Exception != null)
            {
                output.Write(",\"@x\":");
                JsonWriter.WriteString(logEvent.Exception.ToString(), output);
            }

            var seenKeys = new HashSet<string>();

            foreach (var property in properties)
            {
                var name = EscapeKey(property.Name);
                if (seenKeys.Contains(name))
                    continue;

                seenKeys.Add(name);

                output.Write(',');
                JsonWriter.WriteString(name, output);
                output.Write(':');

                var stringValue = property.Value.Render(logEvent);
                if (property.As == "number")
                {
                    decimal numberValue;
                    if (decimal.TryParse(stringValue, out numberValue))
                    {
                        JsonWriter.WriteLiteral(numberValue, output);
                        continue;
                    }
                }

                JsonWriter.WriteString(stringValue, output);
            }

            if (template != null &&
                template.IsPositional &&
                logEvent.Parameters != null)
            {
                for (var i = 0; i < logEvent.Parameters.Length; ++i)
                {
                    var name = i.ToString(CultureInfo.InvariantCulture);
                    if (seenKeys.Contains(name))
                        continue;

                    seenKeys.Add(name);

                    output.Write(',');
                    JsonWriter.WriteString(name, output);
                    output.Write(':');
                    JsonWriter.WriteLiteral(logEvent.Parameters[i], output);
                }
            }

            if (logEvent.Properties != null)
            {
                foreach (var property in logEvent.Properties)
                {
                    var name = EscapeKey(property.Key.ToString());
                    if (seenKeys.Contains(name))
                        continue;

                    seenKeys.Add(name);

                    output.Write(',');
                    JsonWriter.WriteString(name, output);
                    output.Write(':');

                    CaptureType captureType = CaptureType.Normal;
                    if (captureTypes == null || !captureTypes.TryGetValue(name, out captureType))
                        captureType = CaptureType.Normal;
                    
                    JsonWriter.WriteLiteral(property.Value, output, captureType);
                }
            }
        }

        static string EscapeKey(string key)
        {
            var san = key;
            if (san.Length > 0 && san[0] == '@')
            {
                // Escape first '@' by doubling
                san = '@' + san;
            }
            return san;
        }
    }
}
