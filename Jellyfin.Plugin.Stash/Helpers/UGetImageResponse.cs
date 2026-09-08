using System.Threading;
using System.Threading.Tasks;

#if __EMBY__
using MediaBrowser.Common.Net;
#else
using System.Net.Http;
using MediaBrowser.Common.Net;
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

            return Plugin.Http.GetResponse(options);
        }
#else
        public static async Task<HttpResponseMessage> SendAsync(string url, CancellationToken cancellationToken)
        {
            using (var client = Plugin.Http.CreateClient(NamedClient.Default))
            {
                return await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
            }
        }
#endif
    }
}
