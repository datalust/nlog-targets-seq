using System.Collections.Generic;

namespace NLog.Targets.Seq.Tests.Support
{
    [Target("memory")]
    public class CollectingTarget : Target
    {
        public List<LogEventInfo> Events { get; } = new List<LogEventInfo>();

        protected override void Write(LogEventInfo logEvent)
        {
            Events.Add(logEvent);
        }
    }
}
