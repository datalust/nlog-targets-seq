using Newtonsoft.Json.Linq;
using NLog.Config;
using NLog.Targets.Seq.Tests.Support;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace NLog.Targets.Seq.Tests
{
    public class SeqTargetTests
    {
        static void ToCompactJson(LogEventInfo evt, TextWriter output, IEnumerable<SeqPropertyItem> properties, int? maxRecursionLimit = null)
        {
            var target = new SeqTarget();
            if (maxRecursionLimit.HasValue)
                target.MaxRecursionLimit = maxRecursionLimit.Value;
            foreach (var prop in properties)
            {
                target.Properties.Add(prop);
            }

            target.TestInitialize();

            target.RenderCompactJsonLine(evt, output);
        }

        JObject AssertValidJson(Action<ILogger> act, IEnumerable<SeqPropertyItem> properties = null, int? maxRecursionLimit = null)
        {
            var logger = LogManager.GetCurrentClassLogger();
            var target = new CollectingTarget();

            SimpleConfigurator.ConfigureForTargetLogging(target, LogLevel.Trace);

            act(logger);

            var formatted = new StringWriter();
            
            ToCompactJson(target.Events.Single(), formatted, properties?.ToList() ?? new List<SeqPropertyItem>(), maxRecursionLimit);

            return Assertions.AssertValidJson(formatted.ToString());
        }

        [Fact]
        public void AnEmptyEventIsValidJson()
        {
            AssertValidJson(log => log.Info("No properties"));
        }

        [Fact]
        public void ANonInfoLevelEventIsValid()
        {
            dynamic evt = AssertValidJson(log => log.Warn("No properties"));
            Assert.Equal("Warn", (string)evt["@l"]);
        }

        [Fact]
        public void AMinimalEventIsValidJson()
        {
            AssertValidJson(log => log.Info("One {Property}", 42));
        }

        [Fact]
        public void ASimplePropertyEventIsValidJson()
        {
            LogEventInfo logEvent = new LogEventInfo() { Message = "Hello " };
            logEvent.Properties["Answer"] = 42;
            var evt = AssertValidJson(log => log.Info(logEvent));
            Assert.Equal(42, evt["Answer"].Value<int>());
        }

        [Fact]
        public void AToStringPropertyEventIsValidJson()
        {
            LogEventInfo logEvent = new LogEventInfo() { Message = "Hello " };
            logEvent.Properties["Result"] = new DivideByZeroException();
            var result = logEvent.Properties["Result"].ToString();
            var evt = AssertValidJson(log => log.Info(logEvent));
            Assert.Equal(result, evt["Result"].Value<string>());
        }

        [Fact]
        public void AComplexPropertyEventIsValidJson()
        {
            LogEventInfo logEvent = new LogEventInfo() { Message = "Hello " };
            logEvent.Properties["Result"] = new DivideByZeroException();
            var result = logEvent.Properties["Result"].ToString();
            var evt = AssertValidJson(log => log.Info(logEvent), maxRecursionLimit: 1);
            Assert.Equal(3, evt["Result"].ToList().Count());
        }

        [Fact]
        public void DefaultStructuredDataIsStringified()
        {
            dynamic evt = AssertValidJson(log => log.Info("Some {StringData}", new StringData { Data = "A" }));
            Assert.Equal("SD:A", (string)evt.StringData);
        }

        [Fact]
        public void SerializedStructuredDataIsCaptured()
        {
            dynamic evt = AssertValidJson(log => log.Info("Some {@StringData}", new StringData { Data = "A" }));
            Assert.Equal("A", (string)evt.StringData.Data);
        }

        [Fact]
        public void EnumerableDataIsCapturedToDepth1ByDefault()
        {
            dynamic evt = AssertValidJson(log => log.Info("Some {StringData}", new[] { new StringData { Data = "A" } }));
            Assert.Equal("SD:A", (string)evt.StringData[0]);
        }

        [Fact]
        public void EnumerableDataIsCapturedToFullDepthWhenSerialized()
        {
            dynamic evt = AssertValidJson(log => log.Info("Some {@StringData}", new[] { new StringData { Data = "A" } }));
            Assert.Equal("A", (string)evt.StringData[0].Data);
        }

        [Fact]
        public void MultiplePropertiesAreDelimited()
        {
            AssertValidJson(log => log.Info("Property {First} and {Second}", "One", "Two"));
        }

        [Fact]
        public void ExceptionsAreFormattedToValidJson()
        {
            AssertValidJson(log => log.Info(new DivideByZeroException(), "With exception"));
        }

        [Fact]
        public void ExceptionAndPropertiesAreValidJson()
        {
            AssertValidJson(log => log.Info(new DivideByZeroException(), "With exception and {Property}", 42));
        }

        [Fact]
        public void RenderingsAreValidJson()
        {
            AssertValidJson(log => log.Info("One {Rendering:x8}", 42));
        }

        [Fact]
        public void MultipleRenderingsAreDelimited()
        {
            AssertValidJson(log => log.Info("Rendering {First:x8} and {Second:x8}", 1, 2));
        }

        [Fact]
        public void TimestampIsUtcOrCarriesTimeZone()
        {
            var jobject = AssertValidJson(log => log.Info("Hello"));

            Assert.True(jobject.TryGetValue("@t", out var val));
            var str = val.ToObject<string>();
            Assert.True(str.EndsWith("Z") || str[str.Length - 3] == ':');
        }

        [Fact]
        public void RenderingsAreRecordedWhenNamed()
        {
            dynamic evt = AssertValidJson(log => log.Info("The number is {N:000}", 42));
            Assert.Equal("042", (string)(evt["@r"][0]));
        }
    }
}
