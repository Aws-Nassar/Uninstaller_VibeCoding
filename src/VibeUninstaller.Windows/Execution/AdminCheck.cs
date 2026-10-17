using System.Security.Principal;

namespace VibeUninstaller.Windows.Execution;

public static class AdminCheck
{
    public static bool IsElevated { get; } = Detect();

    private static bool Detect()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch (Exception)
        {
            return false;
        }
    }
}
