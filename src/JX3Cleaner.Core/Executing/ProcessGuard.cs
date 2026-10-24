using System.Diagnostics;
using JX3Cleaner.Core.Models;

namespace JX3Cleaner.Core.Executing;

/// <summary>清理前的进程守卫：游戏运行时文件被占用，删除会大面积失败。</summary>
public static class ProcessGuard
{
    private static readonly string[] GameProcesses =
    [
        "JX3Client", "JX3ClientX64", "JX3Client_x64", "GameConfig", "GameDoctor"
    ];

    public sealed record RunningProcess(string Name, int Id);

    public static List<RunningProcess> FindRunningGame()
    {
        var result = new List<RunningProcess>();
        foreach (var name in GameProcesses)
        {
            Process[] found;
            try { found = Process.GetProcessesByName(name); }
            catch { continue; }

            foreach (var p in found)
            {
                try { result.Add(new RunningProcess(p.ProcessName, p.Id)); }
                catch { }
                finally { p.Dispose(); }
            }
        }
        return result;
    }

    public static bool IsGameRunning => FindRunningGame().Count > 0;
}
