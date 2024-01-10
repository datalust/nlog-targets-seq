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
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using NLog.Common;
using NLog.Config;
using NLog.Layouts;

// ReSharper disable MemberCanBePrivate.Global

namespace NLog.Targets.Seq
{
    /// <summary>
    /// Writes events over HTTP to a Seq server.
    /// </summary>
    [Target("Seq")]
    public sealed class SeqTarget : Target
    {
        Layout _serverUrl;
        Layout _apiKey;
        Layout _proxyAddress;
        readonly JsonLayout _compactLayout = new CompactJsonLayout();
        readonly char[] _reusableEncodingBuffer = new char[32 * 1024];  // Avoid large-object-heap

        Uri _webRequestUri;
        string _headerApiKey;
        HttpClient _httpClient;
        LogLevel _minimumLevel = LogLevel.Trace;

        static readonly UTF8Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        /// <summary>
        /// The layout used to format `LogEvent`s as compact JSON.
        /// </summary>
        public JsonLayout TemplatedClefLayout => _compactLayout;

        /// <summary>
        /// The layout used to format `LogEvent`s as compact JSON.
        /// </summary>
        public JsonLayout TextClefLayout => _compactLayout;

        /// <summary>
        /// Maximum size allowed for JSON payload sent to Seq-Server. Discards log events that are larger than limit.
        /// </summary>
        public int JsonPayloadMaxLength { get; set; }

        /// <summary>
        /// The address of the Seq server to write to.
        /// </summary>
        [RequiredParameter]
        public string ServerUrl { get => (_serverUrl as SimpleLayout)?.Text; set => _serverUrl = value ?? string.Empty; }

        /// <summary>
        /// A Seq <i>API key</i> that authenticates the client to the Seq server.
        /// </summary>
        public string ApiKey { get => (_apiKey as SimpleLayout)?.Text; set => _apiKey = value ?? string.Empty; }

        /// <summary>
        /// The address of the proxy to use, including port separated by a colon. If not provided, default operating system proxy will be used.
        /// </summary>
        public string ProxyAddress { get => (_proxyAddress as SimpleLayout)?.Text; set => _proxyAddress = value ?? string.Empty; }

        /// <summary>
        /// Use default credentials
        /// </summary>
        public bool UseDefaultCredentials { get; set; }

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
            get => _compactLayout.MaxRecursionLimit;
            set => _compactLayout.MaxRecursionLimit = value;
        }

        /// <summary>
        /// Gets or sets whether to include the contents of the <see cref="ScopeContext"/> dictionary.
        /// </summary>
        public bool IncludeScopeProperties
        {
            get => _compactLayout.IncludeScopeProperties;
            set => _compactLayout.IncludeScopeProperties = value;
        }

        /// <summary>
        /// List of property names to exclude from LogEventInfo-Properties or ScopeContext-Properties.
        /// </summary>
        public ISet<string> ExcludeProperties
        {
            get => _compactLayout.ExcludeProperties;
            set => _compactLayout.ExcludeProperties = value;
        }

        /// <summary>
        /// Construct a <see cref="SeqTarget"/>.
        /// </summary>
        public SeqTarget()
        {
            Properties = new List<SeqPropertyItem>();
            MaxRecursionLimit = 0;  // Default behavior for Serilog
            JsonPayloadMaxLength = 128 * 1024;
        }

