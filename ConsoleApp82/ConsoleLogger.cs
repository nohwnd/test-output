// See https://aka.ms/new-console-template for more information


using Spectre.Console;
using System.Threading.Channels;

internal class ConsoleLogger
{

    public ConsoleLogger()
    {

    }

    internal async Task ConsumeAsync(ChannelReader<ITestingEvent> reader)
    {

        
        Dictionary<string, ProgressTask> runningAssemblies = new();
        Progress progress = AnsiConsole.Progress().AutoRefresh(true);
        await progress.StartAsync(async c =>
        {
            //.AutoClear(true);
            //.Columns(
            //[
            //    new TaskDescriptionColumn(),    // Task description
            //    new ProgressBarColumn(),        // Progress bar
            //    new PercentageColumn(),         // Percentage
            //    new RemainingTimeColumn(),      // Remaining time
            //    new SpinnerColumn(),            // Spinner
            //]);

            await foreach (var item in reader.ReadAllAsync())
            {
                switch (item)
                {
                    case TestRunStarting testRunStarting:
                        {
                            if (runningAssemblies.TryGetValue(testRunStarting.Assembly, out _))
                            {
                                throw new InvalidOperationException($"Assembly {testRunStarting.Assembly} is already running.");
                            }
                            else
                            {
                                runningAssemblies.Add(testRunStarting.Assembly, c.AddTask(testRunStarting.Assembly, autoStart: true));
                            }
                        }
                        break;
                    case TestRunUpdate testRunUpdate:
                        {
                            if (runningAssemblies.TryGetValue(testRunUpdate.Assembly, out ProgressTask? progress))
                            {
                                // progress.IsIndeterminate = false;
                                // We calculate it ourselves because the base value could increase as well when we expand tests
                                progress.Value = 10; // testRunUpdate.Tests / (testRunUpdate.Failed + testRunUpdate.Passed);
                            }
                            else
                            {
                                throw new InvalidOperationException($"Assembly {testRunUpdate.Assembly} is not running.");
                            }
                        }
                        break;

                    case TestResultUpdate testResultUpdate:
                        {
                            if (testResultUpdate.Outcome == 0)
                            {
                                // test failed
                                AnsiConsole.Write(new Markup($"[red]failed[/] {testResultUpdate.TestName}"));
                                AnsiConsole.WriteException(testResultUpdate.Error);
                            }
                            else
                            {
                                AnsiConsole.Write(new Markup($"[green]passed[/] {testResultUpdate.TestName}"));
                            }
                        }
                        break;
                }
            }

            return Task.CompletedTask;
        });
    }
}
