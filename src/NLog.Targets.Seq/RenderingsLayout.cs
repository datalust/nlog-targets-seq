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
using System.IO;
using System.Text;
using NLog.Layouts;
using NLog.MessageTemplates;

namespace NLog.Targets.Seq
{
    class RenderingsLayout : Layout
    {
        readonly Lazy<IJsonConverter> _jsonConverter;

        public RenderingsLayout(Lazy<IJsonConverter> jsonConverter)
        {
            _jsonConverter = jsonConverter;
        }

        protected override void RenderFormattedMessage(LogEventInfo logEvent, StringBuilder target)
        {
            var result = RenderLogEvent(logEvent, target);
            if (result == null)
                target.Append("null");
        }

        protected override string GetFormattedMessage(LogEventInfo logEvent)
        {
            var result = RenderLogEvent(logEvent);
            return result?.ToString() ?? "null";
        }

        StringBuilder RenderLogEvent(LogEventInfo logEvent, StringBuilder preallocated = null)
        {
            var orgLength = preallocated?.Length ?? 0;
            StringBuilder output = null;

            try
            {
                var nextDelimiter = "";
                var mtp = logEvent.MessageTemplateParameters;

                foreach (var parameter in mtp)
                {
                    if (parameter.Format == null) continue;
                    
                    if (output == null)
                    {
                        output = preallocated ?? new StringBuilder();
                        output.Append("[");
                    }

                    var space = new StringWriter();

                    if (logEvent.Properties != null &&
                        logEvent.Properties.TryGetValue(parameter.Name, out var value))
                    {
                        if (parameter.CaptureType == CaptureType.Normal)
                        {
                            var formatString = string.Concat("{0:", parameter.Format, "}");
                            space.Write(formatString, value);
                        }
                        else
                        {
                            space.Write(value);
                        }
                    }

                    output.Append(nextDelimiter);
                    nextDelimiter = ",";
                    _jsonConverter.Value.SerializeObject(space.ToString(), output);
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
                output?.Append("]");
            }
        }
    }
}
