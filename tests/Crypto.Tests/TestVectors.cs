namespace Moonlight.Crypto.Tests;

/// <summary>One line of monero tests.txt: operation name plus its arguments.</summary>
public readonly record struct Vector(string Op, string[] Args)
{
    public string this[int i] => Args[i];

    public byte[] Bytes(int i) => Convert.FromHexString(Args[i]);

    public bool Flag(int i) => Args[i] switch
    {
        "true" => true,
        "false" => false,
        var x => throw new FormatException($"expected true/false, got '{x}'"),
    };
}

public static class TestVectors
{
    public static string Path { get; } = System.IO.Path.Combine(RepoRoot(), "tests", "vectors", "tests.txt");

    public static bool Available => File.Exists(Path);

    public static IReadOnlyList<Vector> All()
    {
        List<Vector> all = [];

        foreach (string line in File.ReadLines(Path))
        {
            string[] parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 1)
            {
                all.Add(new Vector(parts[0], parts[1..]));
            }
        }

        return all;
    }

    public static IEnumerable<Vector> Read(string op)
    {
        foreach (string line in File.ReadLines(Path))
        {
            string[] parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 1 && parts[0] == op)
            {
                yield return new Vector(parts[0], parts[1..]);
            }
        }
    }

    public static IEnumerable<object[]> Cases(string op) =>
        Available ? Read(op).Select(v => new object[] { v }) : [];

    private static string RepoRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(System.IO.Path.Combine(dir.FullName, "global.json")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("repo root not found above " + AppContext.BaseDirectory);
    }
}
