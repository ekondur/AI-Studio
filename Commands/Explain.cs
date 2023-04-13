namespace AI_Studio
{
    [Command(PackageIds.Explain)]
    internal sealed class Explain : AIBaseCommand<Explain>
    {
        public Explain()
        {
            SystemMessage = "Explain this code.";
        }
    }
}
