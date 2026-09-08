using System;
using Newtonsoft.Json;

namespace Stash.Models
{
    internal sealed class StashChapterMarker
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("seconds")]
        public double? Seconds { get; set; }

        [JsonProperty("screenshot")]
        public string Screenshot { get; set; }

        [JsonProperty("primary_tag")]
        public Tags? PrimaryTag { get; set; }

        public string GetName() => !string.IsNullOrWhiteSpace(this.Title) ? this.Title
            : !string.IsNullOrWhiteSpace(this.PrimaryTag?.Name) ? this.PrimaryTag.Value.Name : "Marker " + this.Id;

        public long GetTicks() => (long)Math.Round(this.Seconds.Value * TimeSpan.TicksPerSecond, MidpointRounding.AwayFromZero);

        public bool TryGetTicks(long? runtimeTicks, out long ticks)
        {
            ticks = 0;
            if (string.IsNullOrWhiteSpace(this.Id) || !this.Seconds.HasValue || double.IsNaN(this.Seconds.Value)
                || double.IsInfinity(this.Seconds.Value) || this.Seconds.Value < 0
                || this.Seconds.Value * TimeSpan.TicksPerSecond >= long.MaxValue)
            {
                return false;
            }

            ticks = this.GetTicks();

            // Unknown/zero runtime cannot establish an upper bound. Never clamp a marker to the end.
            return !runtimeTicks.HasValue || runtimeTicks.Value <= 0 || ticks < runtimeTicks.Value;
        }
    }
}
