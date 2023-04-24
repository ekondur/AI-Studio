namespace AI_Studio
{
    [Command(PackageIds.SecurityCheck)]
    internal sealed class SecurityCheck : AIBaseCommand<SecurityCheck>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            SystemMessage = "Is the code secure, do you have any suggestions to make it safer?";

            var opts = await Commands.GetLiveInstanceAsync();

            UserInput = opts.SecurityCheck;

            await base.ExecuteAsync(e);
        }
    }
}
