global using Community.VisualStudio.Toolkit;

global using Microsoft.VisualStudio.Shell;

global using System;

global using Task = System.Threading.Tasks.Task;

using System.Runtime.InteropServices;

namespace MaxLineLength
{
    internal static class PackageGuids
    {
        public const string MaxLineLengthString = "49044f8f-9144-4a29-b462-8abeb171f8bd";
    }

    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration(Vsix.Name, Vsix.Description, Vsix.Version)]
    [Guid(PackageGuids.MaxLineLengthString)]
    public sealed class MaxLineLengthPackage : ToolkitPackage
    {
    }
}