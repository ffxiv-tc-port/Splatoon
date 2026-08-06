namespace Splatoon;

internal static unsafe class Camera
{
    // 🔴 不要把 float* 存成靜態欄位。
    //
    // 原本的寫法在 Init() 當下做一次 `*(nint*)靜態位址`，把攝影機物件的位址凍結下來，
    // 再把 +0x140 / +0x144 / +0x124 三個 float* 存進靜態欄位，之後每一幀都直接解參考。
    // 那是跨幀保存原生指標：遊戲換掉／釋放那個攝影機物件之後，欄位仍然指向舊位址，
    // 讀到的要嘛是垃圾（畫面錯亂），要嘛是已經解除對映的頁面 → AccessViolationException，
    // 而 AVE 在 .NET Core 屬於 corrupted-state exception，try/catch 完全攔不到。
    //
    // 正解是「存來源、不存指標」：只保留特徵碼解出來的那個**靜態位址**（那是模組內的位址，
    // 只要外掛還載著就一直有效），每次要用的時候才重新讀出目前的攝影機指標。
    // 重讀的成本是一次記憶體讀取，放在每幀的繪製路徑上完全可以接受。
    private static nint cameraAddressSource;

    private const int OffsetAngleX = 0x140;
    private const int OffsetAngleY = 0x144;
    private const int OffsetZoom = 0x124;

    public static void Init()
    {
        try
        {
            cameraAddressSource = Svc.SigScanner.GetStaticAddressFromSig("48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 8B 8B ?? ?? ?? ?? 48 85 C9 74 11 48 8B 01");
            if(cameraAddressSource == nint.Zero) throw new Exception("Camera address source was zero");
            PluginLog.Information($"Camera address source: {cameraAddressSource:X16}");
            PluginLog.Information("Camera initialized successfully");
        }
        catch(Exception e)
        {
            cameraAddressSource = nint.Zero;
            e.Log();
        }
    }

    /// <summary>
    /// 每次呼叫都重新讀出目前的攝影機物件位址。特徵碼沒解出來或遊戲還沒建立攝影機時回 0。
    /// </summary>
    private static nint GetCameraAddress()
    {
        if(cameraAddressSource == nint.Zero) return nint.Zero;
        return *(nint*)cameraAddressSource;
    }

    internal static float GetAngleX()
    {
        var camera = GetCameraAddress();
        if(camera == nint.Zero)
        {
            return 0;
        }
        return *(float*)(camera + OffsetAngleX);
    }

    internal static float GetAngleY()
    {
        var camera = GetCameraAddress();
        if(camera == nint.Zero)
        {
            return 0;
        }
        return *(float*)(camera + OffsetAngleY);
    }

    internal static float GetZoom()
    {
        var camera = GetCameraAddress();
        if(camera == nint.Zero)
        {
            return 20;
        }
        return *(float*)(camera + OffsetZoom);
    }
}
