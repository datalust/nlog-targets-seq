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

using NLog.StructuredEvents.Parts;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;

namespace NLog.Targets.Seq
{
    static class JsonWriter
    {
        static readonly IDictionary<Type, Action<object, TextWriter>> LiteralWriters;

        static JsonWriter()
        {
            LiteralWriters = new Dictionary<Type, Action<object, TextWriter>>
            {
                { typeof(bool), (v, w) => WriteBoolean((bool)v, w) },
                { typeof(char), (v, w) => WriteString(((char)v).ToString(CultureInfo.InvariantCulture), w) },
                { typeof(byte), WriteNumber },
                { typeof(sbyte), WriteNumber },
                { typeof(short), WriteNumber },
                { typeof(ushort), WriteNumber },
                { typeof(int), WriteNumber },
                { typeof(uint), WriteNumber },
                { typeof(long), WriteNumber },
                { typeof(ulong), WriteNumber },
                { typeof(float), (v, w) => WriteFloat((float)v, w) },
                { typeof(double), (v, w) => WriteDouble((double)v, w) },
                { typeof(decimal), WriteNumber },
                { typeof(string), (v, w) => WriteString((string)v, w) },
                { typeof(DateTime), (v, w) => WriteDateTime((DateTime)v, w) },
                { typeof(DateTimeOffset), (v, w) => WriteOffset((DateTimeOffset)v, w) },
            };
        }

        public static void WriteLiteral(object value, TextWriter output, CaptureType captureType = CaptureType.Normal, int depthRemaining = 5)
        {
            if (value == null)
            {
                output.Write("null");
                return;
            }

            if (captureType == CaptureType.Stringify)
            {
                WriteString(value.ToString(), output);
                return;
            }

            Action<object, TextWriter> writer;
            if (LiteralWriters.TryGetValue(value.GetType(), out writer))
            {
                writer(value, output);
                return;
            }

            if (depthRemaining == 1)
            {
                WriteString(value.ToString(), output);
                return;
            }

            if (captureType == CaptureType.Normal)
            {
                if (value is IEnumerable)
                    WriteLiteral(value, output, CaptureType.Serialize, 2);
                else
                    WriteString(value.ToString(), output);
                return;
            }

            if (value is IEnumerable)
            {
                // Dictionary serialization missing here.
                output.Write('[');
                var arrayDelimiter = "";
                foreach (var item in (IEnumerable)value)
                {
                    output.Write(arrayDelimiter);
                    arrayDelimiter = ",";
                    WriteLiteral(item, output, captureType, depthRemaining - 1);
                }
                output.Write(']');
                return;
            }

            output.Write('{');
            var propertyDelimiter = "";
            foreach (var prop in value.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (prop.CanRead && !prop.IsSpecialName)
                {
                    output.Write(propertyDelimiter);
                    propertyDelimiter = ",";
                    WriteString(prop.Name, output);
                    output.Write(':');

                    try
                    {
                        var item = prop.GetValue(value);
                        WriteLiteral(item, output, captureType, depthRemaining - 1);
                    }
                    catch (TargetInvocationException tie)
                    {
                        WriteString(tie.ToString(), output);
                    }
                }
            }
            output.Write('}');
        }

        public static void WriteString(string value, TextWriter output)
        {
            var content = Escape(value);
            output.Write("\"");
            output.Write(content);
            output.Write("\"");
        }

        static void WriteNumber(object number, TextWriter output)
        {
            output.Write(number.ToString());
        }

        static void WriteDouble(double number, TextWriter output)
        {
            if (double.IsNaN(number))
            {
                WriteString("NaN", output);
            }
            else if (double.IsPositiveInfinity(number))
            {
                WriteString("Infinity", output);
            }
            else if (double.IsNegativeInfinity(number))
            {
                WriteString("-Infinity", output);
            }
            else
            {
                output.Write(number.ToString());
            }
        }

        static void WriteFloat(float number, TextWriter output)
        {
            if (float.IsNaN(number))
            {
                WriteString("NaN", output);
            }
            else if (float.IsPositiveInfinity(number))
            {
                WriteString("Infinity", output);
            }
            else if (float.IsNegativeInfinity(number))
            {
                WriteString("-Infinity", output);
            }
            else
            {
                output.Write(number.ToString());
            }
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
    }
}
