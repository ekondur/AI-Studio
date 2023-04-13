using System.ComponentModel;

namespace AI_Studio
{
    public enum UnitTestingFramework
    {
        [Description("According to this URL https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-with-mstest, use MSTest as main framework")]
        MSTest,
        [Description("According to this URL https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-with-nunit, use xUnit as main framework")]
        xUnit,
        [Description("According to this URL https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-with-nunit, use NUnit as main framework")]
        NUnit
    }
}
