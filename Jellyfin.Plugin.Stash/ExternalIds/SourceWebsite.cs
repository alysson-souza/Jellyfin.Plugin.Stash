using System;
using System.Collections.Generic;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using Stash.Helpers;

#if !__EMBY__
using MediaBrowser.Model.Providers;
#endif

namespace Stash.ExternalIds
{
#if __EMBY__
    public sealed class SourceWebsite : IExternalId
#else
    public sealed class SourceWebsite : IExternalId, IExternalUrlProvider
#endif
    {
        public string Name => SourceUrl.ProviderKey;

        public string Key => SourceUrl.ProviderKey;

#if __EMBY__
        public string UrlFormatString => "{0}";
#else
        public string ProviderName => this.Name;

        public ExternalIdMediaType? Type => null;
#endif

        public bool Supports(IHasProviderIds item) => item is Video || item is Person || item is BoxSet;

#if !__EMBY__
        public IEnumerable<string> GetExternalUrls(BaseItem item)
        {
            if (this.Supports(item) && item.TryGetProviderId(this.Key, out var value)
                && Uri.TryCreate(value, UriKind.Absolute, out var url)
                && (url.Scheme == Uri.UriSchemeHttp || url.Scheme == Uri.UriSchemeHttps))
            {
                yield return url.AbsoluteUri;
            }
        }
#endif
    }
}
