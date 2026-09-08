using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Stash.Helpers;
using Xunit;

namespace Stash.Tests
{
    public sealed class ImageUrlTests : IDisposable
    {
        private const string ApiKey = "private-stash-credential";
        private readonly string directory = Path.Combine(Path.GetTempPath(), "stash-signed-test-" + Guid.NewGuid().ToString("N"));

        [Theory]
        [InlineData("/performer/1/image?t=123")]
        [InlineData("/studio/7/image?t=2&default=true")]
        [InlineData("/scene/9/screenshot?t=3")]
        [InlineData("/scene/9/scene_marker/4/screenshot")]
        public async Task SignedResourceDownloadsAnAuthenticatedImageWithoutExposingCredentials(string resource)
        {
            using var upstream = new ImageServer(ApiKey);
            using var proxy = this.Create(upstream.Url);
            var signed = proxy.Create("https://public-stash.example" + resource);
            Assert.StartsWith("http://media.example/library/Plugins/Stash/Image?", signed);
            Assert.DoesNotContain(ApiKey, signed);
            Assert.DoesNotContain("apikey", signed, StringComparison.OrdinalIgnoreCase);
            var (path, signature) = Parameters(signed);
            var image = await proxy.DownloadAsync(path, signature, CancellationToken.None);
            Assert.Equal(ImageServer.Picture, image.Content);
            Assert.Equal("image/png", image.ContentType);
            Assert.Equal(resource, upstream.LastPath);
        }

        [Fact]
        public async Task ReverseProxyPrefixesAndOldCredentialsAreHandledWithoutForwardingTheirHost()
        {
            using var upstream = new ImageServer(ApiKey);
            using var proxy = this.Create(upstream.Url + "/stash");
            var signed = proxy.Create("https://public.example/stash/performer/1/image?apikey=" + ApiKey + "&default=true&t=1");
            Assert.DoesNotContain(ApiKey, signed);
            var (path, signature) = Parameters(signed);
            await proxy.DownloadAsync(path, signature, CancellationToken.None);
            Assert.Equal("/stash/performer/1/image?t=1&default=true", upstream.LastPath);
        }

        [Theory]
        [InlineData("/performer/2/image?t=1")]
        [InlineData("/graphql")]
        [InlineData("/performer/1/image?t=2")]
        [InlineData("/scene/9/stream")]
        [InlineData("http://example.com/performer/1/image")]
        [InlineData("/performer/1/../2/image")]
        [InlineData("/performer/1/image?t=1&apikey=injected")]
        [InlineData("/performer/1/image%3Ft=1")]
        public async Task ChangedResourcesAreRejectedBeforeAnyUpstreamRequest(string replacement)
        {
            using var upstream = new ImageServer(ApiKey);
            using var proxy = this.Create(upstream.Url);
            var (_, signature) = Parameters(proxy.Create(upstream.Url + "/performer/1/image?t=1"));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => proxy.DownloadAsync(replacement, signature, CancellationToken.None));
            Assert.Equal(0, upstream.Requests);
        }

