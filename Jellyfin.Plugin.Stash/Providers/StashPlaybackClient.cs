using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Stash.Providers
{
    internal sealed class StashPlaybackClient
    {
        private readonly HttpClient http;

        public StashPlaybackClient(HttpClient http)
        {
            this.http = http;
        }

        public async Task<JObject> History(string endpoint, string key, string scene, CancellationToken token)
        {
            var data = await this.Send(endpoint, key,
                "query($id:ID!){findScene(id:$id){play_count last_played_at play_history}}",
                new { id = scene }, token).ConfigureAwait(false);
            return data["findScene"] as JObject ?? throw new InvalidOperationException("Stash scene was not found.");
        }

        public async Task AddPlay(string endpoint, string key, string scene, DateTime timestamp, CancellationToken token)
        {
            var data = await this.Send(endpoint, key,
                "mutation($id:ID!,$time:[Timestamp!]){sceneAddPlay(id:$id,times:$time){count history}}",
                new { id = scene, time = new[] { timestamp.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture) } }, token).ConfigureAwait(false);
            if (!(data["sceneAddPlay"]?["history"] is JArray history)
                || !history.Any(value => value.Value<DateTime>().ToUniversalTime() == timestamp.ToUniversalTime()))
            {
                throw new InvalidOperationException("Stash did not confirm the completion timestamp.");
            }
        }

        public static int Occurrences(JObject history, DateTime timestamp)
        {
            var dates = history["play_history"] as JArray ?? throw new InvalidOperationException("Stash did not return play history.");
            return dates.Count(value => value.Value<DateTime>().ToUniversalTime() == timestamp.ToUniversalTime());
        }

        private async Task<JObject> Send(string endpoint, string key, string query, object variables, CancellationToken token)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Post, endpoint.TrimEnd('/') + "/graphql"))
            {
                if (!string.IsNullOrEmpty(key))
                {
                    request.Headers.Add("ApiKey", key);
                }

                request.Content = new StringContent(JsonConvert.SerializeObject(new { query, variables }), Encoding.UTF8, "application/json");
                using (var response = await this.http.SendAsync(request, token).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();
                    var json = JObject.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
                    if (json["errors"] != null)
                    {
                        throw new InvalidOperationException("Stash rejected the playback history operation.");
                    }

                    return json["data"] as JObject ?? throw new InvalidOperationException("Stash returned no playback history data.");
                }
            }
        }
    }
}
