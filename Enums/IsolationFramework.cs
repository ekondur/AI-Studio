using System.ComponentModel;

namespace AI_Studio
{
    public enum IsolationFramework
    {
        [Description("Do not use any isolation framework")]
        None,
        [Description("According to this URL https://github.com/Moq/moq4, use Moq as isolation framework")]
        Moq,
        [Description("According to this URL https://github.com/FakeItEasy/FakeItEasy, use FakeItEasy as isolation framework")]
        FakeItEasy,
        [Description("According to this URL https://github.com/nsubstitute/NSubstitute, use NSubstitute as isolation framework")]
        NSubstitute
    }
}
