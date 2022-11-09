using Newtonsoft.Json;
using Spectre.Console;

namespace CliToolkit.AutoConfig;

[Serializable]
[AutoConfig(false)]
public class SftpIgnoreAutoConfig : IAutoConfig
{
    [JsonProperty("ignore")]
    private string[] _ignore { get; set; }

    private readonly HashSet<string> _ignore_set = new();

    public bool IsIgnore(string file_name) { return _ignore_set.Contains(file_name); }

    public bool CheckPrompt()
    {
        bool changed = false;

        if(_ignore is null || _ignore.Length <= 0)
        {
            var ignore = AnsiConsole.Prompt(
                new TextPrompt<string>("请输入需要忽略的文件类型, 使用英文逗号分割: ").DefaultValue(".DS_Store")
            );

            _ignore = ignore.Split(',');

            changed = true;
        }

        _ignore_set.Clear();

        foreach(var ignore in _ignore)
        {
            _ignore_set.Add(ignore);
        }

        return changed;
    }
}