        /// <summary>
        /// Initializes the target. Can be used by inheriting classes
        /// to initialize logging.
        /// </summary>
        protected override void InitializeTarget()
        {
            foreach (var prop in Properties)
            {
                var attr = new JsonAttribute(prop.Name, prop.Value, prop.AsString);
                AddJsonAttribute(_compactLayout, attr);
            }

            if (!string.IsNullOrEmpty(ServerUrl))
            {
                var uri = _serverUrl?.Render(LogEventInfo.CreateNullEvent()) ?? string.Empty;
                if (!uri.EndsWith("/", StringComparison.InvariantCulture))
                    uri += "/";
                uri += SeqApi.BulkUploadResource;
                _webRequestUri = new Uri(uri);

                _headerApiKey = _apiKey?.Render(LogEventInfo.CreateNullEvent()) ?? string.Empty;

                HttpClientHandler handler = null;

                var proxyAddress = _proxyAddress?.Render(LogEventInfo.CreateNullEvent()) ?? string.Empty;
                if (!string.IsNullOrEmpty(proxyAddress))
                    handler = new HttpClientHandler { Proxy = new WebProxy(new Uri(proxyAddress), true) };
                if (UseDefaultCredentials)
                {
                    if (handler != null)
                    {
                        handler.UseDefaultCredentials = UseDefaultCredentials;
                    }
                    else
                    {
                        handler = new HttpClientHandler { UseDefaultCredentials = UseDefaultCredentials };
                    }
                }

                _httpClient = handler == null ? new HttpClient() : new HttpClient(handler);
            }

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
                PostBatch(logEvents).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                InternalLogger.Error(ex, "Seq(Name={0}): Failed sending LogEvents. Uri={1}", Name, _webRequestUri);
                if (LogManager.ThrowExceptions)
                    throw;

                for (var i = 0; i < logEvents.Count; ++i)
                    logEvents[i].Continuation(ex);
            }
        }

        /// <summary>
        /// Writes logging event to Seq.
        /// </summary>
        /// <param name="logEvent">Logging event to be written.</param>
        protected override void Write(AsyncLogEventInfo logEvent)
        {
            try
            {
                PostBatch(new[] { logEvent }).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                InternalLogger.Error(ex, "Seq(Name={0}): Failed sending LogEvents. Uri={1}", Name, _webRequestUri);
                if (LogManager.ThrowExceptions)
                    throw;

                logEvent.Continuation(ex);
            }
        }

        async Task PostBatch(IList<AsyncLogEventInfo> logEvents)
        {
            if (_webRequestUri == null)
                return;

            List<AsyncLogEventInfo> extraBatch = null;
            var payload = new StringBuilder();

            for (var i = 0; i < logEvents.Count; ++i)
            {
                var evt = logEvents[i].LogEvent;

                if (evt.Level < _minimumLevel)
                    continue;

                int orgLength = payload.Length;
                var jsonLength = RenderCompactJsonLine(evt, payload);
                if (jsonLength > JsonPayloadMaxLength)
                {
                    InternalLogger.Warn("Seq(Name={0}): LogEvent JSON-length exceeds JsonPayloadMaxLength: {1} > {2}", Name, jsonLength, JsonPayloadMaxLength);
                    payload.Length = orgLength;
                    logEvents[i].Continuation(new ArgumentException("Seq LogEvent JSON-length exceeds JsonPayloadMaxLength"));
                }
                else if (payload.Length > JsonPayloadMaxLength)
                {
                    extraBatch = new List<AsyncLogEventInfo>(logEvents.Count - i);
                    for (; i < logEvents.Count; ++i)
                        extraBatch.Add(logEvents[i]);
                    payload.Length = orgLength;
                    break;
                }
                else if (jsonLength > 0)
                {
                    payload.AppendLine();
                }
            }

            if (payload.Length > 0)
            {
                await SendPayload(payload).ConfigureAwait(false);
            }

            var completedCount = logEvents.Count - (extraBatch?.Count ?? 0);
            for (var i = 0; i < completedCount; ++i)
                logEvents[i].Continuation(null);

            if (extraBatch != null)
            {
                await PostBatch(extraBatch).ConfigureAwait(false);
            }
        }

        private async Task SendPayload(StringBuilder payload)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, _webRequestUri);
            if (!string.IsNullOrWhiteSpace(_headerApiKey))
                request.Headers.Add(SeqApi.ApiKeyHeaderName, _headerApiKey);

            var httpContent = new ByteArrayContent(EncodePayload(Utf8, payload));
            httpContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(SeqApi.CompactLogEventFormatMediaType) { CharSet = Utf8.WebName };
            request.Content = httpContent;

            // Even if no events are above `_minimumLevel`, we'll send a batch to make sure we observe minimum
            // level changes sent by the server.

            using (var response = await _httpClient.SendAsync(request).ConfigureAwait(false))
            {
                if ((int)response.StatusCode > 299)
                {
                    var data = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    throw new WebException($"Received failed response {response.StatusCode} from Seq server: {data}");
                }

                if ((int)response.StatusCode == (int)HttpStatusCode.Created)
                {
                    var data = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var serverRequestedLevel = LevelMapping.ToNLogLevel(SeqApi.ReadMinimumAcceptedLevel(data));
                    if (serverRequestedLevel != _minimumLevel)
                    {
                        InternalLogger.Info("Seq(Name={0}): Setting minimum log level to {1} per server request", Name, serverRequestedLevel);
                        _minimumLevel = serverRequestedLevel;
                    }
                }
            }
        }

        private byte[] EncodePayload(Encoding encoder, StringBuilder payload)
        {
            lock (_reusableEncodingBuffer)
            {
                int totalLength = payload.Length;
                if (totalLength < _reusableEncodingBuffer.Length)
                {
                    payload.CopyTo(0, _reusableEncodingBuffer, 0, payload.Length);
                    return encoder.GetBytes(_reusableEncodingBuffer, 0, totalLength);
                }
                else
                {
                    return encoder.GetBytes(payload.ToString());
                }
            }
        }

        internal int RenderCompactJsonLine(LogEventInfo evt, StringBuilder payload)
        {
            var orgLength = payload.Length;
            _compactLayout.Render(evt, payload);
            var jsonLength = payload.Length - orgLength;
            return jsonLength;
        }

        internal void TestInitialize()
        {
            InitializeTarget();
        }

        private static void AddJsonAttribute(JsonLayout jsonLayout, JsonAttribute jsonAttribute)
        {
            for (int i = jsonLayout.Attributes.Count - 1; i >= 0; --i)
            {
                if (jsonLayout.Attributes[i].Name == jsonAttribute.Name)
                {
                    jsonLayout.Attributes.RemoveAt(i);
                }
            }

            jsonLayout.Attributes.Add(jsonAttribute);
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _httpClient?.Dispose();
            base.Dispose(disposing);
        }
    }
}
