using System;
using System.Collections.Generic;
using MediaBrowser.Controller.Entities;

namespace Stash.Helpers
{
    /// <summary>
    /// Stores the first HTTP(S) source page for the native external-link provider.
    /// </summary>
    internal static class SourceUrl
    {
        public const string ProviderKey = "Stash source";

        public static void Apply(BaseItem item, List<string> urls)
        {
            if (item == null || urls == null)
            {
                return;
            }

            foreach (var url in urls)
            {
                if (string.IsNullOrWhiteSpace(url)
                    || !Uri.TryCreate(url.Trim(), UriKind.Absolute, out var parsed)
                    || (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
                {
                    continue;
                }

                item.ProviderIds[ProviderKey] = parsed.AbsoluteUri;
                return;
            }
        }
    }
}
