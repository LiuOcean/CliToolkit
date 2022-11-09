using Newtonsoft.Json;
using Spectre.Console;

namespace CliToolkit.AutoConfig;

[AutoConfig]
[Serializable]
public class SftpAutoConfig : IAutoConfig
{
    [JsonProperty]
    public string ip { get; private set; }

    [JsonProperty]
    public string user_name { get; private set; }

    [JsonProperty]
    public string password { get; private set; }

    public bool CheckPrompt()
    {
        bool changed = false;

        if(string.IsNullOrEmpty(ip))
        {
            ip      = AnsiConsole.Prompt(new TextPrompt<string>("请输入 IP: "));
            changed = true;
        }

        if(string.IsNullOrEmpty(user_name))
        {
            user_name = AnsiConsole.Prompt(new TextPrompt<string>("请输入用户名: "));
            changed   = true;
        }

        if(string.IsNullOrEmpty(password))
        {
            password = AnsiConsole.Prompt(new TextPrompt<string>("请输入密码: ").Secret());
            changed  = true;
        }

        return changed;
    }
}