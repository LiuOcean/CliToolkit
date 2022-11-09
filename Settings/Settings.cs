using System.ComponentModel;
using CliToolkit.AutoConfig;
using CliToolkit.Tools;
using Spectre.Cli;
using Spectre.Console;

namespace CliToolkit.Commands;

public class Settings : CommandSettings
{
    public static Settings Current { get; private set; }

    [CommandOption("-u|--uname")]
    [Description("用于区分多个本地配置")]
    public string user_name { get; private set; }

    [CommandOption("-p|--path")]
    [Description("菜单路径, 使用英文逗号分割多个需要执行的菜单")]
    public string? path { get; private set; }

    [CommandOption("--yooasset_local_dir")]
    [Description("YooAsset 本地上传路径")]
    public string yooasset_local_dir { get; private set; }

    [CommandOption("--yooasset_remove_dir")]
    [Description("YooAsset 远端上传路径")]
    public string yooaset_remote_dir { get; private set; }

    public void CheckPrompt()
    {
        Current = this;

        var config = AutoConfigUtils.GetAutoConfig<UserAutoConfig>();

        // 这里分两种情况 cli 可能会传入参数
        // 当用户名为空时, 重新问用户要一个有效值
        // 如果上次登陆过, 则默认账号为上次登陆的账号
        if(string.IsNullOrEmpty(user_name))
        {
            var display = string.IsNullOrEmpty(config.last_login)
                ? new TextPrompt<string>("请输入用户名, 默认为").DefaultValue("01")
                : new TextPrompt<string>("上次登陆用户名为").DefaultValue(config.last_login);

            user_name = AnsiConsole.Prompt(display);
        }

        // 最后无论 user_name 来源是哪里
        // 都尝试保存一下最后登录的用户名
        config.SetLastLogin(user_name);
    }
}