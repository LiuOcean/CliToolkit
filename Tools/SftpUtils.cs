using System.Collections.Concurrent;
using System.Text;
using CliToolkit.AutoConfig;
using CliToolkit.Commands;
using Renci.SshNet;
using Renci.SshNet.Sftp;
using Spectre.Console;

namespace CliToolkit.Tools;

public class SftpPool : IDisposable
{
    private static ConcurrentQueue<SftpPool> _pool = new();

    private const int BUFFER_SIZE = 4 * 1024;

    public SftpClient client { get; }

    private SftpPool()
    {
        var config = AutoConfigUtils.GetAutoConfig<SftpAutoConfig>();
        client = new SftpClient(config.ip, config.user_name, config.password);

        client.Connect();
        client.BufferSize = BUFFER_SIZE;
    }

    public static void Prewarm()
    {
        for(int i = 0; i < Settings.Current.max_upload_client; i++)
        {
            _pool.Enqueue(new SftpPool());
        }
    }

    public static SftpPool Get()
    {
        if(!_pool.TryDequeue(out var pool))
        {
            return new SftpPool();
        }

        if(!pool.client.IsConnected)
        {
            pool.client.Connect();
        }

        return pool;
    }

    public void Dispose() { _pool.Enqueue(this); }
}

public static class SftpUtils
{
    private static SftpClient DEFAULT
    {
        get
        {
            if(_default is {IsConnected: true})
            {
                return _default;
            }

            _default = SftpPool.Get().client;

            return _default;
        }
    }

    private static SftpClient? _default;

    static SftpUtils() { SftpPool.Prewarm(); }

    #region Get

    /// <summary>
    /// 读一个文件
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    public static string ReadFile(string path)
    {
        if(!DEFAULT.Exists(path))
        {
            return string.Empty;
        }

        var file_info = DEFAULT.Get(path);

        if(file_info.IsDirectory)
        {
            return string.Empty;
        }

        return DEFAULT.ReadAllText(path, Encoding.UTF8);
    }

    /// <summary>
    /// 获取当前文件夹中所有文件信息
    /// </summary>
    /// <param name="working_dir"></param>
    /// <returns></returns>
    public static async Task<IEnumerable<SftpFile>?> GetAllFileInfo(string working_dir = ".")
    {
        return await DEFAULT.ListDirectoryAsync(working_dir);
    }

    /// <summary>
    /// 递归获取所有文件及文件夹信息
    /// </summary>
    /// <param name="files"></param>
    /// <param name="working_dir"></param>
    public static async Task GetAllFileInfoRecursive(List<SftpFile>? files, string working_dir = ".")
    {
        foreach(var file in await DEFAULT.ListDirectoryAsync(working_dir) ?? new List<SftpFile>())
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
        foreach(var file in await DEFAULT.ListDirectoryAsync(working_dir) ?? new List<SftpFile>())
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
        if(!DEFAULT.Exists(path))
        {
            return;
        }

        var info = DEFAULT.Get(path);

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
    public static void DeleteFile(string path) { DEFAULT.DeleteFile(path); }

    /// <summary>
    /// 删除一个文件夹
    /// </summary>
    /// <param name="path"></param>
    public static void DeleteDirectory(string path) { DEFAULT.DeleteDirectory(path); }

    #endregion

    #region Permision

    public static void ChangePermission(string path, short permission) { DEFAULT.ChangePermissions(path, permission); }

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

        await Task.Run(
            () =>
            {
                progress?.MaxValue(info.Length);
                progress?.StartTask();
                ulong last_size = 0;

                using var pool = SftpPool.Get();

                // 当 Windows 上传 Linux 时, 需要将路径中的 \ 替换为 /
                server_path = server_path.Replace("\\", "/");
                
                ServerDirSafeCheck(pool.client, server_path);

                pool.client.UploadFile(
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

        HashSet<Task> tasks    = new();
        List<Task>    finished = new();

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
                tasks.Add(
                    UploadFile(
                        path,
                        Path.Combine(server_path, info.Name),
                        progress,
                        permission
                    )
                );

                if(tasks.Count < Settings.Current.max_upload_client)
                {
                    continue;
                }

                await Task.WhenAny(tasks);

                foreach(var task in tasks.Where(task => task.IsCompleted))
                {
                    finished.Add(task);

                    if(task.Status == TaskStatus.Faulted)
                    {
                        failed++;
                    }
                    else
                    {
                        success++;
                    }
                }

                foreach(var task in finished)
                {
                    tasks.Remove(task);
                }

                finished.Clear();
            }
            catch(Exception)
            {
                failed++;
            }
        }

        if(tasks.Count > 0)
        {
            await Task.WhenAll(tasks);
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
    public static async Task<bool> UploadFilesWithProgress(IEnumerable<string> files, string server_dir)
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

        return failed <= 0;
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

        int retry = 3;

        while(true)
        {
            var result = await UploadFilesWithProgress(diff_files, server_dir);

            if(result)
            {
                break;
            }

            if(retry <= 0)
            {
                break;
            }

            retry--;

            AnsiConsole.Write("上传失败, 重试中...");
        }
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

        foreach(var file in await DEFAULT.ListDirectoryAsync(remote_dir) ?? new List<SftpFile>())
        {
            string path = file.FullName.Replace($"{remote_dir}", "");

            if(path.StartsWith("/"))
            {
                path = path.TrimStart('/');
            }

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

            string relative_path = local_path.Replace($"{local_dir}", "");

            if(relative_path.StartsWith("/"))
            {
                relative_path = relative_path.TrimStart('/');
            }

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

    public static void ServerDirSafeCheck(SftpClient client, string path)
    {
        int index = 0;

        while(true)
        {
            index++;

            if(index >= path.Length)
            {
                break;
            }

            if(path[index] != '/')
            {
                continue;
            }

            if(!client.Exists(path[..index]))
            {
                client.CreateDirectory(path[..index]);
            }
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

        if(!DEFAULT.Exists(path))
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