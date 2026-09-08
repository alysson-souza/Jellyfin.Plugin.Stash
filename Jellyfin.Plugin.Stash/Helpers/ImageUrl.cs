using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Stash.Helpers
{
    public sealed class ImageUrl : IDisposable
    {
        public const string Route = "Plugins/Stash/Image";

        private const int MaximumImageBytes = 20 * 1024 * 1024;
        private static readonly Regex ResourcePattern = new Regex(
            @"\A/(?:performer/[0-9]+/image|studio/[0-9]+/image|scene/[0-9]+/screenshot|scene/[0-9]+/scene_marker/[0-9]+/screenshot)(?:\?t=-?[0-9]+(?:&default=(?:true|false))?|\?default=(?:true|false))?\z",
            RegexOptions.CultureInvariant);

        private readonly byte[] signingKey;
        private readonly Func<(string Endpoint, string ApiKey)> configuration;
        private readonly Func<string> serverUrl;
        private readonly HttpClient client;

        public ImageUrl(string keyPath, Func<(string Endpoint, string ApiKey)> configuration, Func<string> serverUrl)
        {
            this.signingKey = LoadKey(keyPath);
            this.configuration = configuration;
            this.serverUrl = serverUrl;
            this.client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });
        }

        public string Create(string sourceUrl)
        {
            if (string.IsNullOrEmpty(sourceUrl))
            {
                return sourceUrl;
            }

            var settings = this.configuration();
            var endpoint = Endpoint(settings.Endpoint);
            var source = new Uri(endpoint.AbsoluteUri.TrimEnd('/') + "/");
            if (!Uri.TryCreate(source, sourceUrl, out var image)
                || (image.Scheme != Uri.UriSchemeHttp && image.Scheme != Uri.UriSchemeHttps)
                || image.UserInfo.Length != 0 || image.Fragment.Length != 0)
            {
                throw new ArgumentException("Invalid Stash image URL.", nameof(sourceUrl));
            }

            // Stash may advertise a public origin while the plugin uses its internal address.
            // Only the allowlisted resource is retained; its advertised host is never fetched.
            var path = image.AbsolutePath;
            var prefix = endpoint.AbsolutePath.TrimEnd('/');
            if (prefix.Length != 0 && path.StartsWith(prefix + "/", StringComparison.Ordinal))
            {
                path = path.Substring(prefix.Length);
            }

            string timestamp = null;
            string defaultImage = null;
            foreach (var parameter in image.Query.TrimStart('?').Split('&'))
            {
                if (parameter.Length == 0)
                {
                    continue;
                }

                var pair = parameter.Split(new[] { '=' }, 2);
                if (pair[0].Equals("apikey", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (pair.Length == 2 && pair[0] == "t" && timestamp == null)
                {
                    timestamp = pair[1];
                }
                else if (pair.Length == 2 && pair[0] == "default" && defaultImage == null)
                {
                    defaultImage = pair[1];
                }
                else
                {
                    throw new ArgumentException("Unsupported Stash image parameters.", nameof(sourceUrl));
                }
            }

            var resource = path + (timestamp == null ? string.Empty : "?t=" + timestamp);
            if (defaultImage != null)
            {
                resource += (timestamp == null ? "?" : "&") + "default=" + defaultImage;
            }

            if (!IsResource(resource))
            {
                throw new ArgumentException("Unsupported Stash image resource.", nameof(sourceUrl));
            }

            return this.serverUrl().TrimEnd('/') + "/" + Route
                + "?resource=" + Uri.EscapeDataString(resource)
                + "&signature=" + Uri.EscapeDataString(this.Sign(endpoint, settings.ApiKey, resource));
        }

        public async Task<(byte[] Content, string ContentType)> DownloadAsync(string resource, string signature, CancellationToken cancellationToken)
        {
            return await this.TryDownloadAsync(resource, signature, cancellationToken).ConfigureAwait(false)
                ?? throw new UnauthorizedAccessException("Invalid image signature.");
        }

        public void Dispose() => this.client.Dispose();

        internal async Task<(byte[] Content, string ContentType)?> TryDownloadAsync(string resource, string signature, CancellationToken cancellationToken)
        {
            if (!IsResource(resource) || signature == null || signature.Length != 44)
            {
                return null;
            }

            var settings = this.configuration();
            var endpoint = Endpoint(settings.Endpoint);
            var expected = this.Sign(endpoint, settings.ApiKey, resource);
            if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(signature)))
            {
                return null;
            }

            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                timeout.CancelAfter(TimeSpan.FromSeconds(30));
                var target = endpoint.AbsoluteUri.TrimEnd('/') + resource;
                using (var request = new HttpRequestMessage(HttpMethod.Get, target))
                {
                    if (!string.IsNullOrEmpty(settings.ApiKey))
                    {
                        request.Headers.Add("ApiKey", settings.ApiKey);
                    }

                    using (var response = await this.client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false))
                    {
                        response.EnsureSuccessStatusCode();
                        var mediaType = response.Content.Headers.ContentType?.MediaType;
                        switch (mediaType)
                        {
                            case "image/jpeg":
                            case "image/png":
                            case "image/webp":
                            case "image/gif":
                            case "image/svg+xml":
                            case "image/avif":
                            case "image/bmp":
                            case "image/tiff":
                                break;
                            default:
                                throw new HttpRequestException("Stash did not return a supported image.");
                        }

                        if (response.Content.Headers.ContentLength > MaximumImageBytes)
                        {
                            throw new HttpRequestException("Stash image exceeds the download limit.");
                        }

                        using (var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                        using (var output = new MemoryStream())
                        {
                            var buffer = new byte[81920];
                            int count;
                            while ((count = await input.ReadAsync(buffer, 0, buffer.Length, timeout.Token).ConfigureAwait(false)) != 0)
                            {
                                if (output.Length + count > MaximumImageBytes)
                                {
                                    throw new HttpRequestException("Stash image exceeds the download limit.");
                                }

                                output.Write(buffer, 0, count);
                            }

                            return (output.ToArray(), mediaType);
                        }
                    }
                }
            }
        }

        private static bool IsResource(string resource)
            => resource != null && resource.Length <= 2048 && ResourcePattern.IsMatch(resource);

        private static Uri Endpoint(string value)
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var endpoint)
                || (endpoint.Scheme != Uri.UriSchemeHttp && endpoint.Scheme != Uri.UriSchemeHttps)
                || endpoint.UserInfo.Length != 0 || endpoint.Query.Length != 0 || endpoint.Fragment.Length != 0)
            {
                throw new InvalidOperationException("Configure a valid Stash endpoint before downloading images.");
            }

            return endpoint;
        }

        private static byte[] LoadKey(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            if (!File.Exists(path))
            {
                var temporary = path + "." + Guid.NewGuid().ToString("N");
                var key = new byte[32];
                using (var random = RandomNumberGenerator.Create())
                {
                    random.GetBytes(key);
                }

                try
                {
                    File.WriteAllBytes(temporary, key);
                    try
                    {
                        File.Move(temporary, path);
                    }
                    catch (IOException) when (File.Exists(path))
                    {
                        // Another initialization already installed the persistent key.
                    }
                }
                finally
                {
                    File.Delete(temporary);
                }
            }

            var existing = File.ReadAllBytes(path);
            if (existing.Length != 32)
            {
                throw new InvalidDataException("Invalid image signing key.");
            }

            return existing;
        }

        private string Sign(Uri endpoint, string apiKey, string resource)
        {
            using (var hmac = new HMACSHA256(this.signingKey))
            {
                // Binding to the configuration invalidates links when the Stash server or key changes.
                // There is no clock expiry: hosts can persist remote image URLs between refreshes.
                var message = "stash-image-v1\n" + endpoint.AbsoluteUri.TrimEnd('/') + "\n" + apiKey + "\n" + resource;
                return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(message)));
            }
        }
    }
}
