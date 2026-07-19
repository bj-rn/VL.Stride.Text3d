// Tests for the poll-based async helper behind the (Async) mesh nodes.

using System.Diagnostics;
using NUnit.Framework;
using VL.Stride.Text3d.Core;

namespace VL.Stride.Text3d.Tests;

[TestFixture]
public class BackgroundComputationTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private static bool PollUntilAdopted(BackgroundComputation<string> computation, int hash, out string? result)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < Timeout)
        {
            if (computation.Poll(hash, out result, out _, out _))
                return true;
            Thread.Sleep(5);
        }
        result = null;
        return false;
    }

    [Test]
    public void AdoptsResultOnceAndCaches()
    {
        var computation = new BackgroundComputation<string>();

        Assert.That(computation.Poll(1, out _, out bool needsStart, out bool inProgress), Is.False);
        Assert.That(needsStart, Is.True);
        Assert.That(inProgress, Is.False);

        computation.Start(1, () => "one");
        Assert.That(PollUntilAdopted(computation, 1, out var result), Is.True, "timed out");
        Assert.That(result, Is.EqualTo("one"));

        // Unchanged input: cached, no restart, no re-adoption
        Assert.That(computation.Poll(1, out result, out needsStart, out inProgress), Is.False);
        Assert.That(result, Is.EqualTo("one"));
        Assert.That(needsStart, Is.False);
        Assert.That(inProgress, Is.False);
    }

    [Test]
    public void InputChangeTriggersRestartAndKeepsLastResult()
    {
        var computation = new BackgroundComputation<string>();
        computation.Poll(1, out _, out _, out _);
        computation.Start(1, () => "one");
        PollUntilAdopted(computation, 1, out _);

        // New input: last result stays available while the new computation runs
        Assert.That(computation.Poll(2, out var result, out bool needsStart, out _), Is.False);
        Assert.That(result, Is.EqualTo("one"));
        Assert.That(needsStart, Is.True);

        computation.Start(2, () => "two");
        Assert.That(PollUntilAdopted(computation, 2, out result), Is.True, "timed out");
        Assert.That(result, Is.EqualTo("two"));
    }

    [Test]
    public void FaultSticksUntilInputChanges()
    {
        var computation = new BackgroundComputation<string>();
        computation.Poll(1, out _, out _, out _);
        computation.Start(1, () => throw new InvalidOperationException("boom"));

        // Wait for the fault to surface
        var sw = Stopwatch.StartNew();
        Exception? thrown = null;
        while (sw.Elapsed < Timeout && thrown == null)
        {
            try
            {
                computation.Poll(1, out _, out _, out _);
                Thread.Sleep(5);
            }
            catch (Exception e)
            {
                thrown = e;
            }
        }
        Assert.That(thrown, Is.InstanceOf<InvalidOperationException>());

        // Same input keeps throwing without restarting the work
        Assert.That(() => computation.Poll(1, out _, out _, out _), Throws.InvalidOperationException);

        // Changed input clears the fault and requests a restart
        Assert.That(() => computation.Poll(2, out _, out bool needsStart, out _), Throws.Nothing);
        computation.Poll(2, out _, out bool restart, out _);
        Assert.That(restart, Is.True);
    }
}
