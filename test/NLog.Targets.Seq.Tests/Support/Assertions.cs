using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NLog.Targets.Seq.Tests.Support
{
    static class Assertions
    {
        static readonly JsonSerializerSettings _settings = new JsonSerializerSettings
        {
            DateParseHandling = DateParseHandling.None
        };

        public static JObject AssertValidJson(string json)
        {
            // Unfortunately this will not detect all JSON formatting issues; better than nothing however.
            return JsonConvert.DeserializeObject<JObject>(json, _settings);
        }
    }
}