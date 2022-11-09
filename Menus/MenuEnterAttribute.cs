namespace CliToolkit.Menus;

[AttributeUsage(AttributeTargets.Class)]
public class MenuEnterAttribute : Attribute
{
    public readonly string path;

    public readonly bool execute_once;

    public MenuEnterAttribute(string path, bool execute_once = true)
    {
        this.path         = path;
        this.execute_once = execute_once;
    }
}