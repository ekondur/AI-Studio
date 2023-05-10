using System.ComponentModel;
using System.Runtime.InteropServices;

namespace AI_Studio
{
    internal partial class OptionsProvider
    {
        // Register the options with this attribute on your package class:
        // [ProvideOptionPage(typeof(OptionsProvider.CommandsOptions), "AI_Studio", "Commands", 0, 0, true, SupportsProfiles = true)]
        [ComVisible(true)]
        public class CommandsOptions : BaseOptionPage<Commands> { }
    }

    public class Commands : BaseOptionModel<Commands>
    {
        [Category("Commands")]
        [DisplayName("Add Summary")]
        [Description("Add some suggestions here for Chat GPT to customize 'Add Summary' command.")]
        public string AddSummary { get; set; }

        [Category("Commands")]
        [DisplayName("Add Comments")]
        [Description("Add some suggestions here for Chat GPT to customize 'Add Comments' command.")]
        public string AddComments { get; set; }

        [Category("Commands")]
        [DisplayName("Refactor")]
        [Description("Add some suggestions here for Chat GPT to customize 'Refactor' command.")]
        public string Refactor { get; set; }

        [Category("Commands")]
        [DisplayName("Explain")]
        [Description("Add some suggestions here for Chat GPT to customize 'Explain' command.")]
        public string Explain { get; set; }

        [Category("Commands")]
        [DisplayName("Code It")]
        [Description("Add some suggestions here for Chat GPT to customize 'Code It' command.")]
        public string CodeIt { get; set; } = "No explanation";

        [Category("Commands")]
        [DisplayName("Security Check")]
        [Description("Add some suggestions here for Chat GPT to customize 'Security Check' command.")]
        public string SecurityCheck { get; set; }
    }
}
