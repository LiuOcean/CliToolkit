namespace CliToolkit.AutoConfig;

[AttributeUsage(AttributeTargets.Class)]
public class AutoConfigAttribute : Attribute
{
    /// <summary>
    /// 当前配置文件是否追加用户名
    /// </summary>
    public readonly bool use_user_name;

    /// <summary>
    /// 当前配置文件是否可以通过菜单手动创建
    /// </summary>
    public readonly bool can_menu_touch;

    public AutoConfigAttribute(bool use_user_name = true, bool can_menu_touch = true)
    {
        this.use_user_name  = use_user_name;
        this.can_menu_touch = can_menu_touch;
    }
}