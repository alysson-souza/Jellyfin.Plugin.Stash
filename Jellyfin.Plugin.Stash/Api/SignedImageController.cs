using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

#if __EMBY__
using System.Collections.Generic;
using System.Net;
using MediaBrowser.Controller.Net;
using MediaBrowser.Model.Services;
#else
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
#endif

namespace Stash.Api
{
#if __EMBY__
    public sealed class SignedImageController : IService, IRequiresRequest
    {
        private readonly IHttpResultFactory results;

        public SignedImageController(IHttpResultFactory results)
        {
            this.results = results;
        }

        public IRequest Request { get; set; }

        public async Task<object> Get(SignedImageRequest request)
        {
            var cancellationToken = this.Request.CancellationToken;
#else
    [ApiController]
    [AllowAnonymous]
    [Route(Helpers.ImageUrl.Route)]
    public sealed class SignedImageController : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] string resource, [FromQuery] string signature, CancellationToken cancellationToken)
        {
#endif
            try
            {
#if __EMBY__
                var image = await Plugin.Instance.Images.DownloadAsync(request.Resource, request.Signature, cancellationToken).ConfigureAwait(false);
                return this.results.GetResult(this.Request, image.Content, image.ContentType, new Dictionary<string, string>
                {
                    { "Cache-Control", "private, max-age=3600" },
                    { "X-Content-Type-Options", "nosniff" },
                    { "Content-Security-Policy", "default-src 'none'; style-src 'unsafe-inline'; sandbox" },
                    { "Referrer-Policy", "no-referrer" },
                });
#else
                var image = await Plugin.Instance.Images.DownloadAsync(resource, signature, cancellationToken).ConfigureAwait(false);
                this.Response.Headers["Cache-Control"] = "private, max-age=3600";
                this.Response.Headers["X-Content-Type-Options"] = "nosniff";
                this.Response.Headers["Content-Security-Policy"] = "default-src 'none'; style-src 'unsafe-inline'; sandbox";
                this.Response.Headers["Referrer-Policy"] = "no-referrer";
                return this.File(image.Content, image.ContentType);
#endif
            }
            catch (UnauthorizedAccessException)
            {
                return this.Failure(403);
            }
            catch (HttpRequestException)
            {
                return this.Failure(502);
            }
            catch (IOException)
            {
                return this.Failure(502);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return this.Failure(504);
            }
            catch (InvalidOperationException)
            {
                return this.Failure(503);
            }
        }

#if __EMBY__
        private object Failure(int status)
        {
            var result = this.results.GetResult(this.Request, Array.Empty<byte>(), "text/plain", new Dictionary<string, string> { { "Cache-Control", "no-store" } });
            ((IHttpResult)result).StatusCode = (HttpStatusCode)status;
            return result;
        }
#else
        private IActionResult Failure(int status) => this.StatusCode(status);
#endif
    }
}
