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

using NLog.Config;
using NLog.Layouts;

namespace NLog.Targets.Seq
{
    /// <summary>
    /// Configures a property that enriches events sent to Seq.
    /// </summary>
    [NLogConfigurationItem]
    public sealed class SeqPropertyItem
    {
        /// <summary>
        /// The name of the property.
        /// </summary>
        [RequiredParameter]
        public string Name { get; set; }

        /// <summary>
        /// The value of the property.
        /// </summary>
        [RequiredParameter]
        public Layout Value { get; set; }

        /// <summary>
        /// Gets or sets whether value should be handled as string-value.
        /// </summary>
        /// <remarks>
        /// Matches <see cref="NLog.Layouts.JsonAttribute.Encode"/>
        /// </remarks>
        public bool AsString { get; set; } = true;

        /// <summary>
        /// Either "string", which is the default, or "number", which
        /// will cause values of this type to be converted to numbers for
        /// storage.
        /// </summary>
        public string As
        {
            get => AsString ? "string" : "number";
            set => AsString = value != "number";
        }
    }
}