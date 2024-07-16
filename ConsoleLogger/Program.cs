using Microsoft.Build.Logging.TerminalLogger;

public static class Program
{
    public static void Main()
    {
        new ConsoleLogger();

        Thread.Sleep(10000);
    }
}

public class ConsoleLogger
{
    private readonly List<TestWorkerProgress[]> _nextFrames;
    private List<TestWorkerProgress> _nextFrame;
    private List<TestWorkerProgress> _previousFrame;
    private readonly ITerminal _terminal;

    private CancellationTokenSource _rendering;
    private CancellationToken _renderingToken;
    private Task Renderer;

    public object Emitter { get; }

    public ConsoleLogger()
    {
        var slowness = 1000;
        var cts = new CancellationTokenSource();
        _rendering = cts;
        _renderingToken = cts.Token;
        Renderer = Task.Run(async () =>
        {
            try
            {
                while (!_renderingToken.IsCancellationRequested)
                {
                    Thread.Sleep(slowness);
                    Render(); // 24 fps
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }

        }, CancellationToken.None /* Don't propagate the cts token here, we want to write cancellation message and then finish the task. */);
        _previousFrame = new List<TestWorkerProgress>(Environment.ProcessorCount);
        _terminal = new Terminal();

        _nextFrames = new List<TestWorkerProgress[]>(200);
        Exception error;
        try
        {
            throw new InvalidOperationException("Oh my this is failing");
        }
        catch (Exception ex)
        {
            error = ex;
        }
        foreach (var i in Enumerable.Range(0, 200))
        {
            var errorChance = 10;
            var workers = Enumerable.Range(1,2).Select( i =>  new TestWorkerProgress
            {
                Tests = 1000,
                Passed = i * 5,
                Failed = i * 2,
                Skipped = i * 1,
                AssemblyName = "MyTests.dll",
                TargetFramework = "net9.0",
                Architecture = "x64",
                TestUpdates = new TestUpdate[]
                {
                    new TestUpdate
                    {
                        Name = $"MyTest_{i}",
                        Outcome = i%errorChance > 1 ? 1 : 0,
                        Error = i%errorChance > 1 ? null : error,
                    }
                }
            }).ToArray();
            _nextFrames.Add(workers);
        }

        Emitter = Task.Run(async () =>
        {
            var frame = 0;
            try
            {
                while (!_renderingToken.IsCancellationRequested)
                {

                    _nextFrame = _nextFrames[frame++].ToList();
                    Thread.Sleep(slowness);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }

        }, CancellationToken.None /* Don't propagate the cts token here, we want to write cancellation message and then finish the task. */);

    }


    public void Render()
    {
        _terminal.BeginUpdate();
        if (_previousFrame != null)
        {
            if (_previousFrame.Count > 0)
            {
                // Move cursor back to 1st line of nodes.
                _terminal.WriteLine($"{AnsiCodes.CSI}{_previousFrame.Count}{AnsiCodes.MoveUpToLineStart}");
            }
        }
        foreach (TestWorkerProgress testWorkerProgress in _nextFrame)
        {
            WriteWorkerProgress(testWorkerProgress);
        }
        _previousFrame = _nextFrame;

        _terminal.EndUpdate();
    }

    private void WriteWorkerProgress(TestWorkerProgress testWorkerProgress)
    {
        var rewriting = true;
        if (testWorkerProgress.TestUpdates.Length != 0) {

            rewriting = false;
            foreach (var testUpdate in testWorkerProgress.TestUpdates)
            {
                RenderTestWorkerProgress(testUpdate);
            }
            _terminal.WriteLine("");
        }

        var buckets = 64;
        var total = testWorkerProgress.Tests;
        var passed = testWorkerProgress.Passed;
        var failed = testWorkerProgress.Failed;
        var skipped = testWorkerProgress.Skipped;
        var remaining = total - passed + failed + skipped;
        var passedBuckets = AsBucket(total, passed, buckets);
        var failedBuckets = AsBucket(total, failed, buckets);
        var skippedBuckets = AsBucket(total, skipped, buckets);
        var remainingBuckets = buckets - (passedBuckets + failedBuckets + skippedBuckets);
        _terminal.Write("[");
        _terminal.Write(AnsiCodes.Colorize(new string('+', passedBuckets), TerminalColor.Green));
        _terminal.Write(AnsiCodes.Colorize(new string('-', failedBuckets), TerminalColor.Red));
        _terminal.Write(AnsiCodes.Colorize(new string('?', skippedBuckets), TerminalColor.Yellow));
        _terminal.Write(AnsiCodes.Colorize(new string(' ', remainingBuckets), TerminalColor.White)); //TODO: gray?
        _terminal.Write("] ");
        _terminal.Write(" ");
        _terminal.Write($"{testWorkerProgress.AssemblyName} ({testWorkerProgress.TargetFramework}|{testWorkerProgress.Architecture})");
    }

    private void RenderTestWorkerProgress(TestUpdate testUpdate)
    {
        _terminal.Write(AnsiCodes.CSI+AnsiCodes.EraseInLine);
        _terminal.Write(AnsiCodes.Colorize(testUpdate.Outcome == 0 ? "failed" : "passed", testUpdate.Outcome == 0 ? TerminalColor.Red : TerminalColor.Green));
        _terminal.Write(" ");
        _terminal.Write(testUpdate.Name);
        _terminal.Write(" ");
        if (testUpdate.Error != null)
        {
            _terminal.WriteLine("");
            _terminal.WriteLine(AnsiCodes.Colorize(testUpdate.Error.ToString(), TerminalColor.Red));
        }
        else
        {
        }
    }

    private int AsBucket(int total, int passed, int buckets)
    {
        return (int)Math.Round((decimal)passed / total * buckets, 0, MidpointRounding.ToZero);
    }
}

internal interface IOutput
{
    public void WriteLine(string output);
}

internal class Output : IOutput
{
    public void WriteLine(string output)
    {
        Console.WriteLine(output);
    }
}

internal class TestWorkerProgress
{
    public int Tests { get; internal set; }
    public int Passed { get; internal set; }
    public int Failed { get; internal set; }
    public int Skipped { get; internal set; }
    public string AssemblyName { get; internal set; }
    public string TargetFramework { get; internal set; }
    public string Architecture { get; internal set; }
    public TestUpdate[] TestUpdates { get; internal set; }
}

internal class TestUpdate
{
   public int Outcome { get; set; }
   public Exception? Error { get; set; }
   public string Name { get; set; }
}