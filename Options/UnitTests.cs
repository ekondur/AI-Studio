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
