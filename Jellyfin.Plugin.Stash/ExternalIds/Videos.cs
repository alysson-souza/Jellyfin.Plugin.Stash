using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;

#if __EMBY__
#else
using MediaBrowser.Model.Providers;
#endif

namespace Stash.ExternalIds
{
#if __EMBY__
    public class Videos : IExternalId, IHasWebsite
#else
    public class Videos : IExternalId
#endif
    {
#if __EMBY__
        public string Name => Plugin.Instance.Name;
#else
        public string ProviderName => Plugin.Instance.Name;

        public ExternalIdMediaType? Type => null;
#endif

        public string Key => Plugin.Instance.Name;

#if __EMBY__
        public string UrlFormatString => Plugin.Instance.Configuration.StashEndpoint + "/scenes/{0}";

        public string Website => Plugin.Instance.Configuration.StashEndpoint;
#endif

        public bool Supports(IHasProviderIds item) => item is Video && !(item is MediaBrowser.Controller.Entities.Movies.Movie);
    }
}
