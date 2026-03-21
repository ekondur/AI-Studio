using System.ComponentModel;
using System.Runtime.InteropServices;

namespace AI_Studio
{
    internal partial class OptionsProvider
    {
        // Register the options with this attribute on your package class:
        //[ProvideOptionPage(typeof(OptionsProvider.GeneralOptions), "AI_Studio", "General", 0, 0, true, SupportsProfiles = true)]
        [ComVisible(true)]
        public class GeneralOptions : BaseOptionPage<General> { }
    }

    public class General : BaseOptionModel<General>
    {
        [Category("General")]
        [DisplayName("AI Provider")]
        [Description("Select the AI provider to use. OpenAI: requires an API key from platform.openai.com. Anthropic: requires an API key from console.anthropic.com. Ollama: runs locally, no API key needed.")]
        [DefaultValue(AIProvider.OpenAI)]
        [TypeConverter(typeof(EnumConverter))]
        public AIProvider Provider { get; set; } = AIProvider.OpenAI;

        [Category("General")]
        [DisplayName("API Key")]
        [Description("API key for the selected provider. Not required for Ollama. OpenAI: platform.openai.com/account/api-keys. Anthropic: console.anthropic.com/settings/keys.")]
        [PasswordPropertyText(true)]
        public string ApiKey { get; set; }

        [Category("General")]
        [DisplayName("Language Model")]
        [Description("Model name to use. OpenAI examples: gpt-4o-mini, gpt-4o, o4-mini. Anthropic examples: claude-sonnet-4-6, claude-opus-4-6, claude-haiku-4-5. Ollama examples: llama3.2, mistral, codestral.")]
        [DefaultValue("gpt-4o-mini")]
        public string LanguageModel { get; set; } = "gpt-4o-mini";

        [Category("General")]
        [DisplayName("API Endpoint")]
        [Description("Base URL for the API. OpenAI: https://api.openai.com/v1/  Ollama local: http://localhost:11434  Ollama cloud: https://ollama.com  Not used for Anthropic.")]
        [DefaultValue("https://api.openai.com/v1/")]
        public string ApiEndpoint { get; set; } = "https://api.openai.com/v1/";

        [Category("General")]
        [DisplayName("Format Changed Text")]
        [Description("Format text after change.")]
        [DefaultValue(true)]
        public bool FormatChangedText { get; set; } = true;
    }
}
