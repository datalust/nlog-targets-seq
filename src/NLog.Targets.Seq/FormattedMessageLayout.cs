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

using System.Text;
using NLog.Config;
using NLog.Layouts;

namespace NLog.Targets.Seq
{
    [ThreadAgnostic]
    class FormattedMessageLayout : Layout
    {
        protected override void RenderFormattedMessage(LogEventInfo logEvent, StringBuilder target)
        {
            target.Append(GetFormattedMessage(logEvent));
        }

        protected override string GetFormattedMessage(LogEventInfo logEvent)
        {
            if (HasMessageTemplateSyntax(logEvent))
            {
                return string.Empty;    // Message Template Syntax, no need to include formatted message
            }

            return logEvent.FormattedMessage;
        }

        bool HasMessageTemplateSyntax(LogEventInfo logEvent)
        {
            if (!logEvent.HasProperties)
                return false;

            if (logEvent.Message?.IndexOf("{0", System.StringComparison.Ordinal) >= 0)
            {
                var mtp = logEvent.MessageTemplateParameters;
                return !mtp.IsPositional;
            }

            return true;
        }
    }
}
