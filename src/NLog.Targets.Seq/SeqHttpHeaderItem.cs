using NLog.Config;
using NLog.Layouts;

namespace NLog.Targets.Seq
{
    /// <summary>
    /// Represents an additional HTTP header that wil be attached to outgoing HTTP requests made by
    /// <see cref="SeqTarget"/>.
    /// </summary>
    [NLogConfigurationItem]
    public sealed class SeqHttpHeaderItem
    {
        /// <summary>
        /// The name of the HTTP header to add.
        /// </summary>
        [RequiredParameter]
        public string Name { get; set; }
        
        /// <summary>
        /// The value of the HTTP header.
        /// </summary>
        [RequiredParameter]
        public Layout Value { get; set; }
    }
}