namespace AI_Studio
{
    [Command(PackageIds.Explain)]
    internal sealed class Explain : AIBaseCommand<Explain>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            SystemMessage = "Explain this code.";
            ResponseBehavior = ResponseBehavior.Message;

            var opts = await Commands.GetLiveInstanceAsync();

            UserInput = opts.Explain;

            await base.ExecuteAsync(e);
        }
    }
}
