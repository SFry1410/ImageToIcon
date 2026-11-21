using System.Runtime.Versioning;
using System.Security.AccessControl;
using ImageToIcon.Library.Storage;

namespace ImageToIcon.Library;

/// Location and permissions of the system-wide icon library.
public static class LibraryPaths
{
    /// %ProgramData%\ImageToIcon — readable for every account, writable for administrators only.
    public static string DefaultDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ImageToIcon");

    /// Creates the library directory if missing. The system-wide default directory gets
    /// administrator-only write access; standard users keep read and execute access so Explorer can
    /// load the DLL. A user-chosen directory keeps the normal inherited permissions, so the account
    /// that creates it can still write to it.
    [SupportedOSPlatform("windows")]
    internal static void EnsureDirectory(string dir, IIconLibraryLog log)
    {
        if (Directory.Exists(dir))
            return;

        try
        {
            Directory.CreateDirectory(dir);
            if (IsDefaultDirectory(dir))
                ApplyAcl(dir);
            log.Info($"Created icon library directory '{dir}'.");
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new LibraryAccessDeniedException(dir, ex);
        }
    }

    private static bool IsDefaultDirectory(string dir)
    {
        return string.Equals(
            Path.GetFullPath(dir).TrimEnd(Path.DirectorySeparatorChar),
            Path.GetFullPath(DefaultDirectory).TrimEnd(Path.DirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);
    }

    [SupportedOSPlatform("windows")]
    private static void ApplyAcl(string dir)
    {
        var inherit = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
        var security = new DirectorySecurity();
        // Break inheritance so the rules below are the complete list.
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.AddAccessRule(new FileSystemAccessRule(new System.Security.Principal.SecurityIdentifier(System.Security.Principal.WellKnownSidType.BuiltinAdministratorsSid, null), FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(new System.Security.Principal.SecurityIdentifier(System.Security.Principal.WellKnownSidType.LocalSystemSid, null), FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(new System.Security.Principal.SecurityIdentifier(System.Security.Principal.WellKnownSidType.BuiltinUsersSid, null), FileSystemRights.ReadAndExecute, inherit, PropagationFlags.None, AccessControlType.Allow));
        new DirectoryInfo(dir).SetAccessControl(security);
    }
}
