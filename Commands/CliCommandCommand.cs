using CliToolkit.Menus;
using CliToolkit.Tools;
using Spectre.Cli;
using Spectre.Console;

namespace CliToolkit.Commands;

/// <summary>
/// cli 自动化执行菜单
/// </summary>
public class CliCommand : AAsyncCommand
{
    protected override async Task<int> _ExecuteAsync(CommandContext context, Settings settings, CancellationToken token)
    {
        var paths = _AskPath(settings.path);

        foreach(var path in paths)
        {
            await MenuUtils.MenuExecute(token, path, true);
        }

        return 0;
    }

    private List<string> _AskPath(string? path)
    {
        if(!string.IsNullOrEmpty(path))
        {
            var splits = path.Split(",");

            var result = new List<string>();

            foreach(var split in splits)
            {
                if(MenuUtils.IsMenuExist(split, true))
                {
                    result.Add(split);
                }
            }

            if(result.Count > 0)
            {
                return result;
            }
        }

        var all_menus = MenuUtils.GetAllMenuPath(true);

        return AnsiConsole.Prompt(
            new MultiSelectionPrompt<string>().Title("请选择要执行的菜单:").
                                               InstructionsText(
                                                   $"(按 [{CustomColor.Blue.ToHex()}]<空格>[/] 勾选, [{CustomColor.Blue.ToHex()}]<确定>[/] 开始执行)"
                                               ).
                                               AddChoices(all_menus)
        );
    }
}