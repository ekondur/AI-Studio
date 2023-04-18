namespace AI_Studio
{
    [Command(PackageIds.AddSummary)]
    internal sealed class AddSummary : AIBaseCommand<AddSummary>
    {
        public AddSummary()
        {
            SystemMessage = "According to the this Url https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/xmldoc/recommended-tags, refactor the code adding summary. Write only the code, not the explanation.";
        }
    }
}
