using System.Text;
using CliToolkit.AutoConfig;
using Renci.SshNet;
using Renci.SshNet.Sftp;
using Spectre.Console;

namespace CliToolkit.Tools;

public static class SftpUtils
{
    private const int BUFFER_SIZE = 4 * 1024;

    private static SftpClient CLIENT
    {
        get
        {
            var config = AutoConfigUtils.GetAutoConfig<SftpAutoConfig>();

            if(_CLIENT is not null)
            {
                if(!_CLIENT.IsConnected)
                {
                    goto Connect;
                }

                return _CLIENT;
            }

            var client = new SftpClient(config.ip, config.user_name, config.password);
            _CLIENT = client;

            Connect:
            _CLIENT.Connect();
            _CLIENT.BufferSize = BUFFER_SIZE;

            return _CLIENT;
        }
    }

    private static SftpClient? _CLIENT;

    static SftpUtils()
    {
        // 保证在程序退出前, 断开 sftp 链接
        AppDomain.CurrentDomain.ProcessExit += (_, _) => { _CLIENT?.Dispose(); };
    }

    #region Get

    /// <summary>
    /// 读一个文件
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    public static string ReadFile(string path)
    {
        if(!CLIENT.Exists(path))
        {
            return string.Empty;
        }

        var file_info = CLIENT.Get(path);

        if(file_info.IsDirectory)
        {
            return string.Empty;
        }

        return CLIENT.ReadAllText(path, Encoding.UTF8);
    }

    /// <summary>
    /// 获取当前文件夹中所有文件信息
    /// </summary>
    /// <param name="working_dir"></param>
    /// <returns></returns>
    public static async Task<IEnumerable<SftpFile>?> GetAllFileInfo(string working_dir = ".")
    {
        return await CLIENT.ListDirectoryAsync(working_dir);
    }

    /// <summary>
    /// 递归获取所有文件及文件夹信息
    /// </summary>
    /// <param name="files"></param>
    /// <param name="working_dir"></param>
    public static async Task GetAllFileInfoRecursive(List<SftpFile>? files, string working_dir = ".")
    {
        foreach(var file in await CLIENT.ListDirectoryAsync(working_dir) ?? new List<SftpFile>())
        {
            if(string.Equals(file.Name, ".") || string.Equals(file.Name, ".."))
            {
                continue;
            }

            if(file.IsDirectory)
            {
                await GetAllFileInfoRecursive(files, file.FullName);
            }
            else
            {
                files?.Add(file);
            }
        }
    }

    /// <summary>
    /// 递归获取所有文件夹信息
    /// </summary>
    /// <param name="files"></param>
    /// <param name="working_dir"></param>
    public static async Task GetAllDirInfoRecursive(List<SftpFile>? files, string working_dir = ".")
    {
        foreach(var file in await CLIENT.ListDirectoryAsync(working_dir) ?? new List<SftpFile>())
        {
            if(string.Equals(file.Name, ".") || string.Equals(file.Name, ".."))
            {
                continue;
            }

            if(!file.IsDirectory)
            {
                continue;
            }

            await GetAllDirInfoRecursive(files, file.FullName);
            files?.Add(file);
        }
    }

    #endregion

    #region Delete

    public static void Delete(string path)
    {
        if(!CLIENT.Exists(path))
        {
            return;
        }

        var info = CLIENT.Get(path);

        if(info.IsDirectory)
        {
            DeleteDirectory(path);
        }
        else
        {
            DeleteFile(path);
        }
    }

    /// <summary>
    /// 删除一个文件
    /// </summary>
    /// <param name="path"></param>
    public static void DeleteFile(string path) { CLIENT.DeleteFile(path); }

    /// <summary>
    /// 删除一个文件夹
    /// </summary>
    /// <param name="path"></param>
    public static void DeleteDirectory(string path) { CLIENT.DeleteDirectory(path); }

    #endregion

    #region Permision

    public static void ChangePermission(string path, short permission) { CLIENT.ChangePermissions(path, permission); }

    #endregion

    #region Upload

