using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace AI_Studio.Helpers
{
    /// <summary>
    /// IChatClient for Azure AI / Azure OpenAI v1 endpoints.
    /// Uses Azure's api-key header while keeping the OpenAI-style chat payload.
    /// </summary>
    internal sealed class AzureAIChatClient : IChatClient
    {
        private static readonly HttpClient _http = new HttpClient();
        private readonly string _model;
        private readonly string _apiKey;
        private readonly string _completionsUrl;

        internal AzureAIChatClient(string model, string apiKey, string endpoint)
        {
            _model = model;
            _apiKey = apiKey;
            _completionsUrl = NormalizeEndpoint(endpoint);
        }

        public async Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions options = null,
            CancellationToken cancellationToken = default)
        {
            var requestJson = BuildRequestJson(chatMessages, stream: false);
            using var request = BuildHttpRequest(requestJson);
            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            await ThrowIfFailedAsync(response).ConfigureAwait(false);

            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var content = ParseNonStreamingText(body);
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, content));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var requestJson = BuildRequestJson(chatMessages, stream: true);
            using var request = BuildHttpRequest(requestJson);
            using var response = await _http.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            await ThrowIfFailedAsync(response).ConfigureAwait(false);

            using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            using var reader = new StreamReader(stream, Encoding.UTF8);

            while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync().ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: ")) continue;

                var data = line.Substring(6).Trim();
                if (data == "[DONE]") break;

                string content = null;
                try
                {
                    using var doc = JsonDocument.Parse(data);
                    var choices = doc.RootElement.GetProperty("choices");
                    if (choices.GetArrayLength() == 0) continue;
                    var delta = choices[0].GetProperty("delta");
                    if (!delta.TryGetProperty("content", out var contentEl)) continue;
                    content = ParseTextContent(contentEl);
                }
                catch (JsonException) { continue; }

                if (string.IsNullOrEmpty(content)) continue;

                var update = new ChatResponseUpdate();
                update.Contents.Add(new TextContent(content));
                yield return update;
            }
        }

        public void Dispose() { }

        public object GetService(Type serviceType, object key = null) => null;

        private HttpRequestMessage BuildHttpRequest(string json)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, _completionsUrl);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            if (!string.IsNullOrWhiteSpace(_apiKey))
            {
                if (_apiKey.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    request.Headers.TryAddWithoutValidation("Authorization", _apiKey);
                else
                    request.Headers.TryAddWithoutValidation("api-key", _apiKey);
            }

            return request;
        }

        private static string NormalizeEndpoint(string endpoint)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
                throw new ArgumentException(
                    "Azure AI endpoint is missing. Use a base URL like https://<resource>.openai.azure.com/openai/v1/.");

            var baseUrl = endpoint.Trim();
            if (baseUrl.IndexOf("api.openai.com", StringComparison.OrdinalIgnoreCase) >= 0)
                throw new ArgumentException(
                    "Azure AI provider requires an Azure endpoint like https://<resource>.openai.azure.com/openai/v1/.");

            const string chatCompletionsSuffix = "/chat/completions";
            if (baseUrl.EndsWith(chatCompletionsSuffix, StringComparison.OrdinalIgnoreCase))
                baseUrl = baseUrl.Substring(0, baseUrl.Length - chatCompletionsSuffix.Length);

            baseUrl = baseUrl.TrimEnd('/');
            if (!baseUrl.EndsWith("/openai/v1", StringComparison.OrdinalIgnoreCase))
            {
                if (baseUrl.EndsWith("/openai", StringComparison.OrdinalIgnoreCase))
                    baseUrl += "/v1";
                else if (baseUrl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
                    baseUrl = baseUrl.Substring(0, baseUrl.Length - 3) + "/openai/v1";
                else
                    baseUrl += "/openai/v1";
            }

            return baseUrl + "/chat/completions";
        }

        private static async Task ThrowIfFailedAsync(HttpResponseMessage response)
        {
            if (response.IsSuccessStatusCode) return;
            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            throw new HttpRequestException(
                $"Azure AI API error {(int)response.StatusCode} ({response.ReasonPhrase}) at {response.RequestMessage?.RequestUri}: {body}");
        }

        private string BuildRequestJson(IEnumerable<ChatMessage> chatMessages, bool stream)
        {
            var messages = new List<AzureOpenAIMessage>();
            foreach (var msg in chatMessages)
            {
                string role;
                if (msg.Role == ChatRole.System) role = "system";
                else if (msg.Role == ChatRole.Assistant) role = "assistant";
                else role = "user";

                var content = msg.Text;
                if (!string.IsNullOrEmpty(content))
                    messages.Add(new AzureOpenAIMessage { Role = role, Content = content });
            }

            return JsonSerializer.Serialize(new AzureOpenAIRequest
            {
                Model = _model,
                Messages = messages,
                Stream = stream
            });
        }

        private static string ParseNonStreamingText(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var choices = doc.RootElement.GetProperty("choices");
                if (choices.GetArrayLength() == 0) return string.Empty;
                var message = choices[0].GetProperty("message");
                if (!message.TryGetProperty("content", out var contentEl)) return string.Empty;
                return ParseTextContent(contentEl);
            }
            catch (JsonException)
            {
                return string.Empty;
            }
        }

        private static string ParseTextContent(JsonElement contentElement)
        {
            if (contentElement.ValueKind == JsonValueKind.String)
                return contentElement.GetString() ?? string.Empty;

            if (contentElement.ValueKind != JsonValueKind.Array)
                return string.Empty;

            var sb = new StringBuilder();
            foreach (var item in contentElement.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    sb.Append(item.GetString());
                    continue;
                }

                if (item.ValueKind != JsonValueKind.Object) continue;
                if (item.TryGetProperty("text", out var textEl))
                    sb.Append(textEl.GetString());
            }

            return sb.ToString();
        }

        private sealed class AzureOpenAIRequest
        {
            [JsonPropertyName("model")]
            public string Model { get; set; }

            [JsonPropertyName("messages")]
            public List<AzureOpenAIMessage> Messages { get; set; }

            [JsonPropertyName("stream")]
            public bool Stream { get; set; }
        }

        private sealed class AzureOpenAIMessage
        {
            [JsonPropertyName("role")]
            public string Role { get; set; }

            [JsonPropertyName("content")]
            public string Content { get; set; }
        }
    }
}
