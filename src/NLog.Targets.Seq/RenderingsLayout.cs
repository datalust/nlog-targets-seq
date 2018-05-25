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
            StringBuilder output;
            RenderLogEvent(logEvent, target, out output);
            if (output == null)
                target.Append("null");
        }

        protected override string GetFormattedMessage(LogEventInfo logEvent)
        {
            StringBuilder output;
            RenderLogEvent(logEvent, null, out output);
            return output?.ToString() ?? "null";
        }

        private void RenderLogEvent(LogEventInfo logEvent, StringBuilder preallocated, out StringBuilder output)
        {
            int orgLength = preallocated?.Length ?? 0;
            output = null;

            try
            {
                var nextDelimiter = "";
                var mtp = logEvent.MessageTemplateParameters;

                for (var i = 0; i < mtp.Count; ++i)
                {
                    var parameter = mtp[i];

                    if (parameter.CaptureType == CaptureType.Normal && parameter.Format != null)
                    {
                        if (logEvent.Properties != null && logEvent.Properties.TryGetValue(parameter.Name, out var value))
                        {
                            if (output == null)
                            {
                                output = preallocated ?? new StringBuilder();
                                output.Append("[");
                            }

                            var formatString = string.Concat("{0:", parameter.Format, "}");
                            var space = new StringWriter();
                            space.Write(formatString, value);

                            output.Append(nextDelimiter);
                            nextDelimiter = ",";
                            JsonConverter.SerializeObject(space.ToString(), output);
                        }
                    }
                }
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