    /// <summary>
    /// 上传一个文件
    /// </summary>
    /// <param name="local_path"></param>
    /// <param name="server_path"></param>
    /// <param name="progress"></param>
    /// <param name="permission"></param>
    /// <returns></returns>
    public static async Task UploadFile(string        local_path,
                                        string        server_path,
                                        ProgressTask? progress,
                                        short         permission = 755)
    {
        if(!File.Exists(local_path))
        {
            return;
        }

        await using var fs = new FileStream(local_path, FileMode.Open);

        FileInfo info = new FileInfo(local_path);

        ServerDirSafeCheck(server_path);

        await Task.Run(
            () =>
            {
                progress?.MaxValue(info.Length);
                progress?.StartTask();
                ulong last_size = 0;

                CLIENT.UploadFile(
                    fs,
                    server_path,
                    size =>
                    {
                        progress?.Increment(size - last_size);
                        last_size = size;
                    }
                );

                ChangePermission(server_path, permission);
            }
        );
    }

    /// <summary>
    /// 上传指定的所有文件
    /// </summary>
    /// <param name="files"></param>
    /// <param name="server_path"></param>
    /// <param name="ctx"></param>
    /// <param name="permission"></param>
    /// <returns></returns>
    public static async Task<Tuple<int, int, int>?> UploadFiles(IEnumerable<string> files,
                                                                string              server_path,
                                                                ProgressContext     ctx,
                                                                short               permission = 755)
    {
        var config = AutoConfigUtils.GetAutoConfig<SftpIgnoreAutoConfig>();

        int success = 0;
        int failed  = 0;
        int ignored = 0;

        foreach(var path in files)
        {
            FileInfo info = new FileInfo(path);

            if(config.IsIgnore(info.Name))
            {
                ignored++;
                continue;
            }

            var progress = ctx.AddTask(info.Name, new ProgressTaskSettings {AutoStart = false});

            try
            {
                // TODO SFTP 多个文件同时上传没有跑通
                await UploadFile(
                    path,
                    Path.Combine(server_path, info.Name),
                    progress,
                    permission
                );
                success++;
            }
            catch(Exception)
            {
                failed++;
            }
        }

        return new Tuple<int, int, int>(success, ignored, failed);
    }

    /// <summary>
    /// 上传文件夹, 并显示进度条
    /// </summary>
    /// <param name="local_dir"></param>
    /// <param name="server_dir"></param>
    public static Task UploadFilesWithProgress(string local_dir, string server_dir)
    {
        var files = Directory.GetFiles(local_dir, "*", SearchOption.AllDirectories);

        return UploadFilesWithProgress(files, server_dir);
    }

    /// <summary>
    /// 上传指定文件夹并显示进度条
    /// </summary>
    /// <param name="files"></param>
    /// <param name="server_dir"></param>
    public static async Task UploadFilesWithProgress(IEnumerable<string> files, string server_dir)
    {
        Tuple<int, int, int>? result = null;

        await AnsiConsole.Progress().
                          Columns(
                              new TaskDescriptionColumn(),
                              new ProgressBarColumn(),
                              new PercentageColumn(),
                              new RemainingTimeColumn(),
                              new SpinnerColumn()
                          ).
                          StartAsync(async ctx => { result = await UploadFiles(files, server_dir, ctx); });

        var (success, ignored, failed) = result ?? new Tuple<int, int, int>(0, 0, 0);

        AnsiConsole.Write(
            new BarChart().Width(60).
                           Label("上传结果").
                           AddItem("成功", success, Color.Green).
                           AddItem("忽略", ignored, Color.Yellow).
                           AddItem("失败", failed,  Color.Red)
        );
    }

    /// <summary>
    /// 差分上传, 并显示进度条
    /// </summary>
    /// <param name="local_dir"></param>
    /// <param name="server_dir"></param>
    public static async Task DiffUploadWithProgress(string local_dir, string server_dir)
    {
        var (diff_files, total_count, ignored) = await DiffLocalAndRemoteFile(local_dir, server_dir);

        AnsiConsole.Write(
            new BarChart().Width(60).
                           Label("差分结果").
                           AddItem("总计",  total_count,                    Color.Yellow).
                           AddItem("待上传", diff_files.Count,               Color.Green).
                           AddItem("已上传", total_count - diff_files.Count, Color.Red).
                           AddItem("忽略",  ignored,                        Color.Red1)
        );

        if(diff_files.Count <= 0)
        {
            AnsiConsole.Write("本地和远端文件已经同步, 无需上传...");
            return;
        }

        await UploadFilesWithProgress(diff_files, server_dir);
    }

