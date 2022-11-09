namespace CliToolkit.Menus;

[AttributeUsage(AttributeTargets.Class)]
public class MenuAttribute : Attribute
{
    /// <summary>
    /// 菜单的路径
    /// </summary>
    public readonly string path;

    /// <summary>
    /// 菜单的显示顺序
    /// </summary>
    public readonly int order;

    /// <summary>
    /// 是否自动确认, 为 true 时 会问用户是否继续
    /// 如果用户选择继续, 会再次执行
    /// </summary>
    public readonly bool auto_confirm;

    /// <summary>
    /// 标记当前菜单是否可以在 ci 中执行
    /// </summary>
    public readonly bool no_ci;

    public MenuAttribute(string path,
                         int    order        = 1,
                         bool   auto_confirm = false,
                         bool   no_ci        = false)
    {
        this.path         = path;
        this.order        = order;
        this.auto_confirm = auto_confirm;
        this.no_ci        = no_ci;
    }
}