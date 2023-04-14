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
        [DisplayName("Format Document")]
        [Description("Format current document after change.")]
        [DefaultValue(true)]
        public bool FormatDocument { get; set; }
    }
}
