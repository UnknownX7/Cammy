using System;
using Cammy.Structures;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Hooking;
using Dalamud.Logging;

namespace Cammy
{
    public static unsafe class Game
    {
        public static CameraManager* cameraManager;

        // This variable is merged with a lot of other constants so it's not possible to change normally
        public static float zoomDelta = 0.75f;
        private delegate float GetZoomDeltaDelegate();
        private static Hook<GetZoomDeltaDelegate> GetZoomDeltaHook;
        private static float GetZoomDeltaDetour() => zoomDelta;

        // Of course this isn't though
        private static IntPtr foVDeltaPtr;
        public static ref float FoVDelta => ref *(float*)foVDeltaPtr; // 0.08726646751

        private delegate IntPtr GetCameraTargetDelegate(IntPtr camera);
        private static Hook<GetCameraTargetDelegate> GetCameraTargetHook;
        private static IntPtr GetCameraTargetDetour(IntPtr camera) => DalamudApi.TargetManager.FocusTarget is { } target
            ? target.Address
            : DalamudApi.ClientState.LocalPlayer is { } player ? player.Address : IntPtr.Zero;

        public static readonly Memory.Replacer cameraNoCollideReplacer = new("E8 ?? ?? ?? ?? 45 0F 57 FF", new byte[] { 0x30, 0xC0, 0x90, 0x90, 0x90 }); // E8 ?? ?? ?? ?? 48 8B B4 24 E0 00 00 00 40 32 FF (0x90, 0x90, 0x90, 0x90, 0x90)

        public static bool isLoggedIn = false;
        public static bool onLogin = false;
        public static int changingAreaDelay = 0;
        public static bool isChangingAreas = false;

        public static void Initialize()
        {
            cameraManager = (CameraManager*)DalamudApi.SigScanner.GetStaticAddressFromSig("48 8D 35 ?? ?? ?? ?? 48 8B 34 C6 F3"); // g_ControlSystem_CameraManager

            var vtbl = (IntPtr*)*(IntPtr*)cameraManager->WorldCamera;
            GetZoomDeltaHook = new(*(vtbl + 27), GetZoomDeltaDetour); // Client__Game__Camera_vf27
            GetZoomDeltaHook.Enable();

            //GetCameraTargetHook = new(*(vtbl + 16), GetCameraTargetDetour); // Client__Game__Camera_vf16
            //GetCameraTargetHook.Enable();

            //var changeCamera = (delegate*<IntPtr, int, byte, void>)(Cammy.Interface.TargetModuleScanner.Module.BaseAddress + 0x1132E70);
            //changeCamera(cameraManager, 2, 0);

            //var changeCamera = (delegate*<IntPtr>)(Cammy.Interface.TargetModuleScanner.Module.BaseAddress + 0x2D58D0);
            //changeCamera();

            foVDeltaPtr = DalamudApi.SigScanner.GetStaticAddressFromSig("F3 0F 59 05 ?? ?? ?? ?? 0F 28 74 24 20 48 83 C4 30 5B C3 0F 57 C0 0F"); // F3 0F 59 05 ?? ?? ?? ?? 0F 28 74 24 20 48 83 C4 30 5B C3 0F 57 C0 0F 28 74 24 20 48 83 C4 30 5B C3
        }

        public static void Update()
        {
            if (onLogin)
                onLogin = false;

            if (!isLoggedIn)
                onLogin = isLoggedIn = DalamudApi.ClientState.IsLoggedIn && !DalamudApi.Condition[ConditionFlag.BetweenAreas];

            if (isChangingAreas && !DalamudApi.Condition[ConditionFlag.BetweenAreas] && --changingAreaDelay == 0)
                isChangingAreas = false;
        }

