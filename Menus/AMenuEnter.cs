using Spectre.Console;

namespace CliToolkit.Menus;

/// <summary>
/// 当菜单进入到指定路径时的基类
/// </summary>
public abstract class AMenuEnter
{
    /// <summary>
    /// 哪一级菜单
    /// </summary>
    public string path { get; private set; }

    /// <summary>
    /// 标记当前 Enter 是否仅执行一次
    /// </summary>
    public bool execute_once { get; private set; }

    public void Init(MenuEnterAttribute attr)
    {
        path         = attr.path;
        execute_once = attr.execute_once;
    }

    public async Task WhenEnter(CancellationToken token)
    {
        try
        {
            await _WhenEnter(token);
        }
        catch(Exception e)
        {
            AnsiConsole.WriteException(e);
        }
    }

    protected abstract Task _WhenEnter(CancellationToken token);
}