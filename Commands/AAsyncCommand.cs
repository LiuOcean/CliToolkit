using Spectre.Cli;
using Spectre.Console;

namespace CliToolkit.Commands;

/// <summary>
/// 当前项目中所有可执行的命令基类
/// </summary>
public abstract class AAsyncCommand : AsyncCommand<Settings>
{
    public override Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        AnsiConsole.Write(new FigletText("Cli Toolkit"));

        settings.CheckPrompt();

        CancellationTokenSource cts = new CancellationTokenSource();

        Console.CancelKeyPress += (_, _) => cts.Cancel();

        return _ExecuteAsync(context, settings, cts.Token);
    }

    protected abstract Task<int> _ExecuteAsync(CommandContext context, Settings settings, CancellationToken token);
}