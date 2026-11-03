using System.ComponentModel;
using System.Runtime.InteropServices;

namespace AI_Studio
{
    internal partial class OptionsProvider
    {
        // Register the options with this attribute on your package class:
        // [ProvideOptionPage(typeof(OptionsProvider.UnitTestsOptions), "AI_Studio", "UnitTests", 0, 0, true, SupportsProfiles = true)]
        [ComVisible(true)]
        public class UnitTestsOptions : BaseOptionPage<UnitTests> { }
    }

    public class UnitTests : BaseOptionModel<UnitTests>
    {
        [Category("Existing Test Context")]
        [DisplayName("Use Existing Test Context")]
        [Description("For loaded C# projects, send related existing tests, base classes, setup, fixtures and helpers to the configured AI provider alongside production dependencies. Existing test conventions take precedence over fallback framework preferences.")]
        [DefaultValue(true)]
        public bool UseExistingTestContext { get; set; } = true;

        [Category("Existing Test Context")]
        [DisplayName("Preferred Test Project")]
        [Description("Optional exact project name used to restrict reference test discovery, for example MyApp.UnitTests. Leave empty to discover tests in the selected project and projects referencing it. Useful for keeping unit and integration test infrastructure separate.")]
        public string PreferredTestProject { get; set; }

        [Category("Unit Test Settings")]
        [DisplayName("Unit Testing Framework")]
        [Description("Select unit testing framewok to setup main functionalities.")]
        [DefaultValue("MSTest")]
        public string UnitTestingFramework { get; set; } = "MSTest";

        [Category("Unit Test Settings")]
        [DisplayName("Isolation Framework")]
        [Description("An isolation framework is a set of programmable APIs that makes creating fake objects much simpler, faster, and shorter than hand-coding them.")]
        [DefaultValue("Moq")]
        public string IsolationFramework { get; set; } = "Moq";

        [Category("Unit Test Settings")]
        [DisplayName("Test/Dummy Data Framework")]
        [Description("Test Data Builders and Dummy Data Generators.")]
        [DefaultValue("AutoFixture")] 
        public string TestDataFramework { get; set; } = "AutoFixture";

        [Category("Unit Test Settings")]
        [DisplayName("Assertion Framework")]
        [Description("Assertion frameworks is a set of .NET extension methods that allow you to more naturally specify the expected outcome of a TDD or BDD-style unit test.")]
        [DefaultValue("FluentAssertions")] 
        public string AssertionFramework { get; set; } = "FluentAssertions";

        [Category("Unit Test Settings")]
        [DisplayName("Customize")]
        [Description("Add any other details to customize unit tests.")]
        public string Customize { get; set; }
    }
}
