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

using System;

namespace NLog.Targets.Seq
{
    static class LevelMapping
    {
        public static LogLevel ToNLogLevel(SeqLogLevel? level)
        {
            switch (level)
            {
                case null:
                case SeqLogLevel.Verbose:
                    return LogLevel.Trace;
                case SeqLogLevel.Debug:
                    return LogLevel.Debug;
                case SeqLogLevel.Information:
                    return LogLevel.Info;
                case SeqLogLevel.Warning:
                    return LogLevel.Warn;
                case SeqLogLevel.Error:
                    return LogLevel.Error;
                case SeqLogLevel.Fatal:
                    return LogLevel.Fatal;
                default:
                    throw new ArgumentOutOfRangeException(nameof(level), level, null);
            }
        }
    }
}
