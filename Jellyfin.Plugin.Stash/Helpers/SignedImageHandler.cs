#if !__EMBY__
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.WebUtilities;

namespace Stash.Helpers
{
    internal sealed class SignedImageHandler : DelegatingHandler
    {
        private readonly Func<ImageUrl> images;

        public SignedImageHandler(Func<ImageUrl> images)
        {
            this.images = images;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Get
                && request.RequestUri.AbsolutePath.EndsWith("/" + ImageUrl.Route, StringComparison.Ordinal))
            {
                var query = QueryHelpers.ParseQuery(request.RequestUri.Query);
                if (query.TryGetValue("resource", out var resource) && resource.Count == 1
                    && query.TryGetValue("signature", out var signature) && signature.Count == 1)
                {
                    var image = await this.images().TryDownloadAsync(resource[0], signature[0], cancellationToken).ConfigureAwait(false);
                    if (image.HasValue)
                    {
                        var content = new ByteArrayContent(image.Value.Content);
                        content.Headers.ContentType = new MediaTypeHeaderValue(image.Value.ContentType);
                        return new HttpResponseMessage(HttpStatusCode.OK) { Content = content, RequestMessage = request };
                    }
                }
            }

            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
    }
}
#endif
