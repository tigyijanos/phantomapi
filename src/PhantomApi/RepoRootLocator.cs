using System.IO;

static class RepoRootLocator
{
    public static string Resolve(params string?[] startingPaths)
    {
        foreach (var startingPath in startingPaths)
        {
            var resolved = TryResolveFrom(startingPath);
            if (resolved is not null)
            {
                return resolved;
            }
        }

        throw new DirectoryNotFoundException("Unable to locate the repository root containing instructions/ and data/.");
    }

    private static string? TryResolveFrom(string? startingPath)
    {
        if (string.IsNullOrWhiteSpace(startingPath))
        {
            return null;
        }

        var currentPath = Path.GetFullPath(startingPath);
        if (File.Exists(currentPath))
        {
            currentPath = Path.GetDirectoryName(currentPath) ?? currentPath;
        }

        var directory = new DirectoryInfo(currentPath);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "instructions")) &&
                Directory.Exists(Path.Combine(directory.FullName, "data")) &&
                File.Exists(Path.Combine(directory.FullName, "README.md")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
