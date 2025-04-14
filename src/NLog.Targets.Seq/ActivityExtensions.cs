// Seq Target for NLog - Copyright 2014-2020 Datalust and contributors
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

using System.Diagnostics;

namespace NLog.Targets.Seq
{
    /// <summary>
    /// Formats elements of <see cref="Activity.Current"/> for inclusion in log events. Non-W3C-format activities are
    /// ignored (Seq does not support the older Microsoft-proprietary hierarchical activity id format).
    /// </summary>
    internal static class ActivityExtensions
    {
        private static readonly string EmptySpanIdToHexString = default(System.Diagnostics.ActivitySpanId).ToHexString();
        private static readonly string EmptyTraceIdToHexString = default(System.Diagnostics.ActivityTraceId).ToHexString();

        public static string GetSpanId(this Activity activity)
        {
            return activity.IdFormat == ActivityIdFormat.W3C ?
                SpanIdToHexString(activity.SpanId) :
                string.Empty;
        }

        public static string GetTraceId(this Activity activity)
        {
            return activity.IdFormat == ActivityIdFormat.W3C ?
                TraceIdToHexString(activity.TraceId) :
                string.Empty;
        }

        private static string SpanIdToHexString(ActivitySpanId spanId)
        {
            var spanIdString = spanId.ToHexString();
            return EmptySpanIdToHexString.Equals(spanIdString, System.StringComparison.Ordinal) ? string.Empty : spanIdString;
        }

        private static string TraceIdToHexString(ActivityTraceId traceId)
        {
            var traceIdString = traceId.ToHexString();
            return EmptyTraceIdToHexString.Equals(traceIdString, System.StringComparison.Ordinal) ? string.Empty : traceIdString;
        }
    }
}