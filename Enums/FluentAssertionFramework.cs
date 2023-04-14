using System.ComponentModel;

namespace AI_Studio
{
    public enum FluentAssertionFramework
    {
        [Description("Do not use any fluent assertions framework")]
        None,
        [Description("According to this URL https://fluentassertions.com/introduction, use 'FluentAssertions' to assertion.")]
        FluentAssertions,
        [Description("According to this URL https://docs.shouldly.org/, use 'Shouldly' to assertion.")]
        Shouldly,
        [Description("According to this URL https://github.com/tpierrain/NFluent, use 'NFluent' to assertion.")]
        NFluent
    }
}
