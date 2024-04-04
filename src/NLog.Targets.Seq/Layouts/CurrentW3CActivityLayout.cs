// Seq Target for NLog - Copyright © Datalust and contributors
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
using System.Diagnostics;
using System.Text;
using NLog.Layouts;

namespace NLog.Targets.Seq.Layouts
{
    /// <summary>
    /// Formats elements of <see cref="Activity.Current"/> for inclusion in log events. Non-W3C-format activities are
    /// ignored (Seq does not support the older Microsoft-proprietary hierarchical activity id format).
    /// </summary>
    class CurrentW3CActivityLayout: Layout
    {
        readonly Func<Activity, string> _format;

        public CurrentW3CActivityLayout(Func<Activity, string> format)
        {
            _format = format;
        }
        
        protected override void RenderFormattedMessage(LogEventInfo logEvent, StringBuilder target)
        {
            target.Append(GetFormattedMessage(logEvent));
        }

        protected override string GetFormattedMessage(LogEventInfo logEvent)
        {
            return Activity.Current is { IdFormat: ActivityIdFormat.W3C } activity ? _format(activity) : null;
        }        
    }
}