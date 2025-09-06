namespace AI_Studio
{
    [Command(PackageIds.AddUnitTests)]
    internal sealed class AddUnitTests : AIBaseCommand<AddUnitTests>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            SystemMessage = "Create Unit Test methods. Return with markdown format.";
            ResponseBehavior = ResponseBehavior.Message;

            var opts = await UnitTests.GetLiveInstanceAsync();

            UserInput = opts.Customize;

            if (!string.IsNullOrEmpty(opts.UnitTestingFramework))
            {
                AssistantInputs.Add($"UnitTesting framework is: {opts.UnitTestingFramework}");
            }

            if (!string.IsNullOrEmpty(opts.IsolationFramework))
            {
                AssistantInputs.Add($"Isolation framework is: {opts.IsolationFramework}");
            }

            if (!string.IsNullOrEmpty(opts.TestDataFramework))
            {
                AssistantInputs.Add($"Test/Dummy Data framework is: {opts.TestDataFramework}");
            }

            if (!string.IsNullOrEmpty(opts.AssertionFramework))
            {
                AssistantInputs.Add($"Assertions framework is: {opts.AssertionFramework}");
            }

            await base.ExecuteAsync(e);
        }
    }
}
