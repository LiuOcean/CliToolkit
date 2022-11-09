using CliToolkit.Tools;
using Spectre.Console;

namespace CliToolkit.Menus;

/// <summary>
/// 菜单具体执行基类
/// </summary>
public abstract class AMenuExecute
{
    public string path { get; private set; }

    public int order { get; private set; }

    public bool auto_confirm { get; private set; }

    public bool no_ci { get; private set; }

    public void Init(MenuAttribute attr)
    {
        path         = attr.path;
        order        = attr.order;
        auto_confirm = attr.auto_confirm;
        no_ci        = attr.no_ci;
    }

    public async Task Execute(CancellationToken token)
    {
        AnsiConsole.Write(new Rule($"{path.WithColor(CustomColor.Blue)}, 开始执行...") {Alignment = Justify.Left});
        AnsiConsole.WriteLine();

        while(true)
        {
            try
            {
                await _Execute(token);

                if(auto_confirm && AnsiConsole.Confirm("是否继续?"))
                {
                    continue;
                }

                break;
            }
            catch(Exception e)
            {
                AnsiConsole.WriteException(e);
                break;
            }
        }

        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule($"{path.WithColor(CustomColor.Blue)}, 执行结束...") {Alignment = Justify.Left});
    }

    protected abstract Task _Execute(CancellationToken token);
}