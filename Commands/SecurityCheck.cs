namespace AI_Studio
{
    [Command(PackageIds.SecurityCheck)]
    internal sealed class SecurityCheck : AIBaseCommand<SecurityCheck>
    {
        public SecurityCheck()
        {
            SystemMessage = "Is the code secure, do you have any suggestions to make it safer?";
        }
    }
}
