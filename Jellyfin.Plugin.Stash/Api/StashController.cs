#nullable enable

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Stash.Helpers;
using Stash.Helpers.Utils;
using Stash.Models;

#if __EMBY__
using MediaBrowser.Model.Services;
#else
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
#endif

namespace Stash.Api
{
#if __EMBY__
    /// <summary>
    /// Request DTO for testing connection to Stash server.
    /// </summary>
    [Route("/Plugins/Stash/TestConnection", "GET")]
    public class TestConnectionRequest : IReturn<TestConnectionResult>
    {
        public string Endpoint { get; set; } = string.Empty;

        public string? ApiKey { get; set; }
    }

    /// <summary>
    /// API service for Stash plugin operations (Emby).
    /// </summary>
    public class StashService : IService
    {
        public async Task<object> Get(TestConnectionRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Endpoint))
            {
                return new TestConnectionResult
                {
                    Success = false,
                    Message = "Endpoint is required.",
                };
            }

            try
            {
                var endpoint = request.Endpoint.Trim().TrimEnd('/');
                var url = endpoint + "/graphql?query=" + Uri.EscapeDataString("{ stats { scene_count } }");

                var headers = new Dictionary<string, string>
                {
                    { "Accept", "application/json" },
                };

                if (!string.IsNullOrEmpty(request.ApiKey))
                {
                    headers.Add("ApiKey", request.ApiKey);
                }

                var response = await HTTP.Request(url, CancellationToken.None, headers).ConfigureAwait(false);

                if (!response.IsOK)
                {
                    return new TestConnectionResult
                    {
                        Success = false,
                        Message = "HTTP request failed.",
                    };
                }

                var json = JObject.Parse(response.Content);

                if (json["errors"] != null && json["errors"]!.HasValues)
                {
                    var errorMessage = json["errors"]![0]?["message"]?.ToString() ?? "Unknown GraphQL error";
                    return new TestConnectionResult
                    {
                        Success = false,
                        Message = $"GraphQL error: {errorMessage}",
                    };
                }

                var sceneCountToken = json["data"]?["stats"]?["scene_count"];
                if (sceneCountToken != null && sceneCountToken.Type == JTokenType.Integer)
                {
                    var sceneCount = (int)sceneCountToken;
                    return new TestConnectionResult
                    {
                        Success = true,
                        Message = $"Connected! Stash responded with {sceneCount} scenes.",
                        SceneCount = sceneCount,
                    };
                }

                return new TestConnectionResult
                {
                    Success = false,
                    Message = "Received a response, but it was not in the expected format.",
                };
            }
            catch (Exception ex)
            {
                Logger.Error($"TestConnection error: {ex.Message}");
                return new TestConnectionResult
                {
                    Success = false,
                    Message = $"Connection failed: {ex.Message}",
                };
            }
        }
    }
#else
    /// <summary>
    /// API controller for Stash plugin operations (Jellyfin).
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("Plugins/Stash")]
    public class StashController : ControllerBase
    {
        private readonly IHttpClientFactory httpClientFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="StashController"/> class.
        /// </summary>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        public StashController(IHttpClientFactory httpClientFactory)
        {
            this.httpClientFactory = httpClientFactory;
        }

        /// <summary>
        /// Tests the connection to the Stash server.
        /// </summary>
        /// <param name="endpoint">The Stash server endpoint URL.</param>
        /// <param name="apiKey">The optional API key for authentication.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Connection test result.</returns>
        [HttpGet("TestConnection")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<TestConnectionResult>> TestConnection(
            [FromQuery, Required] string endpoint,
            [FromQuery] string? apiKey,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                return this.BadRequest(new TestConnectionResult
                {
                    Success = false,
                    Message = "Endpoint is required.",
                });
            }

            try
            {
                // Normalize endpoint
                endpoint = endpoint.Trim().TrimEnd('/');
                var url = new Uri(new Uri(endpoint), "graphql");
                using var httpClient = this.httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromSeconds(10);

                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Add("Accept", "application/json");

                if (!string.IsNullOrEmpty(apiKey))
                {
                    request.Headers.Add("ApiKey", apiKey);
                }

                request.Content = new StringContent(
                    """{ "query": "{ stats { scene_count } }" }""",
                    Encoding.UTF8,
                    "application/json");

                using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    return this.Ok(new TestConnectionResult
                    {
                        Success = false,
                        Message = $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}",
                    });
                }

                var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                using var json = JsonDocument.Parse(content);
                var root = json.RootElement;

                if (root.TryGetProperty("errors", out var errors) && errors.GetArrayLength() > 0)
                {
                    var errorMessage = errors[0].TryGetProperty("message", out var msg)
                        ? msg.GetString()
                        : "Unknown GraphQL error";
                    return this.Ok(new TestConnectionResult
                    {
                        Success = false,
                        Message = $"GraphQL error: {errorMessage}",
                    });
                }

                if (root.TryGetProperty("data", out var data) &&
                    data.TryGetProperty("stats", out var stats) &&
                    stats.TryGetProperty("scene_count", out var sceneCountElement) &&
                    sceneCountElement.TryGetInt32(out var sceneCount))
                {
                    return this.Ok(new TestConnectionResult
                    {
                        Success = true,
                        Message = $"Connected! Stash responded with {sceneCount} scenes.",
                        SceneCount = sceneCount,
                    });
                }

                return this.Ok(new TestConnectionResult
                {
                    Success = false,
                    Message = "Received a response, but it was not in the expected format.",
                });
            }
            catch (OperationCanceledException)
            {
                return this.Ok(new TestConnectionResult
                {
                    Success = false,
                    Message = "Connection timed out after 10 seconds.",
                });
            }
            catch (HttpRequestException ex)
            {
                return this.Ok(new TestConnectionResult
                {
                    Success = false,
                    Message = $"Connection failed: {ex.Message}",
                });
            }
            catch (JsonException ex)
            {
                Logger.Error($"TestConnection JSON parse error: {ex.Message}");
                return this.Ok(new TestConnectionResult
                {
                    Success = false,
                    Message = "Failed to parse response from server.",
                });
            }
            catch (UriFormatException)
            {
                return this.Ok(new TestConnectionResult
                {
                    Success = false,
                    Message = "Invalid endpoint URL format.",
                });
            }
        }
    }
#endif
}
