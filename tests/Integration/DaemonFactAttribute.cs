using Xunit;

namespace Moonlight.Integration.Tests;

/// <summary>
/// A test that needs a real node, and says so when there is not one.
///
/// The chain tests used to return early with no daemon configured, which xunit
/// reported as three tests passing. A missing node is not a bug, but three green
/// ticks for work nobody did is worse than a red one: it is the same reading a run
/// against a node gives, and the difference is the entire value of those tests.
///
/// xunit 2 has no way to skip from inside a test — that arrived in v3, which only
/// the interface tests are on — so the decision is made here, before the run.
/// </summary>
public sealed class DaemonFactAttribute : FactAttribute
{
    public DaemonFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("MOONLIGHT_DAEMON") is null)
        {
            Skip = "Set MOONLIGHT_DAEMON to a node's address, for example http://127.0.0.1:18081/.";
        }
    }
}
