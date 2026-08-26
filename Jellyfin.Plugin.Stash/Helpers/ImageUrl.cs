using System;
using System.Net;

namespace Stash.Helpers
{
    /// <summary>
    /// Builds Stash image URLs that a media server can download without plugin help.
    ///
    /// Stash never puts the API key in the image paths it returns from GraphQL
    /// (see <c>urlbuilders</c> in the Stash source), and an unauthenticated request is
    /// redirected to the login page, which answers with HTML. Emby downloads and saves
    /// remote images itself instead of going through the provider, so the key has to
    /// travel in the URL.
    /// </summary>
    public static class ImageUrl
    {
        private const string ApiKeyParameter = "apikey";

        /// <summary>
        /// Returns <paramref name="url"/> with <paramref name="apiKey"/> added as a query
        /// parameter, or unchanged when there is no URL, no key, or a key already present.
        /// </summary>
        /// <param name="url">The image URL reported by Stash.</param>
        /// <param name="apiKey">The configured Stash API key.</param>
        /// <returns>An image URL that authenticates itself.</returns>
        public static string WithApiKey(string url, string apiKey)
        {
            if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(apiKey) || HasApiKey(url))
            {
                return url;
            }

            var separator = url.IndexOf('?') < 0 ? '?' : '&';

            return string.Concat(url, separator.ToString(), ApiKeyParameter, "=", WebUtility.UrlEncode(apiKey));
        }

        private static bool HasApiKey(string url)
        {
            var queryStart = url.IndexOf('?');
            if (queryStart < 0)
            {
                return false;
            }

            foreach (var parameter in url.Substring(queryStart + 1).Split('&'))
            {
                var nameEnd = parameter.IndexOf('=');
                var name = nameEnd < 0 ? parameter : parameter.Substring(0, nameEnd);

                if (name.Equals(ApiKeyParameter, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
