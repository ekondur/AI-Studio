namespace AI_Studio
{
    [Command(PackageIds.CodeIt)]
    internal sealed class CodeIt : AIBaseCommand<CodeIt>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            SystemMessage = "Code it by use cases. Write only the code, not the explanation.";

            var opts = await Commands.GetLiveInstanceAsync();

            UserInput = opts.CodeIt;

            await base.ExecuteAsync(e);
        }
    }
}
