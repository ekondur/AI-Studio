namespace AI_Studio
{
    [Command(PackageIds.CodeIt)]
    internal sealed class CodeIt : AIBaseCommand<CodeIt>
    {
        public CodeIt()
        {
            SystemMessage = "Code it by use cases. Return only code, not explanations.";
        }
    }
}
