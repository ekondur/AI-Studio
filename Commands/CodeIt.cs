namespace AI_Studio
{
    [Command(PackageIds.CodeIt)]
    internal sealed class CodeIt : AIBaseCommand<CodeIt>
    {
        public CodeIt()
        {
            SystemMessage = "Code it by use cases. Write only the code, not the explanation.";
        }
    }
}
