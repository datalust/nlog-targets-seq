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
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Net;
using NLog.Common;
using NLog.Config;
using NLog.Layouts;

namespace NLog.Targets.Seq
{
    /// <summary>
    /// Writes events over HTTP to a Seq server.
    /// </summary>
    [Target("Seq")]
    public sealed class SeqTarget : Target
    {
        const string BulkUploadResource = "api/events/raw";
        const string ApiKeyHeaderName = "X-Seq-ApiKey";

        /// <summary>
        /// The layout used to format `LogEvent`s as compact JSON.
        /// </summary>
        public JsonLayout TemplatedClefLayout { get; } = new CompactJsonLayout(true);

        /// <summary>
        /// The layout used to format `LogEvent`s as compact JSON.
        /// </summary>
        public JsonLayout TextClefLayout { get; } = new CompactJsonLayout(false);

        /// <summary>
        /// Initializes the target.
        /// </summary>
        public SeqTarget()
        {
            Properties = new List<SeqPropertyItem>();
        }

        /// <summary>
        /// The address of the Seq server to write to.
        /// </summary>
        [Required]
        public string ServerUrl { get; set; }

        /// <summary>
        /// A Seq <i>API key</i> that authenticates the client to the Seq server.
        /// </summary>
        public string ApiKey { get; set; }

        /// <summary>
        /// The address of the proxy to use, including port separated by a colon. If not provided, default operating system proxy will be used.
        /// </summary>
        public string ProxyAddress { get; set; }

        /// <summary>
        /// A list of properties that will be attached to the events.
        /// </summary>
        [ArrayParameter(typeof(SeqPropertyItem), "property")]
        public IList<SeqPropertyItem> Properties { get; }

        /// <summary>
        /// Initializes the target. Can be used by inheriting classes
        /// to initialize logging.
        /// </summary>
        protected override void InitializeTarget()
        {
            foreach (var prop in Properties)
            {
                var attr = new JsonAttribute(prop.Name, prop.Value, !prop.IsNumber);
                TextClefLayout.Attributes.Add(attr);
                TemplatedClefLayout.Attributes.Add(attr);
            }

            base.InitializeTarget();
        }

        /// <summary>
        /// Writes an array of logging events to Seq.
        /// </summary>
        /// <param name="logEvents">Logging events to be written.</param>
        protected override void Write(IList<AsyncLogEventInfo> logEvents)
        {
            var events = logEvents.Select(e => e.LogEvent);

            PostBatch(events);

            foreach (var evt in logEvents)
                evt.Continuation(null);
        }

        /// <summary>
        /// Writes logging event to Seq.
        /// </summary>
        /// <param name="logEvent">Logging event to be written.
        /// </param>
        protected override void Write(LogEventInfo logEvent)
        {
            PostBatch(new[] { logEvent });
        }

        void PostBatch(IEnumerable<LogEventInfo> events)
        {
            if (ServerUrl == null)
                return;

            var uri = ServerUrl;
            if (!uri.EndsWith("/"))
                uri += "/";
            uri += BulkUploadResource;

            var request = (HttpWebRequest) WebRequest.Create(uri);
            if (!string.IsNullOrWhiteSpace(ProxyAddress))
                request.Proxy = new WebProxy(new Uri(ProxyAddress), true);
            request.Method = "POST";
            request.ContentType = "application/vnd.serilog.clef; charset=utf-8";
            if (!string.IsNullOrWhiteSpace(ApiKey))
                request.Headers.Add(ApiKeyHeaderName, ApiKey);

            using (var requestStream = request.GetRequestStream())
            using (var payload = new StreamWriter(requestStream))
            {
                foreach (var evt in events)
                {
                    RenderCompactJsonLine(evt, payload);
                }
            }

            using (var response = (HttpWebResponse) request.GetResponse())
            {
                var responseStream = response.GetResponseStream();
                if (responseStream == null)
                    throw new WebException("No response was received from the Seq server");

                using (var reader = new StreamReader(responseStream))
                {
                    var data = reader.ReadToEnd();
                    if ((int) response.StatusCode > 299)
                        throw new WebException($"Received failed response {response.StatusCode} from Seq server: {data}");
                }
            }
        }

        internal void RenderCompactJsonLine(LogEventInfo evt, TextWriter output)
        {
            var json = RenderLogEvent(evt.HasProperties ? TemplatedClefLayout : TextClefLayout, evt);
            output.WriteLine(json);
        }

        internal void TestInitialize()
        {
            InitializeTarget();
        }
    }
}
