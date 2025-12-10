#nullable enable

using System;
using System.ComponentModel.DataAnnotations;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Stash.Helpers;
using Stash.Models;

namespace Stash.Api
{
    /// <summary>
    /// API controller for Stash plugin operations.
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("Plugins/Stash")]
    public class StashController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="StashController"/> class.
        /// </summary>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        public StashController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
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
                return BadRequest(new TestConnectionResult
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
                using var httpClient = _httpClientFactory.CreateClient();
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
                    return Ok(new TestConnectionResult
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
                    return Ok(new TestConnectionResult
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
                    return Ok(new TestConnectionResult
                    {
                        Success = true,
                        Message = $"Connected! Stash responded with {sceneCount} scenes.",
                        SceneCount = sceneCount,
                    });
                }

                return Ok(new TestConnectionResult
                {
                    Success = false,
                    Message = "Received a response, but it was not in the expected format.",
                });
            }
            catch (OperationCanceledException)
            {
                return Ok(new TestConnectionResult
                {
                    Success = false,
                    Message = "Connection timed out after 10 seconds.",
                });
            }
            catch (HttpRequestException ex)
            {
                return Ok(new TestConnectionResult
                {
                    Success = false,
                    Message = $"Connection failed: {ex.Message}",
                });
            }
            catch (JsonException ex)
            {
                Logger.Error($"TestConnection JSON parse error: {ex.Message}");
                return Ok(new TestConnectionResult
                {
                    Success = false,
                    Message = "Failed to parse response from server.",
                });
            }
            catch (UriFormatException)
            {
                return Ok(new TestConnectionResult
                {
                    Success = false,
                    Message = "Invalid endpoint URL format.",
                });
            }
        }
    }
}
