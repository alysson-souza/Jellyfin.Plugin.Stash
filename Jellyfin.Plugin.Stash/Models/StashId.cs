using Newtonsoft.Json;

namespace Stash.Models
{
    public struct StashId
    {
        [JsonProperty(PropertyName = "endpoint")]
        public string Endpoint { get; set; }

        [JsonProperty(PropertyName = "stash_id")]
        public string Id { get; set; }
    }
}
