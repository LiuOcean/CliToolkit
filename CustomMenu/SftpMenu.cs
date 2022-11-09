using CliToolkit.AutoConfig;
using CliToolkit.Commands;
using CliToolkit.Menus;
using CliToolkit.Tools;
using Renci.SshNet.Sftp;
using Spectre.Console;

namespace CliToolkit.CustomMenu;

[MenuEnter("Sftp")]
public class SftpEnter : AMenuEnter
{
    protected override Task _WhenEnter(CancellationToken token)
    {
        AnsiConsole.WriteLine("检查 Sftp 配置文件...");
        // 当第一次进入 Sftp 菜单时, 会检查当前 Sftp 是否已经正确配置
        AutoConfigUtils.GetAutoConfig<SftpAutoConfig>();
        AutoConfigUtils.GetAutoConfig<SftpIgnoreAutoConfig>();

        return Task.CompletedTask;
    }
}

[Menu("Sftp/YooAsset")]
public class SftpYooAsset : AMenuExecute
{
    protected override Task _Execute(CancellationToken token)
    {
        var local_dir  = Settings.Current.yooasset_local_dir;
        var remote_dir = Settings.Current.yooaset_remote_dir;

        if(string.IsNullOrEmpty(local_dir))
        {
            local_dir = AnsiConsole.Prompt(new TextPrompt<string>("请输入本地文件夹路径: ").Validate(Directory.Exists));
        }

        if(string.IsNullOrEmpty(remote_dir))
        {
            remote_dir = AnsiConsole.Prompt(new TextPrompt<string>("请输入远端文件夹路径"));
        }

        return SftpUtils.DiffUploadWithProgress(local_dir, remote_dir);
    }
}

[Menu("Sftp/ls", no_ci: true)]
public class SftpLs : AMenuExecute
{
    protected override async Task _Execute(CancellationToken token)
    {
        string path;

        if(AnsiConsole.Confirm("是否手动输入?"))
        {
            path = AnsiConsole.Prompt(new TextPrompt<string>("请输入路径: "));
        }
        else
        {
            var dirs = new List<SftpFile>();

            await SftpUtils.GetAllDirInfoRecursive(dirs);

            var choices = new List<string>();

            foreach(var dir in dirs)
            {
                choices.Add(dir.FullName);
            }

            path = AnsiConsole.Prompt(new SelectionPrompt<string>().AddChoices(choices));
        }

        IEnumerable<SftpFile>? files = null;

        await AnsiConsole.Status().StartAsync("读取中...", async _ => { files = await SftpUtils.GetAllFileInfo(path); });

        if(files is null)
        {
            return;
        }

        AnsiConsole.Write(SftpUtils.Path2Tree(files));
    }
}

[Menu("Sftp/ls-r", no_ci: true)]
public class SftpLsR : AMenuExecute
{
    protected override async Task _Execute(CancellationToken token)
    {
        var files = new List<SftpFile>();

        await AnsiConsole.Status().StartAsync("读取中...", async _ => { await SftpUtils.GetAllFileInfoRecursive(files); });

        AnsiConsole.Write(SftpUtils.Path2Tree(files));
    }
}

[Menu("Sftp/cat")]
public class SftpCat : AMenuExecute
{
    protected override Task _Execute(CancellationToken token)
    {
        string path = AnsiConsole.Prompt(new TextPrompt<string>("请输入文件路径: "));
        string file = string.Empty;

        AnsiConsole.Status().Start("读取中...", _ => { file = SftpUtils.ReadFile(path); });

        if(!string.IsNullOrEmpty(file))
        {
            AnsiConsole.Write(new Panel(Markup.Escape(file)) {Header = new PanelHeader(path)});
        }

        return Task.CompletedTask;
    }
}

[Menu("Sftp/upload", no_ci: true)]
public class SftpUpload : AMenuExecute
{
    protected override async Task _Execute(CancellationToken token)
    {
        string local_path = AnsiConsole.Prompt(
            new TextPrompt<string>("请输入本地文件路径:").Validate(s => File.Exists(s) || Directory.Exists(s))
        );

        string server_dir = AnsiConsole.Prompt(new TextPrompt<string>("请输入服务器文件夹:"));

        if(File.Exists(local_path))
        {
            FileInfo info = new FileInfo(local_path);

            server_dir = Path.Combine(server_dir, info.Name);

            await AnsiConsole.Progress().
                              StartAsync(
                                  async ctx =>
                                  {
                                      ProgressTask task = ctx.AddTask(
                                          $"正在上传 {local_path}",
                                          new ProgressTaskSettings {AutoStart = false}
                                      );

                                      await SftpUtils.UploadFile(local_path, server_dir, task);
                                  }
                              );
            return;
        }

        await SftpUtils.UploadFilesWithProgress(local_path, server_dir);
    }
}

[Menu("Sftp/rm", no_ci: true)]
public class SftpRm : AMenuExecute
{
    protected override Task _Execute(CancellationToken token)
    {
        var path = AnsiConsole.Prompt(new TextPrompt<string>("请输入要删除的文件路径:"));

        AnsiConsole.Status().Start("删除中...", _ => { SftpUtils.Delete(path); });

        return Task.CompletedTask;
    }
}