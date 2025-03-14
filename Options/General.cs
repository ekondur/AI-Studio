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
        [DisplayName("API Key")]
        [Description("AI Studio utilizes Chat GPT API, to use this extension create an API Key and add it here.")]
        public string ApiKey { get; set; }

        [Category("General")]
        [DisplayName("Format Changed Text")]
        [Description("Format text after change.")]
        [DefaultValue(true)]
        public bool FormatChangedText { get; set; } = true;

        [Category("General")]
        [DisplayName("Language Model")]
        [Description("Chat language model")]
        [DefaultValue(ChatLanguageModel.ChatGPTTurbo)]
        public ChatLanguageModel LanguageModel { get; set; } = ChatLanguageModel.ChatGPTTurbo;

        [Category("Custom Model")]
        [DisplayName("Organization")]
        [Description("Name of the organization or owner of the model.")]
        [DefaultValue("organization_owner")]
        public string OrganizationOwner { get; set; } = "";

        [Category("Custom Model")]
        [DisplayName("Custom Language Model ID")]
        [Description("Provide the language model ID to use. (Example: deepseek-coder)")]
        [DefaultValue("")]
        public string CustomLanguageModel { get; set; } = "";

        [Category("General")]
        [DisplayName("API Endpoint")]
        [Description("URL containing the OpenAI API endpoint and request format ({0}=version, {1}=request)")]
        [DefaultValue("https://api.openai.com/{0}/{1}")]
        public string ApiEndpoint { get; set; } = "https://api.openai.com/{0}/{1}";

    }
}
