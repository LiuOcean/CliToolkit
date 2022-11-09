using System.Text;

namespace CliToolkit.Menus;

public class MenuSelection
{
    public string name { get; }

    private readonly Dictionary<string, MenuSelection> _selections = new();

    public MenuSelection(string name) { this.name = name; }

    public void Add(string path)
    {
        if(string.IsNullOrEmpty(path))
        {
            return;
        }

        var span = path.AsSpan();

        string key = path;

        int index = span.IndexOf("/");

        if(index > 0)
        {
            key = span[..index].ToString();

            index++;
            span = span[index..];
        }
        else
        {
            span = span[..0];
        }

        if(!_selections.TryGetValue(key, out var selection))
        {
            selection = new MenuSelection(key);
            _selections.Add(key, selection);
        }

        if(span.Length > 0)
        {
            selection.Add(span.ToString());
        }
    }

    /// <summary>
    /// 获取当前路径下的所有选项
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    public List<string> GetSelection(string path)
    {
        var dic = _GetLastDic(path);

        List<string> result = new List<string> {dic == _selections ? "退出" : "返回"};

        foreach(var value in dic.Values)
        {
            result.Add(value.name);
        }

        return result;
    }

    /// <summary>
    /// 当前路径是否为 Execute
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    public bool IsEnd(string path) { return _GetLastDic(path).Count <= 0; }

    private Dictionary<string, MenuSelection> _GetLastDic(string path)
    {
        Dictionary<string, MenuSelection> dic = _selections;

        var splits = path.Split("/");

        foreach(var split in splits)
        {
            dic.TryGetValue(split, out var temp);

            if(temp is null)
            {
                break;
            }

            dic = temp._selections;
        }

        return dic;
    }
}