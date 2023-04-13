namespace AI_Studio
{
    [Command(PackageIds.AddComments)]
    internal sealed class AddComments : AIBaseCommand<AddComments>
    {
        public AddComments()
        {
            SystemMessage = "Refactor this code just adding comments. Return only refactored code, not explanations.";
        }
    }
}
