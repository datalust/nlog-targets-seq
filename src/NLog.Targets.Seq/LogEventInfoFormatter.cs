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
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace NLog.Targets.Seq
{
    static class LogEventInfoFormatter
    {
        static readonly IDictionary<Type, Action<object, TextWriter>> LiteralWriters;
        const string InfoLevel = "Info";

        static LogEventInfoFormatter()
        {
            LiteralWriters = new Dictionary<Type, Action<object, TextWriter>>
            {
                { typeof(bool), (v, w) => WriteBoolean((bool)v, w) },
                { typeof(char), (v, w) => WriteString(((char)v).ToString(CultureInfo.InvariantCulture), w) },
                { typeof(byte), WriteToString },
                { typeof(sbyte), WriteToString },
                { typeof(short), WriteToString },
                { typeof(ushort), WriteToString },
                { typeof(int), WriteToString },
                { typeof(uint), WriteToString },
                { typeof(long), WriteToString },
                { typeof(ulong), WriteToString },
                { typeof(float), WriteToString },
                { typeof(double), WriteToString },
                { typeof(decimal), WriteToString },
                { typeof(string), (v, w) => WriteString((string)v, w) },
                { typeof(DateTime), (v, w) => WriteDateTime((DateTime)v, w) },
                { typeof(DateTimeOffset), (v, w) => WriteOffset((DateTimeOffset)v, w) },
            };
        }

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

            // Without this call, logEventInfo.Properties behaves erratically when templates are used; stll trying
            // to track down the cause (needs some debugging into NLog).
            logEvent.FormattedMessage?.ToString();

            output.Write("{\"@t\":\"");
            output.Write(logEvent.TimeStamp.ToUniversalTime().ToString("O"));

            string message = null;
            Template template = null;
            try
            {
                if (logEvent.Message != null)
                {
                    message = logEvent.Message;
                    template = TemplateParser.Parse(logEvent.Message);
                }
            }
            catch (TemplateParserException) { }

            if (message != null || (message == null && logEvent.FormattedMessage == null))
            {
                output.Write("\",\"@mt\":");
                WriteString(message ?? "(No message)", output);
            }
            else
            {
                output.Write("\",\"@m\":");
                WriteString(logEvent.FormattedMessage, output);
            }

            var tokensWithFormat = template.Holes.Where(h => h.Format != null);

            // Better not to allocate an array in the 99.9% of cases where this is false
            // ReSharper disable once PossibleMultipleEnumeration
            if (tokensWithFormat.Any())
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

                    WriteString(space.ToString(), output);
                }
                output.Write(']');
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
                WriteString(logEvent.Exception.ToString(), output);
            }

            var seenKeys = new HashSet<string>();

            foreach (var property in properties)
            {
                var name = EscapeKey(property.Name);
                if (seenKeys.Contains(name))
                    continue;

                seenKeys.Add(name);

                output.Write(',');
                WriteString(name, output);
                output.Write(':');

                var stringValue = property.Value.Render(logEvent);
                if (property.As == "number")
                {
                    decimal numberValue;
                    if (decimal.TryParse(stringValue, out numberValue))
                    {
                        WriteLiteral(numberValue, output);
                        continue;
                    }
                }

                WriteString(stringValue, output);
            }

            if (logEvent.Message != null &&
                logEvent.Message.Contains("{0") &&
                logEvent.Parameters != null)
            {
                for (var i = 0; i < logEvent.Parameters.Length; ++i)
                {
                    var name = i.ToString(CultureInfo.InvariantCulture);
                    if (seenKeys.Contains(name))
                        continue;

                    seenKeys.Add(name);

                    output.Write(',');
                    WriteString(name, output);
                    output.Write(':');
                    WriteLiteral(logEvent.Parameters[i], output);
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
                    WriteString(name, output);
                    output.Write(':');
                    WriteLiteral(property.Value, output);
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
        
        static void WriteLiteral(object value, TextWriter output)
        {
            if (value == null)
            {
                output.Write("null");
                return;
            }

            // Attempt to convert the object (if a string) to its literal type (int/decimal/date)
            value = GetValueAsLiteral(value);

            Action<object, TextWriter> writer;
            if (LiteralWriters.TryGetValue(value.GetType(), out writer))
            {
                writer(value, output);
                return;
            }

            WriteString(value.ToString(), output);
        }

        static void WriteToString(object number, TextWriter output)
        {
            output.Write(number.ToString());
        }

        static void WriteBoolean(bool value, TextWriter output)
        {
            output.Write(value ? "true" : "false");
        }

        static void WriteOffset(DateTimeOffset value, TextWriter output)
        {
            output.Write("\"");
            output.Write(value.ToString("o"));
            output.Write("\"");
        }

        static void WriteDateTime(DateTime value, TextWriter output)
        {
            output.Write("\"");
            output.Write(value.ToString("o"));
            output.Write("\"");
        }

        static void WriteString(string value, TextWriter output)
        {
            var content = Escape(value);
            output.Write("\"");
            output.Write(content);
            output.Write("\"");
        }

        static string Escape(string s)
        {
            if (s == null) return null;

            StringBuilder escapedResult = null;
            var cleanSegmentStart = 0;
            for (var i = 0; i < s.Length; ++i)
            {
                var c = s[i];
                if (c < (char)32 || c == '\\' || c == '"')
                {

                    if (escapedResult == null)
                        escapedResult = new StringBuilder();

                    escapedResult.Append(s.Substring(cleanSegmentStart, i - cleanSegmentStart));
                    cleanSegmentStart = i + 1;

                    switch (c)
                    {
                        case '"':
                            {
                                escapedResult.Append("\\\"");
                                break;
                            }
                        case '\\':
                            {
                                escapedResult.Append("\\\\");
                                break;
                            }
                        case '\n':
                            {
                                escapedResult.Append("\\n");
                                break;
                            }
                        case '\r':
                            {
                                escapedResult.Append("\\r");
                                break;
                            }
                        case '\f':
                            {
                                escapedResult.Append("\\f");
                                break;
                            }
                        case '\t':
                            {
                                escapedResult.Append("\\t");
                                break;
                            }
                        default:
                            {
                                escapedResult.Append("\\u");
                                escapedResult.Append(((int)c).ToString("X4"));
                                break;
                            }
                    }
                }
            }

            if (escapedResult != null)
            {
                if (cleanSegmentStart != s.Length)
                    escapedResult.Append(s.Substring(cleanSegmentStart));

                return escapedResult.ToString();
            }

            return s;
        }

        /// <summary>
        /// GetValueAsLiteral attempts to transform the (string) object into a literal type prior to json serialization.
        /// </summary>
        /// <param name="value">The value to be transformed/parsed.</param>
        /// <returns>A translated representation of the literal object type instead of a string.</returns>
        static object GetValueAsLiteral(object value)
        {
            var str = value as string;
            if (str == null)
                return value;

            // All number literals are serialized as a decimal so ignore other number types.
            decimal decimalBuffer;
            if (decimal.TryParse(str, out decimalBuffer))
                return decimalBuffer;

            // Standardize on dates if/when possible.
            DateTime dateBuffer;
            if (DateTime.TryParse(str, out dateBuffer))
                return dateBuffer;

            return value;
        }
    }
}
