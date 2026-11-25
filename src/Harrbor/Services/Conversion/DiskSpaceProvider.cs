namespace Harrbor.Services.Conversion;

/// <summary>Free space on the filesystem holding a path.</summary>
/// <param name="VolumeId">Identifies the filesystem (its mount point), so two paths can be compared.</param>
/// <param name="AvailableBytes">Bytes available to this process.</param>
public record DiskSpace(string VolumeId, long AvailableBytes);

public interface IDiskSpaceProvider
{
    /// <summary>Free space for the filesystem that holds <paramref name="path"/>, or null if it can't be determined.</summary>
    DiskSpace? GetDiskSpace(string path);
}

public class DiskSpaceProvider : IDiskSpaceProvider
{
    public DiskSpace? GetDiskSpace(string path)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);

            // The longest mount point that contains the path is its filesystem.
            var drive = DriveInfo.GetDrives()
                .Where(d => IsSameOrUnder(fullPath, d.RootDirectory.FullName))
                .MaxBy(d => d.RootDirectory.FullName.Length);
            if (drive == null)
                return null;

            return new DiskSpace(drive.RootDirectory.FullName, drive.AvailableFreeSpace);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    private static bool IsSameOrUnder(string path, string root)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var trimmedRoot = root.TrimEnd(Path.DirectorySeparatorChar);
        return path.Equals(trimmedRoot, comparison)
            || path.StartsWith(trimmedRoot + Path.DirectorySeparatorChar, comparison)
            || trimmedRoot.Length == 0; // "/" trims to ""
    }
}
