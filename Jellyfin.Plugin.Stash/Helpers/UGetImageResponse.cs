using System.Threading;
using System.Threading.Tasks;

#if __EMBY__
using MediaBrowser.Common.Net;
#else
using System.Net.Http;
#endif

namespace Stash.Helpers
{
    public static class UGetImageResponse
    {
#if __EMBY__
        public static Task<HttpResponseInfo> SendAsync(string url, CancellationToken cancellationToken)
        {
            var options = new HttpRequestOptions
            {
                CancellationToken = cancellationToken,
                Url = url,
                EnableDefaultUserAgent = false,
            };

            if (!string.IsNullOrEmpty(Plugin.Instance.Configuration.StashAPIKey))
            {
                options.RequestHeaders["APIKey"] = Plugin.Instance.Configuration.StashAPIKey;
            }

            return Plugin.Http.GetResponse(options);
        }
#else
        public static Task<HttpResponseMessage> SendAsync(string url, CancellationToken cancellationToken)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (!string.IsNullOrEmpty(Plugin.Instance.Configuration.StashAPIKey))
            {
                request.Headers.Add("APIKey", Plugin.Instance.Configuration.StashAPIKey);
            }

            return Plugin.Http.CreateClient().SendAsync(request, cancellationToken);
        }
#endif
    }
}