    #endregion

    #region Tools

    /// <summary>
    /// 对本地和远端指定路径的文件进行 diff, 以本地为准返回不同的文件地址
    /// </summary>
    /// <param name="local_dir"></param>
    /// <param name="remote_dir"></param>
    /// <returns></returns>
    public static async Task<(HashSet<string>, int, int)> DiffLocalAndRemoteFile(string local_dir, string remote_dir)
    {
        var local_set = Directory.GetFiles(local_dir, "*", SearchOption.AllDirectories).ToHashSet();

        Dictionary<string, SftpFile> remote_files = new();

        foreach(var file in await CLIENT.ListDirectoryAsync(remote_dir) ?? new List<SftpFile>())
        {
            string path = file.FullName.Replace($"{remote_dir}/", "");

            remote_files[path] = file;
        }

        int ignored = 0;

        HashSet<string> result = new();

        var ignore_config = AutoConfigUtils.GetAutoConfig<SftpIgnoreAutoConfig>();

        foreach(var local_path in local_set)
        {
            FileInfo local_info = new FileInfo(local_path);

            if(ignore_config.IsIgnore(local_info.Name))
            {
                ignored++;
                continue;
            }

            string relative_path = local_path.Replace($"{local_dir}/", "");

            if(remote_files.TryGetValue(relative_path, out var remote_info))
            {
                // 此时说明两个文件完全一致
                // 直接 continue
                if(local_info.Length == remote_info.Length)
                {
                    continue;
                }
            }

            result.Add(local_path);
        }

        return(result, local_set.Count, ignored);
    }

    public static void ServerDirSafeCheck(string path)
    {
        var span  = path.AsSpan();
        int index = span.LastIndexOf("/");

        var dir = span[..index].ToString();

        if(!CLIENT.Exists(dir))
        {
            CLIENT.CreateDirectory(dir);
        }
    }

    /// <summary>
    /// 将 SftpFile 转化为命令行 UI Tree
    /// </summary>
    /// <param name="files"></param>
    /// <returns></returns>
    public static Tree Path2Tree(IEnumerable<SftpFile>? files)
    {
        var tree = new Tree("/");

        switch(files)
        {
            case null: return tree;
            case List<SftpFile> sort:
                sort.Sort((x, y) => string.Compare(x.FullName, y.FullName, StringComparison.Ordinal));
                break;
        }

        var dir_2_node = new Dictionary<string, TreeNode>();

        foreach(var file in files)
        {
            TreeNode node;
            if(file.IsDirectory)
            {
                dir_2_node.TryGetValue(file.FullName, out node);

                if(node is null)
                {
                    node = tree.AddNode(file.FullName);
                    dir_2_node.Add(file.FullName, node);
                }

                continue;
            }

            var span = file.FullName.AsSpan();

            var index = span.LastIndexOf("/");

            var dir = span[..index].ToString();

            dir_2_node.TryGetValue(dir, out node);

            if(node is null)
            {
                node = tree.AddNode(dir);
                dir_2_node.Add(dir, node);
            }

            node.AddNode(file.Name);
        }

        return tree;
    }

    /// <summary>
    /// 封装同步获取文件代码为 async
    /// </summary>
    /// <param name="self"></param>
    /// <param name="path"></param>
    /// <returns></returns>
    public static Task<IEnumerable<SftpFile>?> ListDirectoryAsync(this SftpClient self, string path)
    {
        var tcs = new TaskCompletionSource<IEnumerable<SftpFile>?>();

        if(!CLIENT.Exists(path))
        {
            tcs.SetResult(null);
        }

        self.BeginListDirectory(
            path,
            async_result =>
            {
                try
                {
                    tcs.TrySetResult(self.EndListDirectory(async_result));
                }
                catch(OperationCanceledException)
                {
                    tcs.TrySetCanceled();
                }
                catch(Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            },
            null
        );
        return tcs.Task;
    }

    #endregion
}