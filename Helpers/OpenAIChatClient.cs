using Microsoft.Extensions.AI;
using OpenAI.Chat;
using System.ClientModel;
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
    /// IChatClient wrapper for OpenAI-compatible endpoints.
    /// Non-streaming uses OpenAI.Chat.ChatClient (no async-enumerable dependency).
    /// Streaming uses raw HttpClient SSE parsing to avoid the Microsoft.Bcl.AsyncInterfaces
    /// version conflict (8.0/9.0/10.0) that breaks AsyncCollectionResult on .NET Framework 4.8.
    /// </summary>
    internal sealed class OpenAIChatClient : IChatClient
    {
        private readonly ChatClient _chatClient;
        private static readonly HttpClient _http = new HttpClient();
        private readonly string _model;
        private readonly string _apiKey;
        private readonly string _completionsUrl;

        internal OpenAIChatClient(string model, string apiKey, string endpoint)
        {
            _model = model;
            _apiKey = apiKey;

            var baseUrl = endpoint.TrimEnd('/') + "/";
            _completionsUrl = baseUrl + "chat/completions";

            var credential = string.IsNullOrEmpty(apiKey)
                ? new ApiKeyCredential("ollama")
                : new ApiKeyCredential(apiKey);
            _chatClient = new ChatClient(model: model, credential: credential,
                options: new OpenAI.OpenAIClientOptions { Endpoint = new Uri(baseUrl) });
        }

        public async Task<ChatResponse> GetResponseAsync(
            IEnumerable<Microsoft.Extensions.AI.ChatMessage> chatMessages,
            ChatOptions options = null,
            CancellationToken cancellationToken = default)
        {
            var msgs = BuildOpenAiMessages(chatMessages);
            var result = await _chatClient.CompleteChatAsync(msgs, cancellationToken: cancellationToken);
            var sb = new StringBuilder();
            foreach (var part in result.Value.Content)
                if (part.Text != null) sb.Append(part.Text);
            return new ChatResponse(new Microsoft.Extensions.AI.ChatMessage(
                Microsoft.Extensions.AI.ChatRole.Assistant, sb.ToString()));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<Microsoft.Extensions.AI.ChatMessage> chatMessages,
            ChatOptions options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var requestJson = BuildRequestJson(chatMessages, stream: true);

            using var request = new HttpRequestMessage(HttpMethod.Post, _completionsUrl);
            request.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");
            if (!string.IsNullOrEmpty(_apiKey))
                request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + _apiKey);

            using var response = await _http.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                throw new HttpRequestException(
                    $"API error {(int)response.StatusCode} ({response.ReasonPhrase}) at {_completionsUrl}: {errorBody}");
            }

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
                    content = contentEl.GetString();
                }
                catch (JsonException) { continue; }

                if (string.IsNullOrEmpty(content)) continue;

                var update = new ChatResponseUpdate();
                update.Contents.Add(new Microsoft.Extensions.AI.TextContent(content));
                yield return update;
            }
        }

        public void Dispose() { }

        public object GetService(Type serviceType, object key = null) => null;

        private static List<OpenAI.Chat.ChatMessage> BuildOpenAiMessages(
            IEnumerable<Microsoft.Extensions.AI.ChatMessage> chatMessages)
        {
            var msgs = new List<OpenAI.Chat.ChatMessage>();
            foreach (var msg in chatMessages)
            {
                if (msg.Role == Microsoft.Extensions.AI.ChatRole.System)
                    msgs.Add(new SystemChatMessage(msg.Text));
                else if (msg.Role == Microsoft.Extensions.AI.ChatRole.Assistant)
                    msgs.Add(new AssistantChatMessage(msg.Text));
                else
                    msgs.Add(new UserChatMessage(msg.Text));
            }
            return msgs;
        }

        private string BuildRequestJson(
            IEnumerable<Microsoft.Extensions.AI.ChatMessage> chatMessages, bool stream)
        {
            var messages = new List<OpenAIMessage>();
            foreach (var msg in chatMessages)
            {
                string role;
                if (msg.Role == Microsoft.Extensions.AI.ChatRole.System) role = "system";
                else if (msg.Role == Microsoft.Extensions.AI.ChatRole.Assistant) role = "assistant";
                else role = "user";
                var content = msg.Text;
                if (!string.IsNullOrEmpty(content))
                    messages.Add(new OpenAIMessage { Role = role, Content = content });
            }
            return JsonSerializer.Serialize(new OpenAIRequest
            {
                Model = _model,
                Messages = messages,
                Stream = stream
            });
        }

        private sealed class OpenAIRequest
        {
            [JsonPropertyName("model")]
            public string Model { get; set; }

            [JsonPropertyName("messages")]
            public List<OpenAIMessage> Messages { get; set; }

            [JsonPropertyName("stream")]
            public bool Stream { get; set; }
        }

        private sealed class OpenAIMessage
        {
            [JsonPropertyName("role")]
            public string Role { get; set; }

            [JsonPropertyName("content")]
            public string Content { get; set; }
        }
    }
}
