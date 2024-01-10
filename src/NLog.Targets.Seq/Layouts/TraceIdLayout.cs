using System.Diagnostics;
using System.Text;
using NLog.Layouts;

namespace NLog.Targets.Seq.Layouts
{
    class TraceIdLayout: Layout
    {
        protected override void RenderFormattedMessage(LogEventInfo logEvent, StringBuilder target)
        {
            target.Append(GetFormattedMessage(logEvent));
        }

        protected override string GetFormattedMessage(LogEventInfo logEvent)
        {
            return Activity.Current is { IdFormat: ActivityIdFormat.W3C } activity ? activity.TraceId.ToString() : null;
        }        
    }
}
