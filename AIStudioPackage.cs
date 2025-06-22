global using Community.VisualStudio.Toolkit;
global using Microsoft.VisualStudio.Shell;
global using System;
global using Task = System.Threading.Tasks.Task;
using System.Runtime.InteropServices;
using System.Threading;

namespace AI_Studio
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration(Vsix.Name, Vsix.Description, Vsix.Version)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [Guid(PackageGuids.AI_StudioString)]
    [ProvideOptionPage(typeof(OptionsProvider.GeneralOptions), "AI Studio", "General", 0, 0, true, SupportsProfiles = true)]
    [ProvideOptionPage(typeof(OptionsProvider.UnitTestsOptions), "AI Studio", "Unit Test", 1, 1, true, SupportsProfiles = true)]
    [ProvideOptionPage(typeof(OptionsProvider.CommandsOptions), "AI Studio", "Commands", 2, 2, true, SupportsProfiles = true)]
    [ProvideToolWindow(typeof(OutputToolWindow), Style = VsDockStyle.Tabbed)]
    public sealed class AIStudioPackage : ToolkitPackage
    {
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await this.RegisterCommandsAsync();
        }
    }
}