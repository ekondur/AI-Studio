namespace AI_Studio
{
    [Command(PackageIds.AddComments)]
    internal sealed class AddComments : AIBaseCommand<AddComments>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            SystemMessage = "Refactor this code just adding comments. Return only refactored code, not explanations.";

            var opts = await Commands.GetLiveInstanceAsync();

            UserInput = opts.AddComments;

            await base.ExecuteAsync(e);
        }
    }
}
