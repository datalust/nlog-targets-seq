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

using System;
using System.Text;
using NLog.Config;
using NLog.Layouts;
using NLog.MessageTemplates;

namespace NLog.Targets.Seq
{
    [ThreadAgnostic]
    class RenderingsLayout : Layout
    {
        readonly Lazy<IJsonConverter> _jsonConverter;

        public RenderingsLayout(Lazy<IJsonConverter> jsonConverter)
        {
            _jsonConverter = jsonConverter;
        }

        protected override void RenderFormattedMessage(LogEventInfo logEvent, StringBuilder target)
        {
            RenderLogEvent(logEvent, target);
        }

        protected override string GetFormattedMessage(LogEventInfo logEvent)
        {
            var result = RenderLogEvent(logEvent);
            return result?.ToString() ?? "";
        }

        StringBuilder RenderLogEvent(LogEventInfo logEvent, StringBuilder preallocated = null)
        {
            var orgLength = preallocated?.Length ?? 0;
            StringBuilder output = null;

            try
            {
                if (!logEvent.HasProperties)
                    return null;

                var nextDelimiter = "";
                var mtp = logEvent.MessageTemplateParameters;
                if (mtp.IsPositional || mtp.Count == 0)
                    return null;

                foreach (var parameter in mtp)
                {
                    if (string.IsNullOrEmpty(parameter.Format)) continue;

                    if (!logEvent.Properties.TryGetValue(parameter.Name, out var value)) continue;

                    if (output == null)
                    {
                        output = preallocated ?? new StringBuilder();
                        output.Append('[');
                    }

                    string formattedValue = FormatToString(parameter, value);

                    output.Append(nextDelimiter);
                    nextDelimiter = ",";
                    _jsonConverter.Value.SerializeObject(formattedValue, output);
                }

                return output;
            }
            catch
            {
                if (output != null && preallocated != null)
                    preallocated.Length = orgLength;    // truncate/unwind faulty output
                output = null;
                throw;
            }
            finally
            {
                output?.Append(']');
            }
        }

        private static string FormatToString(MessageTemplateParameter parameter, object value)
        {
            if (parameter.CaptureType == CaptureType.Normal)
            {
                var formatString = string.Concat("{0:", parameter.Format, "}");
                return string.Format(formatString, value);
            }
            else
            {
                return Convert.ToString(value);
            }
        }
    }
}
