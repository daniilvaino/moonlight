using System.Text.Json;

namespace Moonlight.Tests;

/// <summary>Locates tests/vectors and loads the corpus files. Linked into every test project.</summary>
public static class Corpus
{
    public static string Dir { get; } = Path.Combine(RepoRoot(), "tests", "vectors");

    public static string File(params string[] parts) => Path.Combine([Dir, .. parts]);

    public static bool Has(params string[] parts) => System.IO.File.Exists(File(parts));

    // Trailing commas allowed: block_202612_transactions.txt has one, and it is a
    // vector file we copy verbatim rather than reformat.
    public static JsonDocument Json(params string[] parts)
        => JsonDocument.Parse(System.IO.File.ReadAllText(File(parts)),
            new JsonDocumentOptions { AllowTrailingCommas = true });

    private static string RepoRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null && !System.IO.File.Exists(Path.Combine(dir.FullName, "global.json")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("repo root not found above " + AppContext.BaseDirectory);
    }
}
