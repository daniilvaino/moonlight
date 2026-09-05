namespace Moonlight.Gui.Tests;

/// <summary>
/// Locates the checked-out repository from the test binary, so tests can reach the committed
/// baselines and write their artifacts next to them.
/// </summary>
public static class RepositoryLayout
{
    /// <summary>The repository root.</summary>
    public static string Root { get; } = FindRoot();

    private static string FindRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Directory.Build.props"))
                && Directory.Exists(Path.Combine(directory.FullName, "tests", "Gui.Tests")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the Moonlight repository root.");
    }
}
