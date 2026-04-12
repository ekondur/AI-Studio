using Microsoft.Extensions.AI;

namespace AI_Studio.Helpers
{
    internal static class ChatClientFactory
    {
        public static IChatClient Create(General options)
        {
            switch (options.Provider)
            {
                case AIProvider.AzureAI:
                    return new AzureAIChatClient(
                        model: options.LanguageModel,
                        apiKey: options.ApiKey,
                        endpoint: options.ApiEndpoint);

                case AIProvider.Anthropic:
                    return new AnthropicChatClient(
                        apiKey: options.ApiKey,
                        model: options.LanguageModel);

                case AIProvider.Ollama:
                    return new OllamaChatClient(
                        model: options.LanguageModel,
                        baseEndpoint: options.ApiEndpoint,
                        apiKey: options.ApiKey);

                default: // AIProvider.OpenAI
                    return new OpenAIChatClient(
                        model: options.LanguageModel,
                        apiKey: options.ApiKey,
                        endpoint: options.ApiEndpoint);
            }
        }

        /// <summary>
        /// Returns true when the selected provider requires an API key.
        /// </summary>
        public static bool RequiresApiKey(AIProvider provider) =>
            provider != AIProvider.Ollama;
    }
}
