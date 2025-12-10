#nullable enable

namespace Stash.Models
{
    /// <summary>
    /// Result of a connection test.
    /// </summary>
    public class TestConnectionResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether the connection was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the result message.
        /// </summary>
        public string? Message { get; set; }

        /// <summary>
        /// Gets or sets the scene count from Stash (if successful).
        /// </summary>
        public int? SceneCount { get; set; }
    }
}
