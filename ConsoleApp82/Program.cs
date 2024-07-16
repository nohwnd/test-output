// See https://aka.ms/new-console-template for more information

using Spectre.Console;
using System.Threading.Channels;

public class Program
{


    public static async Task<int> Main(string[] args)
    {
        Channel<ITestingEvent> channel = Channel.CreateUnbounded<ITestingEvent>();

        var loop = Task.Run(async () =>
        {
            var reader = channel.Reader;
            var consoleLogger = new ConsoleLogger();
            await consoleLogger.ConsumeAsync(reader);
        });


        var writer = channel.Writer;
        await new TestRunner().RunAsync(writer);
        writer.Complete();

        await loop;
        return 0;
    }
}

internal class TestRunStarting : ITestingEvent
{
    public string Assembly { get; internal set; }
    public int Tests { get; internal set; }
}

internal class TestRunUpdate : ITestingEvent
{
    public string Assembly { get; set; }
    public int Passed { get; set; }
    public int Failed { get; internal set; }
    public int Tests { get; internal set; }
}

internal class Wait : ITestingEvent
{
    public Wait(int milliseconds)
    {
        Duration = TimeSpan.FromMilliseconds(milliseconds);
    }

    public TimeSpan Duration { get; }
}

internal class TestRunFinished : ITestingEvent
{
    public string Assembly { get; set; }
    public int Tests { get; internal set; }
}

internal class TestResultUpdate : ITestingEvent
{
    public string TestName { get; internal set; }
    public int Outcome { get; set; }
    public Exception? Error { get; set; }
}