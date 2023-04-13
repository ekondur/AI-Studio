namespace AI_Studio
{
    [Command(PackageIds.Refactor)]
    internal sealed class Refactor : AIBaseCommand<Refactor>
    {
        public Refactor()
        {
            SystemMessage = "Refactor this code with best practices. Return only refactored code, not explanations.";
        }
    }
}
