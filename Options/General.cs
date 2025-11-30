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
        [PasswordPropertyText(true)]
        public string ApiKey { get; set; }

        [Category("General")]
        [DisplayName("Format Changed Text")]
        [Description("Format text after change.")]
        [DefaultValue(true)]
        public bool FormatChangedText { get; set; } = true;

        [Category("General")]
        [DisplayName("Language Model")]
        [Description("Chat language model")]
        [DefaultValue("o4-mini")]
        public string LanguageModel { get; set; } = "o4-mini";

        [Category("General")]
        [DisplayName("API Endpoint")]
        [Description("URL containing the OpenAI API endpoint format.")]
        [DefaultValue("https://api.openai.com/v1/")]
        public string ApiEndpoint { get; set; } = "https://api.openai.com/v1/";

    }
}
