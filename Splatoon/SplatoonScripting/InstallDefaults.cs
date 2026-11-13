namespace Splatoon.SplatoonScripting;

/// <summary>新安裝腳本的預設啟停狀態。刻意不依賴插件其餘部分，好讓探針單獨編譯這個檔。</summary>
internal static class InstallDefaults
{
    /// <summary>
    /// 安裝前磁碟上沒有這支腳本的檔案＝新安裝：寫進停用清單並回 <see langword="true"/>。
    /// 檔案已存在（更新、重裝、編譯失敗後重抓）一律不動使用者原本的啟停狀態。
    /// </summary>
    internal static bool DisableIfNewInstall(ISet<string> disabledScripts, string fullName, bool fileExistedBefore)
    {
        if(fileExistedBefore) return false;
        disabledScripts.Add(fullName);
        return true;
    }
}
