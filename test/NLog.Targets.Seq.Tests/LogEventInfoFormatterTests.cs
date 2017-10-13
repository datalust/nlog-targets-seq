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
    public class LogEventInfoFormatterTests
    {
        JObject AssertValidJson(Action<ILogger> act)
        {
            var logger = LogManager.GetCurrentClassLogger();
            var target = new CollectingTarget();

            SimpleConfigurator.ConfigureForTargetLogging(target, LogLevel.Trace);

            act(logger);

            var formatted = new StringWriter();
            LogEventInfoFormatter.ToCompactJson(target.Events.Single(), formatted, new List<SeqPropertyItem>());

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
        public void AtPrefixedPropertyNamesAreEscaped()
        {
            var logger = LogManager.GetCurrentClassLogger();
            var target = new CollectingTarget();

            SimpleConfigurator.ConfigureForTargetLogging(target, LogLevel.Trace);

            logger.Info("Hello");
            var evt = target.Events.Single();

            // Not possible in message templates, but accepted this way
            evt.Properties.Add("@Mistake", 42);

            var formatted = new StringWriter();
            LogEventInfoFormatter.ToCompactJson(evt, formatted, new List<SeqPropertyItem>());
            var jobject = Assertions.AssertValidJson(formatted.ToString());

            JToken val;
            Assert.True(jobject.TryGetValue("@@Mistake", out val));
            Assert.Equal(42, val.ToObject<int>());
        }

        [Fact]
        public void TimestampIsUtc()
        {
            // Not possible in message templates, but accepted this way
            var jobject = AssertValidJson(log => log.Info("Hello"));

            JToken val;
            Assert.True(jobject.TryGetValue("@t", out val));
            Assert.EndsWith("Z", val.ToObject<string>());
        }

        [Fact]
        public void RenderingsAreRecordedWhenPositional()
        {
            dynamic evt = AssertValidJson(log => log.Info("The number is {0:000}", 42));
            Assert.Equal("042", (string)(evt["@r"][0]));
        }

        [Fact]
        public void RenderingsAreRecordedWhenNamed()
        {
            dynamic evt = AssertValidJson(log => log.Info("The number is {N:000}", 42));
            Assert.Equal("042", (string)(evt["@r"][0]));
        }
    }
}
