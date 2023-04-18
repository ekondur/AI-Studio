namespace AI_Studio
{
    [Command(PackageIds.AddUnitTests)]
    internal sealed class AddUnitTests : AIBaseCommand<AddUnitTests>
    {
        public AddUnitTests()
        {
            SystemMessage = "Create Unit Test methods with the user inputs. Write only the code, not any explanations.";
        }
    }
}
