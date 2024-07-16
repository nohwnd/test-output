// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


namespace Microsoft.Build.CommandLine.UnitTests;

public class NodeStatus_Transition_Tests
{
    [Fact]
    public async Task NodeTargetChanges()
    {
        var rendered = Animate(
            [
                new("Namespace.Project", "TargetFramework", "Build", new MockStopwatch())
            ],
            [
               new("Namespace.Project", "TargetFramework", "Testing", new MockStopwatch())
            ]);

        await VerifyReplay(rendered);
    }

    [Fact]
    public async Task NodeTargetUpdatesTime()
    {
        // This test look like there is no change between the frames, but we ask the stopwatch for time they will increase the number.
        // We need this because animations check that NodeStatus reference is the same.
        // And we cannot use MockStopwatch because we don't know when to call Tick on them, and if we do it right away, the time will update in "both" nodes.
        NodeStatus node = new("Namespace.Project", "TargetFramework", "Build", new TickingStopwatch());
        var rendered = Animate(
            [
                node,
            ],
            [
               node,
            ]);

        await VerifyReplay(rendered);
    }

    [Fact]
    public async Task NodeTargetChangesToColoredTarget()
    {
        var rendered = Animate(
            [
                new("Namespace.Project", "TargetFramework", "Testing", new MockStopwatch())
            ],
            [
               new("Namespace.Project", "TargetFramework", TerminalColor.Red, "failed", "MyTestName1", new MockStopwatch())
            ]);

        await VerifyReplay(rendered);
    }

    [Fact]
    public async Task NodeWithColoredTargetUpdatesTime()
    {
        // This test look like there is no change between the frames, but we ask the stopwatch for time they will increase the number.
        // We need this because animations check that NodeStatus reference is the same.
        // And we cannot use MockStopwatch because we don't know when to call Tick on them, and if we do it right away, the time will update in "both" nodes.
        NodeStatus node = new("Namespace.Project", "TargetFramework", TerminalColor.Green, "passed", "MyTestName1", new TickingStopwatch());
        var rendered = Animate(
            [
                node,
            ],
            [
               node
            ]);

        await VerifyReplay(rendered);
    }

    /// <summary>
    /// Chains and renders node status updates and outputs replay able string of all the transitions.
    /// </summary>
    /// <param name="workerProgress">Takes array of arrays. The inner array is collection of nodes that are currently running. The outer array is how they update over time.</param>
    /// <returns></returns>
    private string Animate(params TestWorkerProgress[][] workerProgress)
    {
        var width = 80;
        var height = 1;

        NodesFrame previousFrame = new(Array.Empty<TestWorkerProgress>(), 0, 0);
        StringBuilder result = new StringBuilder();
        foreach (var nodeStatuses in workerProgress)
        {
            NodesFrame currentFrame = new NodesFrame(nodeStatuses, width, height);
            result.Append(currentFrame.Render(previousFrame));
            previousFrame = currentFrame;
        }

        return result.ToString();
    }

    private async Task VerifyReplay(string rendered)
    {
        try
        {
            await Verify(rendered);
        }
        catch (Exception ex)
        {
            if (ex.GetType().Name != "VerifyException")
            {
                throw;
            }

            if (!ex.Message.StartsWith("Directory:"))
            {
                throw;
            }

            string? directory = null;
            string? received = null;
            string? verified = null;
            foreach (var line in ex.Message.Split('\n'))
            {
                var trimmed = line.TrimStart(' ', '-');
                Extract(trimmed, "Directory", ref directory);
                Extract(trimmed, "Received", ref received);
                Extract(trimmed, "Verified", ref verified);
            }

            if (directory == null || received == null || verified == null)
            {
                throw;
            }

            var pipeline = $$""" | % { "`n`n" } { $_ -split "(?=`e)" | % { Write-Host -NoNewline $_; Start-Sleep 0.5 }; Write-Host }""";
            throw new Exception($$"""
                {{ex.Message.TrimEnd('\n')}}

                Received replay:
                    Get-Content {{Path.Combine(directory, received)}} {{pipeline}}

                Verified replay:
                    Get-Content {{Path.Combine(directory, verified)}} {{pipeline}}
                """);
        }

        void Extract(string line, string prefix, ref string? output)
        {
            if (line.StartsWith($"{prefix}: "))
            {
                output = line.Substring(prefix.Length + 2);
            }
        }
    }
}
