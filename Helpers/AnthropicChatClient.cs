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
    internal sealed class AnthropicChatClient : IChatClient
    {
        private const string ApiVersion = "2023-06-01";
        private const string MessagesUrl = "https://api.anthropic.com/v1/messages";
        private const int MaxTokens = 8096;

        private readonly HttpClient _http;
        private readonly string _apiKey;
        private readonly string _model;

        internal AnthropicChatClient(string apiKey, string model)
        {
            _apiKey = apiKey;
            _model = model;
            _http = new HttpClient();
        }

        public async Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions options = null,
            CancellationToken cancellationToken = default)
        {
            var requestJson = BuildRequestJson(chatMessages, stream: false);
            using var request = BuildHttpRequest(requestJson);
            using var response = await _http.SendAsync(request, cancellationToken);
            await ThrowIfFailedAsync(response);

            var body = await response.Content.ReadAsStringAsync();
            var text = ParseNonStreamingText(body);
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, text));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var requestJson = BuildRequestJson(chatMessages, stream: true);
            using var request = BuildHttpRequest(requestJson);
            using var response = await _http.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            await ThrowIfFailedAsync(response);

            using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream, Encoding.UTF8);

            while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: ")) continue;

                var data = line.Substring(6).Trim();
                if (data == "[DONE]") break;

                string chunk = null;
                try
                {
                    using var doc = JsonDocument.Parse(data);
                    var root = doc.RootElement;
                    if (!root.TryGetProperty("type", out var typeEl)) continue;
                    if (typeEl.GetString() != "content_block_delta") continue;
                    if (!root.TryGetProperty("delta", out var delta)) continue;
                    if (!delta.TryGetProperty("type", out var deltaType)) continue;
                    if (deltaType.GetString() != "text_delta") continue;
                    if (!delta.TryGetProperty("text", out var textEl)) continue;
                    chunk = textEl.GetString();
                }
                catch { continue; }

                if (string.IsNullOrEmpty(chunk)) continue;

                var update = new ChatResponseUpdate();
                update.Contents.Add(new TextContent(chunk));
                yield return update;
            }
        }

        public void Dispose() => _http.Dispose();

        public object GetService(Type serviceType, object key = null) => null;

        // --- helpers ---

        private HttpRequestMessage BuildHttpRequest(string json)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, MessagesUrl);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            request.Headers.TryAddWithoutValidation("x-api-key", _apiKey);
            request.Headers.TryAddWithoutValidation("anthropic-version", ApiVersion);
            return request;
        }

        private static async Task ThrowIfFailedAsync(HttpResponseMessage response)
        {
            if (response.IsSuccessStatusCode) return;
            var body = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException(
                $"Anthropic API error {(int)response.StatusCode} ({response.ReasonPhrase}): {body}");
        }

        private string BuildRequestJson(IEnumerable<ChatMessage> chatMessages, bool stream)
        {
            string system = null;
            var messages = new List<AnthropicMessage>();

            foreach (var msg in chatMessages)
            {
                if (msg.Role == ChatRole.System)
                {
                    system = msg.Text;
                    continue;
                }

                var role = msg.Role == ChatRole.Assistant ? "assistant" : "user";
                var content = msg.Text;
                if (!string.IsNullOrEmpty(content))
                    messages.Add(new AnthropicMessage { Role = role, Content = content });
            }

            var body = new AnthropicRequest
            {
                Model = _model,
                Messages = messages,
                MaxTokens = MaxTokens,
                Stream = stream,
                System = system
            };

            return JsonSerializer.Serialize(body, new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
        }

        private static string ParseNonStreamingText(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var content = doc.RootElement.GetProperty("content");
                var sb = new StringBuilder();
                foreach (var block in content.EnumerateArray())
                {
                    if (!block.TryGetProperty("type", out var typeEl)) continue;
                    if (typeEl.GetString() != "text") continue;
                    if (block.TryGetProperty("text", out var textEl))
                        sb.Append(textEl.GetString());
                }
                return sb.ToString();
            }
            catch
            {
                return string.Empty;
            }
        }

        // Typed request classes to avoid anonymous-type / List<object> serialization issues

        private sealed class AnthropicRequest
        {
            [JsonPropertyName("model")]
            public string Model { get; set; }

            [JsonPropertyName("messages")]
            public List<AnthropicMessage> Messages { get; set; }

            [JsonPropertyName("max_tokens")]
            public int MaxTokens { get; set; }

            [JsonPropertyName("stream")]
            public bool Stream { get; set; }

            [JsonPropertyName("system")]
            public string System { get; set; }   // null → omitted via WhenWritingNull
        }

        private sealed class AnthropicMessage
        {
            [JsonPropertyName("role")]
            public string Role { get; set; }

            [JsonPropertyName("content")]
            public string Content { get; set; }
        }
    }
}
