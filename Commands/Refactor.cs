namespace AI_Studio
{
    [Command(PackageIds.Refactor)]
    internal sealed class Refactor : AIBaseCommand<Refactor>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            SystemMessage = "Refactor this code with best practices. Write only the code, not the explanation.";

            var opts = await Commands.GetLiveInstanceAsync();

            UserInput = opts.Refactor;

            await base.ExecuteAsync(e);
        }
    }
}
