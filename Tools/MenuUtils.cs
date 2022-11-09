using System.Reflection;
using CliToolkit.Commands;
using CliToolkit.Tools;
using Spectre.Console;

namespace CliToolkit.Menus;

public static class MenuUtils
{
    private static readonly Dictionary<string, AMenuExecute> _ALL_MENUS = new();

    private static readonly Dictionary<string, List<AMenuEnter>> _ALL_MENU_ENTERS = new();

    private static readonly MenuSelection _SELECTION = new("/");

    private static readonly List<string> _SELECTED = new();

    private const int _INDEX_WIDTH = 5;
    private const int _MENU_WIDTH  = 40;

    static MenuUtils()
    {
        var types = typeof(MenuUtils).Assembly.GetTypes();

        foreach(var type in types)
        {
            var attrs = type.GetCustomAttributes();

            foreach(var attr in attrs)
            {
                switch(attr)
                {
                    case MenuAttribute menu_attr:
                        if(string.IsNullOrEmpty(menu_attr.path))
                        {
                            continue;
                        }

                        if(Activator.CreateInstance(type) is not AMenuExecute menu)
                        {
                            continue;
                        }

                        menu.Init(menu_attr);

                        _ALL_MENUS.TryAdd(menu_attr.path, menu);
                        break;
                    case MenuEnterAttribute menu_enter_attr:
                        if(string.IsNullOrEmpty(menu_enter_attr.path))
                        {
                            continue;
                        }

                        if(Activator.CreateInstance(type) is not AMenuEnter enter)
                        {
                            continue;
                        }

                        enter.Init(menu_enter_attr);

                        _ALL_MENU_ENTERS.TryGetValue(menu_enter_attr.path, out var enter_list);

                        if(enter_list is null)
                        {
                            enter_list = new List<AMenuEnter>();
                            _ALL_MENU_ENTERS.Add(menu_enter_attr.path, enter_list);
                        }

                        enter_list.Add(enter);
                        break;
                }
            }
        }

        var list = _ALL_MENUS.Values.ToList();
        list.Sort(
            (x, y) =>
            {
                var compare = x.order.CompareTo(y.order);

                return compare != 0 ? compare : x.path.CompareTo(y.path);
            }
        );

        foreach(var menu in list)
        {
            _SELECTION.Add(menu.path);
        }
    }

    /// <summary>
    /// 展示交互菜单
    /// </summary>
    /// <param name="token"></param>
    /// <param name="path"></param>
    public static async Task ShowMenu(CancellationToken token, string path = "")
    {
        while(true)
        {
            if(token.IsCancellationRequested)
            {
                return;
            }

            if(_ALL_MENU_ENTERS.TryGetValue(path, out var enter_list))
            {
                foreach(var enter in enter_list)
                {
                    await enter.WhenEnter(token);

                    if(enter.execute_once)
                    {
                        _ALL_MENU_ENTERS.Remove(path);
                    }
                }
            }

            if(string.IsNullOrEmpty(path) || !_SELECTION.IsEnd(path))
            {
                var result = _ShowMenuTable(path);
                path = _GenCachedPath();

                if(result)
                {
                    continue;
                }

                return;
            }

            await MenuExecute(token, path);
            _SELECTED.RemoveAt(_SELECTED.Count - 1);

            path = _GenCachedPath();
        }
    }

    /// <summary>
    /// 指定指定路径菜单的具体函数
    /// </summary>
    /// <param name="token"></param>
    /// <param name="path"></param>
    /// <param name="no_ci"></param>
    /// <returns></returns>
    public static Task MenuExecute(CancellationToken token, string path, bool no_ci = false)
    {
        if(string.IsNullOrEmpty(path))
        {
            return Task.CompletedTask;
        }

        _ALL_MENUS.TryGetValue(path, out var menu);

        if(menu is null)
        {
            return Task.CompletedTask;
        }

        if(no_ci && menu.no_ci)
        {
            return Task.CompletedTask;
        }

        return menu.Execute(token);
    }

    /// <summary>
    /// 当前路径菜单是否存在
    /// </summary>
    /// <param name="path"></param>
    /// <param name="no_ci"></param>
    /// <returns></returns>
    public static bool IsMenuExist(string? path, bool no_ci = false)
    {
        if(string.IsNullOrEmpty(path))
        {
            return false;
        }

        _ALL_MENUS.TryGetValue(path, out var menu);

        if(menu is null)
        {
            return false;
        }

        return!no_ci || !menu.no_ci;
    }

    /// <summary>
    /// 获取所有菜单路径
    /// </summary>
    /// <param name="no_ci"></param>
    /// <returns></returns>
    public static List<string> GetAllMenuPath(bool no_ci = false)
    {
        var list = new List<string>();

        foreach(var (key, menu) in _ALL_MENUS)
        {
            if(no_ci && menu.no_ci)
            {
                continue;
            }

            list.Add(key);
        }

        return list;
    }

    /// <summary>
    /// 绘制当前层级菜单
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    private static bool _ShowMenuTable(string path)
    {
        var selections = _SELECTION.GetSelection(path);

        var path_display = string.IsNullOrEmpty(path) ? "/" : path;

        var table = new Table
        {
            Title = new TableTitle(
                $"当前用户: [{CustomColor.Pink.ToHex()}]{Settings.Current.user_name}[/] 当前路径: [{CustomColor.Pink.ToHex()}]{path_display}[/]"
            )
        };

        table.AddColumn(new TableColumn("序号").Centered().Width(_INDEX_WIDTH));
        table.AddColumn(new TableColumn("菜单").Width(_MENU_WIDTH));

        for(int i = 0; i < selections.Count; i++)
        {
            var color = i % 2 == 0 ? CustomColor.Blue : CustomColor.Pink;

            table.AddRow($"{(i + 1).ToString().WithColor(color)}", selections[i].WithColor(color));
        }

        AnsiConsole.Write(new Rule());
        AnsiConsole.Write(table);
        AnsiConsole.Write(new Rule());

        var index = AnsiConsole.Prompt(new TextPrompt<int>("请输入序号选单:"));

        index -= 1;

        if(index >= selections.Count || index < 0)
        {
            return true;
        }

        if(index == 0)
        {
            if(_SELECTED.Count <= 0)
            {
                return!AnsiConsole.Confirm("是否退出?");
            }

            _SELECTED.RemoveAt(_SELECTED.Count - 1);
        }
        else
        {
            _SELECTED.Add(selections[index]);
        }


        return true;
    }

    /// <summary>
    /// 根据当前缓存的菜单层级, 生成菜单路径
    /// </summary>
    /// <returns></returns>
    private static string _GenCachedPath()
    {
        string next_path = string.Empty;

        foreach(var menu in _SELECTED)
        {
            next_path = $"{next_path}/{menu}";
        }

        next_path = next_path.TrimStart('/');

        return next_path;
    }
}