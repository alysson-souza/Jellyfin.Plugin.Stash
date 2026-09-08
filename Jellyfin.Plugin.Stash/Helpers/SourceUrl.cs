using System;
using System.Collections.Generic;
using MediaBrowser.Controller.Entities;

namespace Stash.Helpers
{
    /// <summary>
    /// Writes the source page a Stash record came from to the host's own website field.
    /// </summary>
    /// <remarks>
    /// Jellyfin stores a website on <see cref="BaseItem.HomePageUrl"/>. Emby has no such property and
    /// keeps a website in the reserved "Official Website" provider id, which
    /// <c>BaseItem.SetProviderIds</c> carries across refreshes. Stash can hold several URLs per record,
    /// but neither host stores more than one, so the first usable URL wins.
    /// </remarks>
    internal static class SourceUrl
    {
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

#if __EMBY__
                item.ProviderIds["Official Website"] = parsed.AbsoluteUri;
#else
                item.HomePageUrl = parsed.AbsoluteUri;
#endif
                return;
            }
        }
    }
}
