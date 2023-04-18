namespace AI_Studio
{
    [Command(PackageIds.Refactor)]
    internal sealed class Refactor : AIBaseCommand<Refactor>
    {
        public Refactor()
        {
            SystemMessage = "Refactor this code with best practices. Write only the code, not any explanations.";
        }
    }
}
