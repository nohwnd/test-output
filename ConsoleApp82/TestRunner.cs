// See https://aka.ms/new-console-template for more information

using System.Threading.Channels;

internal class TestRunner
{
    private readonly Exception _error;

    public TestRunner()
    {
        try
        {
            throw new InvalidOperationException("test failure");
        }
        catch (Exception ex)
        {
            _error = ex;
        }
    }

    internal async Task RunAsync(ChannelWriter<ITestingEvent> writer)
    {
        IEnumerable<ITestingEvent[]> events = [
                [ new TestRunStarting { Assembly = "a.dll", Tests = 1000 }, ],
                [ new TestRunUpdate { Assembly = "a.dll", Tests = 1000, Passed = 120, Failed = 300 }, ],
                [ new TestResultUpdate { Error = null, Outcome = 1, TestName = "MyTests.MyAmazingTest" }, ],
                [ new TestResultUpdate { Error = _error, Outcome = 1, TestName = "MyTests.MyAmazingTest" }, ],
                [ new TestRunFinished { Assembly = "a.dll", Tests = 1000 }, ],
                [ new TestRunFinished { Assembly = "a.dll", Tests = 1000 }, ],
        ];

        foreach (var batch in events)
        {
            foreach (var e in batch)
            {
                if (e is Wait wait)
                {
                    await Task.Delay(wait.Duration);
                }
                else
                {
                    await writer.WriteAsync(e);
                    await Task.Delay(1000);
                }
            }

        }
    }
}