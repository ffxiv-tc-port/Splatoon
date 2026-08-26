using Dalamud.Interface.Utility;
using Dalamud.Utility;
using ECommons;
using ECommons.EzEventManager;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;

namespace Splatoon.Services;

internal unsafe class VbmCamera
{
    public Vector3 Origin;
    public Matrix4x4 View;
    public Matrix4x4 Proj;
    public Matrix4x4 ViewProj;
    public Vector4 NearPlane;
    public float CameraAzimuth; // facing north = 0, facing west = pi/4, facing south = +-pi/2, facing east = -pi/4
    public float CameraAltitude; // facing horizontally = 0, facing down = pi/4, facing up = -pi/4
    public Vector2 ViewportSize;

    private readonly List<(Vector2 from, Vector2 to, uint col)> _worldDrawLines = [];

    private VbmCamera()
    {
        new EzFrameworkUpdate(Update);
    }

    public unsafe void Update()
    {
        // 📌 CameraManager.Instance() 刻意不判空——它只是 (CameraManager*)Control.Instance() 的
        //    轉型,而 Control 是 [StaticAddress(..., isPointer: false)],特徵碼失配時擲例外、
        //    成功時回傳靜態結構本身的位址,永遠不會回 null,判空會是死碼。
        //    下一行的 GetActiveCamera() 回傳值才是真的可能為 null,那個已經有判。
        var controlCamera = CameraManager.Instance()->GetActiveCamera();
        var renderCamera = controlCamera != null ? controlCamera->SceneCamera.RenderCamera : null;
        if(renderCamera == null)
            return;

        Origin = renderCamera->Origin;
        View = renderCamera->ViewMatrix;
        View.M44 = 1; // for whatever reason, game doesn't initialize it...
        Proj = renderCamera->ProjectionMatrix;
        ViewProj = View * Proj;

        // note that game uses reverse-z by default, so we can't just get full plane equation by reading column 3 of vp matrix
        // so just calculate it manually: column 3 of view matrix is plane equation for a plane equation going through origin
        // proof:
        // plane equation p is such that p.dot(Q, 1) = 0 if Q lines on the plane => pw = -Q.dot(n); for view matrix, V43 is -origin.dot(forward)
        // plane equation for near plane has Q.dot(n) = O.dot(n) - near => pw = V43 + near
        NearPlane = new(View.M13, View.M23, View.M33, View.M43 + renderCamera->NearPlane);

        CameraAzimuth = MathF.Atan2(View.M13, View.M33);
        CameraAltitude = MathF.Asin(View.M23);
        // 🔴 Device 是 [StaticAddress(..., isPointer: true)],Instance() 回傳靜態槽「裡面的值」,
        //    可以合法為 null(裝置重建、切換解析度的空窗)。直接 -> 解參考產生的
        //    AccessViolationException 在 .NET Core 是 corrupted-state exception,try/catch 攔不到。
        //    取不到就保留上一幀的 ViewportSize 不更新(fail-closed)。
        var device = FFXIVClientStructs.FFXIV.Client.Graphics.Kernel.Device.Instance();
        if(device == null)
            return;
        ViewportSize = new(device->Width, device->Height);
    }

    public bool WorldToScreen(Vector3 worldPos, out Vector2 screenPos)
    {
        // Read current ViewProjectionMatrix plus game window size
        var windowPos = ImGuiHelpers.MainViewport.Pos;
        var viewProjectionMatrix = ViewProj;
        // 🔴 同上:Device.Instance() 可以合法回 null。取不到就當作「這個點不在畫面上」
        //    (fail-closed),不要對位址 0 解參考。
        var device = Device.Instance();
        if(device == null)
        {
            screenPos = Vector2.Zero;
            return false;
        }
        float width = device->Width;
        float height = device->Height;

        var pCoords = Vector4.Transform(new Vector4(worldPos, 1.0f), viewProjectionMatrix);
        var inFront = pCoords.W > 0.0f;
        var inView = false;
        if(Math.Abs(pCoords.W) < float.Epsilon)
        {
            screenPos = Vector2.Zero;
            inView = false;
            return false;
        }

        pCoords *= MathF.Abs(1.0f / pCoords.W);
        screenPos = new Vector2(pCoords.X, pCoords.Y);

        screenPos.X = (0.5f * width * (screenPos.X + 1f)) + windowPos.X;
        screenPos.Y = (0.5f * height * (1f - screenPos.Y)) + windowPos.Y;

        inView = inFront &&
                 screenPos.X > windowPos.X && screenPos.X < windowPos.X + width &&
                 screenPos.Y > windowPos.Y && screenPos.Y < windowPos.Y + height;

        return inFront && inView;
    }
}