using System.ComponentModel;

namespace AI_Studio
{
    public enum TestDataFramework
    {
        [Description("Do not use any test/dummy data framework")]
        None,
        [Description("According to this URL https://github.com/AutoFixture/AutoFixture, use AutoFixture as Test/Dummy data framework.")]
        AutoFixture,
        [Description("According to this URL https://github.com/bchavez/Bogus, use Bogus as Test/Dummy data framework.")]
        Bogus,
        [Description("According to this URL https://github.com/MisterJames/GenFu, use GenFu as Test/Dummy data framework.")]
        GenFu,
        [Description("According to this URL https://github.com/nbuilder/nbuilder, use NBuilder as Test/Dummy data framework.")]
        NBuilder,
        [Description("According to this URL https://github.com/nickdodd79/AutoBogus, use AutoBogus as Test/Dummy data framework.")]
        AutoBogus
    }
}
