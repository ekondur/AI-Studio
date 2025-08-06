namespace AI_Studio
{
    [Command(PackageIds.AddSummary)]
    internal sealed class AddSummary : AIBaseCommand<AddSummary>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            SystemMessage = "According to the this Url https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/xmldoc/recommended-tags, refactor the code adding summary. Write only the code, not the explanation.";
            ResponseBehavior = ResponseBehavior.Replace;

            var opts = await Commands.GetLiveInstanceAsync();

            UserInput = opts.AddSummary;
            _stripResponseMarkdownCode = true;
            _addContentTypePrefix = true;

            await base.ExecuteAsync(e);
        }
    }
}
