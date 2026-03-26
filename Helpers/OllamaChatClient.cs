using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
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
    /// IChatClient for Ollama using the native /api/chat endpoint.
    /// Supports both local (http://localhost:11434) and cloud (https://ollama.com) instances.
    /// </summary>
    internal sealed class OllamaChatClient : IChatClient
    {
        private static readonly HttpClient _http = new HttpClient();
        private readonly string _model;
        private readonly string _chatUrl;
        private readonly string _apiKey;

        internal OllamaChatClient(string model, string baseEndpoint, string apiKey = null)
        {
            _model = model;
            _apiKey = apiKey;

            // Normalize: strip trailing slash and any trailing /api suffix, then append /api/chat.
            // This accepts both "https://ollama.com" and "https://ollama.com/api" as input.
            // Local:  http://localhost:11434      → http://localhost:11434/api/chat
            // Cloud:  https://ollama.com          → https://ollama.com/api/chat
            // Cloud:  https://ollama.com/api      → https://ollama.com/api/chat (stripped)
            var baseUrl = baseEndpoint.TrimEnd('/');
            if (baseUrl.EndsWith("/api", StringComparison.OrdinalIgnoreCase))
                baseUrl = baseUrl.Substring(0, baseUrl.Length - 4);
            _chatUrl = baseUrl + "/api/chat";
        }

        public async Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions options = null,
            CancellationToken cancellationToken = default)
        {
            var json = BuildRequestJson(chatMessages, stream: false);
            using var request = new HttpRequestMessage(HttpMethod.Post, _chatUrl);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            if (!string.IsNullOrEmpty(_apiKey))
                request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + _apiKey);

            using var response = await _http.SendAsync(request, cancellationToken);
            await ThrowIfFailedAsync(response);

            var body = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            var content = doc.RootElement
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? string.Empty;

            return new ChatResponse(new ChatMessage(ChatRole.Assistant, content));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var json = BuildRequestJson(chatMessages, stream: true);
            using var request = new HttpRequestMessage(HttpMethod.Post, _chatUrl);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            if (!string.IsNullOrEmpty(_apiKey))
                request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + _apiKey);

            using var response = await _http.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            await ThrowIfFailedAsync(response).ConfigureAwait(false);

            using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            using var reader = new System.IO.StreamReader(stream, Encoding.UTF8);

            while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync().ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(line)) continue;

                string content = null;
                bool done = false;
                try
                {
                    using var doc = JsonDocument.Parse(line);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("done", out var doneEl) && doneEl.GetBoolean())
                        done = true;

                    if (root.TryGetProperty("message", out var messageEl) &&
                        messageEl.TryGetProperty("content", out var contentEl))
                    {
                        content = contentEl.GetString();
                    }
                }
                catch (JsonException) { continue; }

                if (!string.IsNullOrEmpty(content))
                {
                    var update = new ChatResponseUpdate();
                    update.Contents.Add(new TextContent(content));
                    yield return update;
                }

                if (done) break;
            }
        }

        public void Dispose() { }

        public object GetService(Type serviceType, object key = null) => null;

        private static async Task ThrowIfFailedAsync(HttpResponseMessage response)
        {
            if (response.IsSuccessStatusCode) return;
            var body = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException(
                $"Ollama API error {(int)response.StatusCode} ({response.ReasonPhrase}) at {response.RequestMessage?.RequestUri}: {body}");
        }

        private string BuildRequestJson(IEnumerable<ChatMessage> chatMessages, bool stream)
        {
            var messages = new List<OllamaMessage>();
            foreach (var msg in chatMessages)
            {
                string role;
                if (msg.Role == ChatRole.System) role = "system";
                else if (msg.Role == ChatRole.Assistant) role = "assistant";
                else role = "user";

                var text = msg.Text;
                if (!string.IsNullOrEmpty(text))
                    messages.Add(new OllamaMessage { Role = role, Content = text });
            }

            return JsonSerializer.Serialize(new OllamaRequest
            {
                Model = _model,
                Messages = messages,
                Stream = stream
            });
        }

        private sealed class OllamaRequest
        {
            [JsonPropertyName("model")]
            public string Model { get; set; }

            [JsonPropertyName("messages")]
            public List<OllamaMessage> Messages { get; set; }

            [JsonPropertyName("stream")]
            public bool Stream { get; set; }
        }

        private sealed class OllamaMessage
        {
            [JsonPropertyName("role")]
            public string Role { get; set; }

            [JsonPropertyName("content")]
            public string Content { get; set; }
        }
    }
}
