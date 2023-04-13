namespace AI_Studio
{
    [Command(PackageIds.AddUnitTests)]
    internal sealed class AddUnitTests : AIBaseCommand<AddUnitTests>
    {
        public AddUnitTests()
        {
            SystemMessage = "Create Unit Test methods,  Return only code, not explanations.";
        }
    }
}
