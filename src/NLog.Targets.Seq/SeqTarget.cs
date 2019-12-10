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
using System.IO;
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
        /// Maximum size allowed for JSON payload sent to Seq-Server. Discards logevents that are larger than limit.
        /// </summary>
        public int JsonPayloadMaxLength { get; set; }

        /// <summary>
        /// Initializes the target.
        /// </summary>
        public SeqTarget()
        {
            Properties = new List<SeqPropertyItem>();
            MaxRecursionLimit = 0;  // Default behavior for Serilog
            OptimizeBufferReuse = true;
            JsonPayloadMaxLength = 128 * 1024;
        }

        /// <summary>
        /// The address of the Seq server to write to.
        /// </summary>
        [RequiredParameter]
        public string ServerUrl { get => (_serverUrl as SimpleLayout)?.Text; set => _serverUrl = value ?? string.Empty; }
        private Layout _serverUrl;

        /// <summary>
        /// A Seq <i>API key</i> that authenticates the client to the Seq server.
        /// </summary>
        public string ApiKey { get => (_apiKey as SimpleLayout)?.Text; set => _apiKey = value ?? string.Empty; }
        private Layout _apiKey;

        /// <summary>
        /// The address of the proxy to use, including port separated by a colon. If not provided, default operating system proxy will be used.
        /// </summary>
        public string ProxyAddress { get => (_proxyAddress as SimpleLayout)?.Text; set => _proxyAddress = value ?? string.Empty; }
        private Layout _proxyAddress;

        /// <summary>
        /// A list of properties that will be attached to the events.
        /// </summary>
        [ArrayParameter(typeof(SeqPropertyItem), "property")]
        public IList<SeqPropertyItem> Properties { get; }

        /// <summary>
        /// How far should the JSON serializer follow object references before backing off
        /// 
        /// 0 = Minimal Reflection for only MessageTemplate, 1 = All properties has minimal
        ///  reflection, Higher = Extensive JSON reflection
        /// </summary>
        public int MaxRecursionLimit
        {
            get => TemplatedClefLayout.MaxRecursionLimit;
            set { TemplatedClefLayout.MaxRecursionLimit = value; TextClefLayout.MaxRecursionLimit = value; }
        }

        WebProxy _webProxy;
        Uri _webRequestUri;
        string _headerApiKey;

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

            if (!string.IsNullOrEmpty(ServerUrl))
            {
                var uri = _serverUrl?.Render(LogEventInfo.CreateNullEvent()) ?? string.Empty;
                if (!uri.EndsWith("/", StringComparison.InvariantCulture))
                    uri += "/";
                uri += BulkUploadResource;
                _webRequestUri = new Uri(uri);
            }

            var proxyAddress = _proxyAddress?.Render(LogEventInfo.CreateNullEvent()) ?? string.Empty;
            _webProxy = string.IsNullOrEmpty(proxyAddress) ? null : new WebProxy(new Uri(proxyAddress), true);

            _headerApiKey = _apiKey?.Render(LogEventInfo.CreateNullEvent()) ?? string.Empty;

            base.InitializeTarget();
        }

        /// <summary>
        /// Writes an array of logging events to Seq.
        /// </summary>
        /// <param name="logEvents">Logging events to be written.</param>
        protected override void Write(IList<AsyncLogEventInfo> logEvents)
        {
            try
            {
                PostBatch(logEvents);
            }
            catch (Exception ex)
            {
                InternalLogger.Error(ex, "Seq(Name={0}): Failed sending LogEvents. Uri={1}", Name, _webRequestUri);
                if (LogManager.ThrowExceptions)
                    throw;

                for (int i = 0; i < logEvents.Count; ++i)
                    logEvents[i].Continuation(ex);
            }
        }

        /// <summary>
        /// Writes logging event to Seq.
        /// </summary>
        /// <param name="logEvent">Logging event to be written.
        /// </param>
        protected override void Write(AsyncLogEventInfo logEvent)
        {
            try
            {
                PostBatch(new[] { logEvent });
            }
            catch (Exception ex)
            {
                InternalLogger.Error(ex, "Seq(Name={0}): Failed sending LogEvents. Uri={1}", Name, _webRequestUri);
                if (LogManager.ThrowExceptions)
                    throw;

                logEvent.Continuation(ex);
            }
        }

        void PostBatch(IList<AsyncLogEventInfo> logEvents)
        {
            if (_webRequestUri == null)
                return;

            var request = (HttpWebRequest) WebRequest.Create(_webRequestUri);
            if (_webProxy != null)
                request.Proxy = _webProxy;
            request.Method = "POST";
            request.ContentType = "application/vnd.serilog.clef; charset=utf-8";
            if (!string.IsNullOrWhiteSpace(_headerApiKey))
                request.Headers.Add(ApiKeyHeaderName, _headerApiKey);

            List<AsyncLogEventInfo> extraBatch = null;
            int totalPayload = 0;
            using (var responseStream = request.GetRequestStream())
            {
                using (var payload = new StreamWriter(responseStream))
                {
                    for (int i = 0; i < logEvents.Count; ++i)
                    {
                        var evt = logEvents[i].LogEvent;
                        var json = RenderCompactJsonLine(evt);

                        if (JsonPayloadMaxLength > 0)
                        {
                            if (json.Length > JsonPayloadMaxLength)
                            {
                                InternalLogger.Warn("Seq(Name={0}): Event JSON representation exceeds the char limit: {1} > {2}", Name, json.Length, JsonPayloadMaxLength);
                                continue;
                            }
                            if (totalPayload + json.Length > JsonPayloadMaxLength)
                            {
                                extraBatch = new List<AsyncLogEventInfo>(logEvents.Count - i);
                                for (; i < logEvents.Count; ++i)
                                    extraBatch.Add(logEvents[i]);
                                break;
                            }
                        }

                        totalPayload += json.Length;
                        payload.WriteLine(json);
                    }
                }
            }

            using (var response = (HttpWebResponse)request.GetResponse())
            {
                if ((int)response.StatusCode > 299)
                {
                    var responseStream = response.GetResponseStream();
                    if (responseStream == null)
                        throw new WebException("No response was received from the Seq server");

                    using (var reader = new StreamReader(responseStream))
                    {
                        var data = reader.ReadToEnd();
                        throw new WebException($"Received failed response {response.StatusCode} from Seq server: {data}");
                    }
                }
            }

            var completedCount = logEvents.Count - (extraBatch?.Count ?? 0);
            for (int i = 0; i < completedCount; ++i)
                logEvents[i].Continuation(null);

            if (extraBatch != null)
            {
                PostBatch(extraBatch);
            }
        }

        internal string RenderCompactJsonLine(LogEventInfo evt)
        {
            bool hasProperties = evt.HasProperties && evt.Properties.Count > 0;
            var json = RenderLogEvent(hasProperties ? TemplatedClefLayout : TextClefLayout, evt);
            return json;
        }

        internal void TestInitialize()
        {
            InitializeTarget();
        }
    }
}
