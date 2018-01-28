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

        protected override string GetFormattedMessage(LogEventInfo logEvent)
        {
            StringBuilder output = null;
            var nextDelimiter = "";
            var mtp = logEvent.MessageTemplateParameters;

            for (var i = 0; i < mtp.Count; ++i)
            {
                var parameter = mtp[i];

                if (parameter.CaptureType == CaptureType.Normal && parameter.Format != null)
                {
                    output = output ?? new StringBuilder("[");
                    output.Append(nextDelimiter);
                    nextDelimiter = ",";

                    var space = new StringWriter();
                    var formatString = "{0:" + parameter.Format + "}";

                    if (logEvent.Properties != null && logEvent.Properties.TryGetValue(parameter.Name, out var value))
                        space.Write(formatString, value);

                    JsonConverter.SerializeObject(space.ToString(), output);
                }
            }

            if (output == null)
                return "null";

            output.Append("]");
            return output.ToString();
        }
    }
}
