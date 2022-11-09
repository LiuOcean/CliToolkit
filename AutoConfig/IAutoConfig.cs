namespace CliToolkit.AutoConfig;

/// <summary>
/// 标记当前类为自动保存
/// </summary>
public interface IAutoConfig
{
    public bool CheckPrompt();
}