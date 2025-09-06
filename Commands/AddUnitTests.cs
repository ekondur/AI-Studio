namespace AI_Studio
{
    [Command(PackageIds.AddUnitTests)]
    internal sealed class AddUnitTests : AIBaseCommand<AddUnitTests>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            SystemMessage = "Create Unit Test methods with the user inputs. Return with markdown format.";
            ResponseBehavior = ResponseBehavior.Message;

            var opts = await UnitTests.GetLiveInstanceAsync();

            UserInput = opts.Customize;

            AssistantInputs.Add(opts.UnitTestingFramework.GetEnumDescription());
            AssistantInputs.Add(opts.IsolationFramework.GetEnumDescription());
            AssistantInputs.Add(opts.TestDataFramework.GetEnumDescription());
            AssistantInputs.Add(opts.FluentAssertionFramework.GetEnumDescription());

            await base.ExecuteAsync(e);
        }
    }
}
