using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Splatoon.Modules;

/// <summary>
/// 判斷直播軟體行程在不在。可在任何執行緒呼叫；快照路徑不建 <see cref="Process"/>、不配置受管理物件。
/// 名稱規則與 <see cref="Process.ProcessName"/> 相同（去路徑、去結尾 .exe 不分大小寫），比對一律 OrdinalIgnoreCase。
/// </summary>
internal static unsafe class StreamingSoftwareScan
{
    internal static bool IsStreamingSoftwareRunning()
    {
        var snapshotResult = ScanSnapshot();
        if(snapshotResult >= 0) return snapshotResult == 1;
        return ScanProcessList();
    }

    internal static bool IsStreamingSoftwareName(ReadOnlySpan<char> processName)
    {
        return processName.Equals("obs32", StringComparison.OrdinalIgnoreCase)
            || processName.Equals("obs64", StringComparison.OrdinalIgnoreCase)
            || processName.StartsWith("XSplit", StringComparison.OrdinalIgnoreCase);
    }

    internal static ReadOnlySpan<char> ToProcessName(ReadOnlySpan<char> imageName)
    {
        imageName = imageName.Slice(imageName.LastIndexOf('\\') + 1);
        if(imageName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            imageName = imageName.Slice(0, imageName.Length - 4);
        }
        return imageName;
    }

    /// <returns>1＝找到、0＝沒有、-1＝快照不可用（呼叫端改走 <see cref="ScanProcessList"/>）。</returns>
    internal static int ScanSnapshot()
    {
        var snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
        if(snapshot == INVALID_HANDLE_VALUE || snapshot == 0) return -1;
        try
        {
            ProcessEntry32W entry = default;
            entry.dwSize = (uint)sizeof(ProcessEntry32W);
            if(Process32FirstW(snapshot, &entry) == 0) return -1;
            do
            {
                var exeFile = new ReadOnlySpan<char>(entry.szExeFile, MaxPath);
                var end = exeFile.IndexOf('\0');
                if(end >= 0) exeFile = exeFile.Slice(0, end);
                if(IsStreamingSoftwareName(ToProcessName(exeFile))) return 1;
            }
            while(Process32NextW(snapshot, &entry) != 0);
            return Marshal.GetLastPInvokeError() == ERROR_NO_MORE_FILES ? 0 : -1;
        }
        finally
        {
            CloseHandle(snapshot);
        }
    }

    internal static bool ScanProcessList()
    {
        var processes = Process.GetProcesses();
        try
        {
            foreach(var process in processes)
            {
                if(IsStreamingSoftwareName(process.ProcessName)) return true;
            }
            return false;
        }
        finally
        {
            foreach(var process in processes) process.Dispose();
        }
    }

    private const uint TH32CS_SNAPPROCESS = 0x00000002;
    private const int ERROR_NO_MORE_FILES = 18;
    private const int MaxPath = 260;
    private const nint INVALID_HANDLE_VALUE = -1;

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessEntry32W
    {
        public uint dwSize;
        public uint cntUsage;
        public uint th32ProcessID;
        public nuint th32DefaultHeapID;
        public uint th32ModuleID;
        public uint cntThreads;
        public uint th32ParentProcessID;
        public int pcPriClassBase;
        public uint dwFlags;
        public fixed char szExeFile[MaxPath];
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern int Process32FirstW(nint hSnapshot, ProcessEntry32W* lppe);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern int Process32NextW(nint hSnapshot, ProcessEntry32W* lppe);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern int CloseHandle(nint hObject);
}
