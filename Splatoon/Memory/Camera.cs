using FFXIVClientStructs.FFXIV.Client.Game.Control;
using GameCamera = FFXIVClientStructs.FFXIV.Client.Game.Camera;

namespace Splatoon;

internal static unsafe class Camera
{
    // 不保存 Camera*：遊戲可能換掉或釋放攝影機物件，每次用時從 CameraManager 重取。
    // Control 位址沒解出來時 CS 的 Instance() 會擲例外；先判 StaticAddressPointers，讓每幀路徑退回預設值。
    private static GameCamera* GetCamera()
    {
        if(Control.StaticAddressPointers.pInstance == null) return null;
        return CameraManager.Instance()->Camera;
    }

    public static void Init()
    {
        if(Control.StaticAddressPointers.pInstance == null)
        {
            PluginLog.Error("Camera: Control.Instance address was not resolved; camera angle and zoom fall back to defaults");
            return;
        }
        PluginLog.Information("Camera initialized successfully");
    }

    internal static float GetAngleX()
    {
        var camera = GetCamera();
        return camera == null ? 0 : camera->DirH;
    }

    internal static float GetAngleY()
    {
        var camera = GetCamera();
        return camera == null ? 0 : camera->DirV;
    }

    internal static float GetZoom()
    {
        var camera = GetCamera();
        return camera == null ? 20 : camera->Distance;
    }
}
