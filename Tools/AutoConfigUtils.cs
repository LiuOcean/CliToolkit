using System.Reflection;
using System.Runtime.CompilerServices;
using CliToolkit.AutoConfig;
using CliToolkit.Commands;
using Newtonsoft.Json;

namespace CliToolkit.Tools;

public static class AutoConfigUtils
{
    public const string ROOT = ".AutoConfig/";

    private static readonly Dictionary<string, Type> _ALL_AUTO_CONFIGS = new();

    private static readonly Dictionary<string, AutoConfigAttribute> _AUTO_CONFIG_ATTRS = new();

    static AutoConfigUtils()
    {
        foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            foreach(var type in assembly.GetTypes())
            {
                if(type.IsAssignableTo(typeof(IAutoConfig)) && !type.IsInterface)
                {
                    _ALL_AUTO_CONFIGS.Add(type.Name, type);
                }

                var attr = type.GetCustomAttribute<AutoConfigAttribute>();

                if(attr is not null)
                {
                    _AUTO_CONFIG_ATTRS[type.Name] = attr;
                }
            }
        }
    }

    /// <summary>
    /// 获取一个自动配置, 检查有效性并保存
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static T GetAutoConfig<T>() where T : IAutoConfig, new() { return(T) GetAutoConfig(typeof(T)); }

    /// <summary>
    /// 获取一个自动配置, 检查有效性并保存
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public static IAutoConfig? GetAutoConfig(string name)
    {
        _ALL_AUTO_CONFIGS.TryGetValue(name, out var type);

        return type is null ? null : GetAutoConfig(type);
    }

    /// <summary>
    /// 获取一个自动配置, 检查有效性并保存
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static IAutoConfig GetAutoConfig(Type type)
    {
        if(!Directory.Exists(ROOT))
        {
            Directory.CreateDirectory(ROOT);
        }

        var file_name = _GetFileName(type);

        string json = string.Empty;

        if(File.Exists(file_name))
        {
            json = File.ReadAllText(file_name);
        }

        IAutoConfig? result;

        if(string.IsNullOrEmpty(json))
        {
            result = Activator.CreateInstance(type) as IAutoConfig;
        }
        else
        {
            result = JsonConvert.DeserializeObject(json, type) as IAutoConfig;
        }

        if(result is null)
        {
            throw new ArgumentException();
        }

        if(result.CheckPrompt())
        {
            SaveAutoConfig(result);
        }

        return result;
    }

    /// <summary>
    /// 保存当前配置
    /// </summary>
    /// <param name="config"></param>
    public static void SaveAutoConfig(IAutoConfig config)
    {
        var file_name = _GetFileName(config.GetType());

        if(!File.Exists(file_name))
        {
            File.Create(file_name).Dispose();
        }

        File.WriteAllText(file_name, JsonConvert.SerializeObject(config, Formatting.Indented));
    }

    /// <summary>
    /// 获取所有已经缓存的配置路径
    /// </summary>
    /// <returns></returns>
    public static string[] GetAllConfigName()
    {
        if(!Directory.Exists(ROOT))
        {
            return Array.Empty<string>();
        }

        var result = Directory.GetFiles(ROOT, "*.json");

        return result;
    }

    /// <summary>
    /// 删除指定的配置文件
    /// </summary>
    /// <param name="files"></param>
    public static void DeleteFiles(List<string>? files)
    {
        if(files is null || files.Count <= 0)
        {
            return;
        }

        foreach(var file in files.Where(File.Exists))
        {
            File.Delete(file);
        }
    }

    /// <summary>
    /// 读取指定配置
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    public static string ReadFile(string path) { return!File.Exists(path) ? string.Empty : File.ReadAllText(path); }

    /// <summary>
    /// 获取尚未创建的配置文件
    /// </summary>
    /// <returns></returns>
    public static List<string> GetUnCreateConfigs()
    {
        List<string> result = new List<string>();

        foreach(var type in _ALL_AUTO_CONFIGS.Values)
        {
            _AUTO_CONFIG_ATTRS.TryGetValue(type.Name, out var attr);

            if(attr is not null && !attr.can_menu_touch)
            {
                continue;
            }

            var file_path = _GetFileName(type);

            if(File.Exists(file_path))
            {
                continue;
            }

            result.Add(type.Name);
        }

        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string _GetFileName(Type type)
    {
        _AUTO_CONFIG_ATTRS.TryGetValue(type.Name, out var attr);

        bool use_user_name = attr?.use_user_name ?? true;

        return Path.Combine(
            ROOT,
            use_user_name ? $"{type.Name}_{Settings.Current.user_name}.json" : $"{type.Name}.json"
        );
    }
}