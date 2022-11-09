using CliToolkit.Menus;
using CliToolkit.Tools;
using Spectre.Console;

namespace CliToolkit.CustomMenu;

[Menu("AutoConfig/rm", int.MaxValue, no_ci: true)]
public class AutoConfigRm : AMenuExecute
{
    protected override Task _Execute(CancellationToken token)
    {
        var all_files = AutoConfigUtils.GetAllConfigName();

        if(all_files.Length <= 0)
        {
            AnsiConsole.WriteLine("暂无缓存配置文件...");
            return Task.CompletedTask;
        }

        var files = AnsiConsole.Prompt(
            new MultiSelectionPrompt<string>().Title("请选择要删除的配置: ").
                                               InstructionsText(
                                                   $"(按 [{CustomColor.Blue.ToHex()}]<空格>[/] 勾选, [{CustomColor.Blue.ToHex()}]<确定>[/] 开始执行)"
                                               ).
                                               AddChoices(all_files).
                                               UseConverter(name => name.Replace(AutoConfigUtils.ROOT, ""))
        );

        AutoConfigUtils.DeleteFiles(files);

        foreach(var file in files)
        {
            AnsiConsole.WriteLine($"已移除 {file.Replace(AutoConfigUtils.ROOT, "")}...");
        }

        return Task.CompletedTask;
    }
}

[Menu("AutoConfig/cat", int.MaxValue, no_ci: true)]
public class AutoConfigCat : AMenuExecute
{
    protected override Task _Execute(CancellationToken token)
    {
        var all_files = AutoConfigUtils.GetAllConfigName();

        if(all_files.Length <= 0)
        {
            AnsiConsole.WriteLine("暂无缓存配置文件...");
            return Task.CompletedTask;
        }

        var file = AnsiConsole.Prompt(
            new SelectionPrompt<string>().Title("请选择要查看的配置: ").
                                          AddChoices(all_files).
                                          UseConverter(name => name.Replace(AutoConfigUtils.ROOT, ""))
        );

        AnsiConsole.Write(new Panel(Markup.Escape(AutoConfigUtils.ReadFile(file))) {Header = new PanelHeader(file)});
        return Task.CompletedTask;
    }
}

[Menu("AutoConfig/touch", int.MaxValue, no_ci: true)]
public class AutoConfigTouch : AMenuExecute
{
    protected override Task _Execute(CancellationToken token)
    {
        var un_create = AutoConfigUtils.GetUnCreateConfigs();

        if(un_create.Count <= 0)
        {
            AnsiConsole.WriteLine("当前账号的所有配置均已创建...");
            return Task.CompletedTask;
        }

        var files = AnsiConsole.Prompt(
            new MultiSelectionPrompt<string>().Title("请选择要创建的配置: ").
                                               InstructionsText(
                                                   $"(按 [{CustomColor.Blue.ToHex()}]<空格>[/] 勾选, [{CustomColor.Blue.ToHex()}]<确定>[/] 开始执行)"
                                               ).
                                               AddChoices(un_create)
        );

        foreach(var file in files)
        {
            AutoConfigUtils.GetAutoConfig(file);
        }

        return Task.CompletedTask;
    }
}