        /*
        public unsafe void ToggleFreecam()
        {
            var enable = freeCamera == null;
            var isMainMenu = !DalamudApi.Condition.Any();
            if (enable)
            {
                freeCamera = isMainMenu ? menuCamera : worldCamera;
                *(byte*)(freeCamera.Address + 0x2A0) = 0;
                freeCamera.MinVRotation = -1.559f;
                freeCamera.MaxVRotation = 1.559f;
                freeCamera.CurrentFoV = freeCamera.MinFoV = freeCamera.MaxFoV = 0.78f;
                freeCamera.CurrentZoom = freeCamera.MinZoom = freeCamera.MaxZoom = freeCamera.AddedFoV = 0;
                cameraNoCollideReplacer.Enable();

                //if (!isMainMenu)
                    //GetCameraTargetHook.Enable();
            }
            else
            {
                freeCamera = null;
                cameraNoCollideReplacer.Disable();
                if (!isMainMenu)
                {
                    LoadPreset(true);
                    //GetCameraTargetHook.Disable();
                }
            }

            if (isMainMenu)
            {
                static void ToggleAddonVisible(string name)
                {
                    var addon = DalamudApi.GameGui.GetAddonByName(name, 1);
                    if (addon == IntPtr.Zero) return;
                    ((AtkUnitBase*)addon)->IsVisible ^= true;
                }

                ToggleAddonVisible("_TitleRights");
                ToggleAddonVisible("_TitleRevision");
                ToggleAddonVisible("_TitleMenu");
                ToggleAddonVisible("_TitleLogo");
            }
        }

        public void Update()
        {
            if (freeCamera == null) return;

            var keyState = DalamudApi.KeyState;

            if (keyState[27] || (!DalamudApi.Condition.Any() && DalamudApi.GameGui.GetAddonByName("Title", 1) == IntPtr.Zero)) // Esc
            {
                ToggleFreecam();
                return;
            }

            var movePos = Vector3.Zero;

            if (keyState[87]) // W
                movePos.X += 1;

            if (keyState[65]) // A
                movePos.Y += 1;

            if (keyState[83]) // S
                movePos.X += -1;

            if (keyState[68]) // D
                movePos.Y += -1;

            if (keyState[32]) // Space
                movePos.Z += 1;

            if (keyState[67]) // C
            {
                freeCamera.X = 0;
                freeCamera.Y = 0;
                freeCamera.Z = 0;
                freeCamera.Z2 = 0;
            }

            if (movePos == Vector3.Zero) return;

            movePos *= ImGui.GetIO().DeltaTime * 20;

            if (ImGui.GetIO().KeyShift)
                movePos *= 10;
            const double halfPI = Math.PI / 2f;
            var hAngle = freeCamera.HRotation + halfPI;
            var vAngle = freeCamera.CurrentVRotation;
            var direction = new Vector3((float)(Math.Cos(hAngle) * Math.Cos(vAngle)), -(float)(Math.Sin(hAngle) * Math.Cos(vAngle)), (float)Math.Sin(vAngle));

            var amount = direction * movePos.X;
            freeCamera.X += amount.X + movePos.Y * (float)Math.Sin(freeCamera.HRotation - halfPI);
            freeCamera.Y += amount.Y + movePos.Y * (float)Math.Cos(freeCamera.HRotation - halfPI);
            freeCamera.Z2 = freeCamera.Z += amount.Z + movePos.Z;
        }

        public void Draw()
        {
            if (freeCamera == null && DalamudApi.GameGui.GetAddonByName("Title", 1) != IntPtr.Zero)
            {
                ImGuiHelpers.ForceNextWindowMainViewport();
                var size = new Vector2(50) * ImGuiHelpers.GlobalScale;
                ImGui.SetNextWindowSize(size, ImGuiCond.Always);
                ImGuiHelpers.SetNextWindowPosRelativeMainViewport(new Vector2(ImGuiHelpers.MainViewport.Size.X - size.X, 0), ImGuiCond.Always);
                ImGui.Begin("Freecam Button", ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoFocusOnAppearing);

                if (ImGui.IsWindowHovered() && ImGui.IsMouseReleased(ImGuiMouseButton.Left))
                    ToggleFreecam();

                ImGui.End();
            }
        }
         */

        public static void Dispose()
        {
            //if (freeCamera != null)
            //    ToggleFreecam();

            GetZoomDeltaHook?.Dispose();
            GetCameraTargetHook?.Dispose();
        }
    }
}
