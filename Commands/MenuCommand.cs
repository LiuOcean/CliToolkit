using CliToolkit.Menus;
using Spectre.Cli;

namespace CliToolkit.Commands;

/// <summary>
/// 手动执行菜单
/// </summary>
public class MenuCommand : AAsyncCommand
{
    protected override async Task<int> _ExecuteAsync(CommandContext context, Settings settings, CancellationToken token)
    {
        await MenuUtils.ShowMenu(token);

        return 0;
    }
}