        [Fact]
        public async Task MissingOrForgedSignaturesNeverFetchAnImage()
        {
            using var upstream = new ImageServer(ApiKey);
            using var proxy = this.Create(upstream.Url);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => proxy.DownloadAsync("/performer/1/image", null, CancellationToken.None));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => proxy.DownloadAsync("/performer/1/image", new string('a', 44), CancellationToken.None));
            Assert.Equal(0, upstream.Requests);
        }

        [Fact]
        public async Task StoredLinksSurviveRestartButNotConfigurationChanges()
        {
            using var upstream = new ImageServer(ApiKey);
            string url;
            using (var original = this.Create(upstream.Url))
            {
                url = original.Create(upstream.Url + "/performer/1/image");
            }

            var (path, signature) = Parameters(url);
            using (var restarted = this.Create(upstream.Url))
            {
                Assert.Equal(ImageServer.Picture, (await restarted.DownloadAsync(path, signature, CancellationToken.None)).Content);
            }

            using var changedKey = this.Create(upstream.Url, "different-key");
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => changedKey.DownloadAsync(path, signature, CancellationToken.None));
            using var changedServer = this.Create(upstream.Url + "/another-server");
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => changedServer.DownloadAsync(path, signature, CancellationToken.None));
            Assert.Equal(1, upstream.Requests);
        }

        [Fact]
        public async Task StashWithoutAuthenticationStillRequiresAnUnforgeableImageLink()
        {
            using var upstream = new ImageServer(string.Empty);
            using var proxy = this.Create(upstream.Url, string.Empty);
            var (path, signature) = Parameters(proxy.Create(upstream.Url + "/studio/7/image"));
            Assert.Equal(ImageServer.Picture, (await proxy.DownloadAsync(path, signature, CancellationToken.None)).Content);
            using var otherInstallation = new ImageUrl(Path.Combine(this.directory, "other.key"), () => (upstream.Url, string.Empty), () => "http://media.example");
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => otherInstallation.DownloadAsync(path, signature, CancellationToken.None));
        }

        [Fact]
        public async Task RedirectsCannotForwardCredentialsToAnotherServer()
        {
            using var destination = new ImageServer(ApiKey);
            using var upstream = new ImageServer(ApiKey) { Redirect = destination.Url + "/collect" };
            using var proxy = this.Create(upstream.Url);
            var (path, signature) = Parameters(proxy.Create(upstream.Url + "/performer/1/image"));
            await Assert.ThrowsAsync<HttpRequestException>(() => proxy.DownloadAsync(path, signature, CancellationToken.None));
            Assert.Equal(0, destination.Requests);
        }

        [Fact]
        public async Task LoginPagesAreNotReturnedAsImages()
        {
            using var upstream = new ImageServer(ApiKey) { MediaType = "text/html" };
            using var proxy = this.Create(upstream.Url);
            var (path, signature) = Parameters(proxy.Create(upstream.Url + "/performer/1/image"));
            await Assert.ThrowsAsync<HttpRequestException>(() => proxy.DownloadAsync(path, signature, CancellationToken.None));
        }

        [Fact]
        public async Task OversizedResponsesAreRejected()
        {
            using var upstream = new ImageServer(ApiKey) { Oversized = true };
            using var proxy = this.Create(upstream.Url);
            var (path, signature) = Parameters(proxy.Create(upstream.Url + "/scene/9/screenshot"));
            await Assert.ThrowsAsync<HttpRequestException>(() => proxy.DownloadAsync(path, signature, CancellationToken.None));
        }

        [Theory]
        [InlineData("http://stash.example/graphql")]
        [InlineData("http://stash.example/scene/9/stream")]
        [InlineData("file:///performer/1/image")]
        [InlineData("http://stash.example/performer/1/image?target=http://elsewhere")]
        public void NonImageResourcesCannotBeSigned(string url)
        {
            using var proxy = this.Create("http://stash.example");
            Assert.Throws<ArgumentException>(() => proxy.Create(url));
        }

        [Fact]
        public async Task ServerDownloadDoesNotResolveTheBrowserOrigin()
        {
            using var upstream = new ImageServer(ApiKey);
            using var proxy = new ImageUrl(
                Path.Combine(this.directory, "signing.key"),
                () => (upstream.Url, ApiKey),
                () => "https://media.invalid:9443/library");
            using var client = new HttpClient(new SignedImageHandler(() => proxy)
            {
                InnerHandler = new UnreachableServer(),
            });

            var image = await client.GetByteArrayAsync(proxy.Create(upstream.Url + "/performer/1/image"));
            Assert.Equal(ImageServer.Picture, image);
        }

        [Fact]
        public async Task ImagesSignedByAnotherServerRemainExternalRequests()
        {
            using var upstream = new ImageServer(ApiKey);
            using var proxy = this.Create(upstream.Url);
            using var other = new ImageUrl(
                Path.Combine(this.directory, "other.key"),
                () => (upstream.Url, ApiKey),
                () => "https://other.invalid/library");
            using var client = new HttpClient(new SignedImageHandler(() => proxy)
            {
                InnerHandler = new UnreachableServer(),
            });

            using var response = await client.GetAsync(other.Create(upstream.Url + "/performer/1/image"));
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        }

        public void Dispose()
        {
            if (Directory.Exists(this.directory))
            {
                Directory.Delete(this.directory, true);
            }
        }

        private static (string Resource, string Signature) Parameters(string url)
        {
            var query = new Uri(url).Query.TrimStart('?').Split('&').Select(p => p.Split('=', 2))
                .ToDictionary(p => p[0], p => Uri.UnescapeDataString(p[1]));
            return (query["resource"], query["signature"]);
        }

        private ImageUrl Create(string endpoint, string key = ApiKey)
            => new ImageUrl(Path.Combine(this.directory, "signing.key"), () => (endpoint, key), () => "http://media.example/library");

        private sealed class UnreachableServer : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        }
    }
}
