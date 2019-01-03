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

using System.IO;
using System.Text;
using NLog.Config;
using NLog.Layouts;
using NLog.MessageTemplates;

namespace NLog.Targets.Seq
{
    class RenderingsLayout : Layout
    {
        IJsonConverter _jsonConverter;
        IJsonConverter JsonConverter => _jsonConverter ?? (_jsonConverter = ConfigurationItemFactory.Default.JsonConverter);

        protected override void RenderFormattedMessage(LogEventInfo logEvent, StringBuilder target)
        {
            StringBuilder result = RenderLogEvent(logEvent, target);
            if (result == null)
                target.Append("null");
        }

        protected override string GetFormattedMessage(LogEventInfo logEvent)
        {
            StringBuilder result = RenderLogEvent(logEvent);
            return result?.ToString() ?? "null";
        }

        private StringBuilder RenderLogEvent(LogEventInfo logEvent, StringBuilder preallocated = null)
        {
            int orgLength = preallocated?.Length ?? 0;
            StringBuilder output = null;

            try
            {
                var nextDelimiter = "";
                var mtp = logEvent.MessageTemplateParameters;

                for (var i = 0; i < mtp.Count; ++i)
                {
                    var parameter = mtp[i];

                    if (parameter.Format != null)
                    {
                        if (output == null)
                        {
                            output = preallocated ?? new StringBuilder();
                            output.Append("[");
                        }

                        var space = new StringWriter();

                        if (logEvent.Properties != null &&
                            logEvent.Properties.TryGetValue(parameter.Name, out var value))
                        {
                            switch (parameter.CaptureType)
                            {
                                case CaptureType.Normal:
                                    var formatString = string.Concat("{0:", parameter.Format, "}");
                                    space.Write(formatString, value);
                                    break;
                                default: // Serialize, Stringify, Unknown
                                    space.Write(value);
                                    break;
                            }
                        }

                        output.Append(nextDelimiter);
                        nextDelimiter = ",";
                        JsonConverter.SerializeObject(space.ToString(), output);
                    }
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
                if (output != null)
                    output.Append("]");
            }
        }

    }
}
