using CliToolkit.Commands;
using Spectre.Cli;
using Spectre.Console;

TaskScheduler.UnobservedTaskException += (_, event_args) => { AnsiConsole.WriteException(event_args.Exception); };

var app = new CommandApp();

app.SetDefaultCommand<MenuCommand>();

app.Configure(
    config =>
    {
        config.AddCommand<MenuCommand>("Menu");
        config.AddCommand<CliCommand>("Cli");
    }
);

return await app.RunAsync(args);