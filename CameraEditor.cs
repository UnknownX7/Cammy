using System;
using System.Numerics;
using Dalamud.Hooking;
using Dalamud.Interface;
using ImGuiNET;

namespace Cammy
{
    public class CameraEditor : IDisposable
    {
        public class CameraPreset
        {
            public float CurrentZoom = 6f;
            public float MinZoom = 1.5f;
            public float MaxZoom = 20f;
            public float ZoomDelta = 0.75f;

            public float CurrentFoV = 0.78f;
            public float MinFoV = 0.69f;
            public float MaxFoV = 0.78f;
            public float FoVDelta = 0.08726646751f;
            public float AddedFoV = 0f;

            public float CurrentVRotation = -0.349066f;
            public float MinVRotation = -1.483530f;
            public float MaxVRotation = 0.785398f;

            public float CenterHeightOffset = 0f;
        }

        public IntPtr cameraManager = IntPtr.Zero;
        public Structures.GameCamera worldCamera;
        public Structures.GameCamera menuCamera;
        public Structures.GameCamera spectatorCamera;

        public Structures.GameCamera freeCamera;

        // This variable is merged with a lot of other constants so it's not possible to change normally
        private float zoomDelta = 0.75f;
        private delegate float GetZoomDeltaDelegate();
        private readonly Hook<GetZoomDeltaDelegate> GetZoomDeltaHook;
        private float GetZoomDeltaDetour() => zoomDelta;

        // Of course this isn't though
        private readonly IntPtr foVDeltaPtr;
        private unsafe ref float FoVDelta => ref *(float*)foVDeltaPtr; // 0.08726646751

        public readonly Memory.Replacer cameraNoCollideReplacer = new("E8 ?? ?? ?? ?? 45 0F 57 FF", new byte[] { 0x30, 0xC0, 0x90, 0x90, 0x90 }); // E8 ?? ?? ?? ?? 48 8B B4 24 E0 00 00 00 40 32 FF (0x90, 0x90, 0x90, 0x90, 0x90)

        private CameraPreset Defaults => Cammy.Config.CameraPreset;
        public bool editorVisible = false;

        public unsafe CameraEditor()
        {
            try
            {
                cameraManager = Cammy.Interface.TargetModuleScanner.GetStaticAddressFromSig("48 8D 35 ?? ?? ?? ?? 48 8B 34 C6 F3"); // g_ControlSystem_CameraManager
                worldCamera = new(*(IntPtr*)cameraManager);
                //unknownCamera = new(*(IntPtr*)(cameraManager + 0x8));
                menuCamera = new(*(IntPtr*)(cameraManager + 0x10));
                spectatorCamera = new(*(IntPtr*)(cameraManager + 0x18));

                var vtbl = (IntPtr*)*(IntPtr*)worldCamera.Address;
                GetZoomDeltaHook = new Hook<GetZoomDeltaDelegate>(*(vtbl + 27), new GetZoomDeltaDelegate(GetZoomDeltaDetour)); // Client__Game__Camera_vf27
                GetZoomDeltaHook.Enable();

                foVDeltaPtr = Cammy.Interface.TargetModuleScanner.GetStaticAddressFromSig("F3 0F 59 05 ?? ?? ?? ?? 0F 28 74 24 20 48 83 C4 30 5B C3 0F 57 C0 0F"); // F3 0F 59 05 ?? ?? ?? ?? 0F 28 74 24 20 48 83 C4 30 5B C3 0F 57 C0 0F 28 74 24 20 48 83 C4 30 5B C3

                if (Cammy.Config.AutoLoadCameraPreset && Cammy.Interface.ClientState.LocalPlayer != null)
                    LoadPreset(true);
            }
            catch { }
        }

        public void SavePreset()
        {
            Cammy.Config.CameraPreset = new CameraPreset
            {
                CurrentZoom = worldCamera.CurrentZoom,
                MinZoom = worldCamera.MinZoom,
                MaxZoom = worldCamera.MaxZoom,
                ZoomDelta = zoomDelta,

                CurrentFoV = worldCamera.CurrentFoV,
                MinFoV = worldCamera.MinFoV,
                MaxFoV = worldCamera.MaxFoV,
                FoVDelta = (foVDeltaPtr != IntPtr.Zero) ? FoVDelta : Defaults.FoVDelta,
                AddedFoV = worldCamera.AddedFoV,

                CurrentVRotation = worldCamera.CurrentVRotation,
                MinVRotation = worldCamera.MinVRotation,
                MaxVRotation = worldCamera.MaxVRotation,

                CenterHeightOffset = worldCamera.CenterHeightOffset
            };
            Cammy.Config.Save();
        }

        public void LoadPreset(bool init)
        {
            var preset = Defaults;
            if (!init)
                worldCamera.CurrentZoom = preset.CurrentZoom;
            worldCamera.MinZoom = preset.MinZoom;
            worldCamera.MaxZoom = preset.MaxZoom;
            zoomDelta = preset.ZoomDelta;

            if (!init)
                worldCamera.CurrentFoV = preset.CurrentFoV;
            worldCamera.MinFoV = preset.MinFoV;
            worldCamera.MaxFoV = preset.MaxFoV;
            if (foVDeltaPtr != IntPtr.Zero)
                FoVDelta = preset.FoVDelta;
            worldCamera.AddedFoV = preset.AddedFoV;

            //if (!init)
            //    CurrentVRotation = preset.CurrentVRotation;
            worldCamera.MinVRotation = preset.MinVRotation;
            worldCamera.MaxVRotation = preset.MaxVRotation;

            //CenterHeightOffset = preset.CenterHeightOffset;
        }

        public unsafe void ToggleFreecam()
        {
            var enable = freeCamera == null;
            if (enable)
            {
                freeCamera = menuCamera;
                *(byte*)(freeCamera.Address + 0x2A0) = 0;
                freeCamera.MinVRotation = -1.559f;
                freeCamera.MaxVRotation = 1.559f;
                freeCamera.CurrentFoV = freeCamera.MinFoV = freeCamera.MaxFoV = 0.78f;
                freeCamera.CurrentZoom = freeCamera.MinZoom = freeCamera.MaxZoom = freeCamera.AddedFoV = 0;
                cameraNoCollideReplacer.Enable();
            }
            else
            {
                freeCamera = null;
                cameraNoCollideReplacer.Disable();
            }

            static void ToggleAddonVisible(string name)
            {
                var addon = Cammy.Interface.Framework.Gui.GetUiObjectByName(name, 1);
                if (addon == IntPtr.Zero) return;

                *(byte*)(addon + Dalamud.Game.Internal.Gui.Structs.AddonOffsets.Flags) ^= 0x20;
            }

            ToggleAddonVisible("_TitleRights");
            ToggleAddonVisible("_TitleRevision");
            ToggleAddonVisible("_TitleMenu");
            ToggleAddonVisible("_TitleLogo");
        }

        public void OnLogin()
        {
            if (Cammy.Config.AutoLoadCameraPreset)
                LoadPreset(false);
        }

        public void OnLogout() { }

        public void Update()
        {
            if (freeCamera == null) return;

            var keyState = Cammy.Interface.ClientState.KeyState;

            if (keyState[27] || Cammy.Interface.Framework.Gui.GetUiObjectByName("Title", 1) == IntPtr.Zero) // Esc
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
            if (freeCamera == null && Cammy.Interface.Framework.Gui.GetUiObjectByName("Title", 1) != IntPtr.Zero)
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

            if (!editorVisible) return;

            var scale = ImGui.GetIO().FontGlobalScale;
            ImGui.SetNextWindowSize(new Vector2(550, 0) * scale);
            ImGui.Begin("Cammy Configuration", ref editorVisible, ImGuiWindowFlags.NoResize);

            static void ResetSliderFloat(string id, ref float val, float min, float max, float reset, float def, string format)
            {
                if (ImGui.Button($"Reset##{id}"))
                    val = reset;
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Right click to reset to game default.");
                    if (ImGui.IsMouseReleased(ImGuiMouseButton.Right))
                        val = def;
                }
                ImGui.SameLine();
                ImGui.SliderFloat(id, ref val, min, max, format);
            }

            ResetSliderFloat("Current Zoom", ref worldCamera.CurrentZoom, worldCamera.MinZoom, worldCamera.MaxZoom, Defaults.CurrentZoom, 6f, "%.2f");
            ResetSliderFloat("Minimum Zoom", ref worldCamera.MinZoom, 1f, worldCamera.MaxZoom, Defaults.MinZoom, 1.5f, "%.2f");
            ResetSliderFloat("Maximum Zoom", ref worldCamera.MaxZoom, worldCamera.MinZoom, 100f, Defaults.MaxZoom, 20f, "%.2f");
            if (GetZoomDeltaHook != null)
                ResetSliderFloat("Zoom Delta", ref zoomDelta, 0, 5f, Defaults.ZoomDelta, 0.75f, "%.2f");

            ImGui.Spacing();
            ImGui.Spacing();

            ResetSliderFloat("Current FoV", ref worldCamera.CurrentFoV, worldCamera.MinFoV, worldCamera.MaxFoV, Defaults.CurrentFoV, 0.78f, "%f");
            ResetSliderFloat("Minimum FoV", ref worldCamera.MinFoV, 0.01f, worldCamera.MaxFoV, Defaults.MinFoV, 0.69f, "%f");
            ResetSliderFloat("Maximum FoV", ref worldCamera.MaxFoV, worldCamera.MinFoV, 3f, Defaults.MaxFoV, 0.78f, "%f");
            if (foVDeltaPtr != IntPtr.Zero)
                ResetSliderFloat("FoV Delta", ref FoVDelta, 0, 0.5f, Defaults.FoVDelta, 0.08726646751f, "%f");
            ResetSliderFloat("Added FoV", ref worldCamera.AddedFoV, -1.56f, 2f, Defaults.AddedFoV, 0f, "%f"); // Slightly useless but that's ok
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("In some weather, the fov will cause lag or crash if the total is 3.14.");

            ImGui.Spacing();
            ImGui.Spacing();

            ResetSliderFloat("H Rotation", ref worldCamera.HRotation, (float)-Math.PI, (float)Math.PI, (float)Math.PI, (float)Math.PI, "%f");

            ImGui.Spacing();
            ImGui.Spacing();

            ResetSliderFloat("Current V Rotation", ref worldCamera.CurrentVRotation, worldCamera.MinVRotation, worldCamera.MaxVRotation, Defaults.CurrentVRotation, -0.349066f, "%f");
            ResetSliderFloat("Minimum V Rotation", ref worldCamera.MinVRotation, -1.569f, worldCamera.MaxVRotation, Defaults.MinVRotation, -1.483530f, "%f");
            ResetSliderFloat("Maximum V Rotation", ref worldCamera.MaxVRotation, worldCamera.MinVRotation, 1.569f, Defaults.MaxVRotation, 0.785398f, "%f");

            ImGui.Spacing();
            ImGui.Spacing();

            ResetSliderFloat("Tilt", ref worldCamera.Tilt, (float)-Math.PI, (float)Math.PI, 0f, 0f, "%f");

            ImGui.Spacing();
            ImGui.Spacing();

            ResetSliderFloat("Center Height Offset", ref worldCamera.CenterHeightOffset, -10f, 10f, Defaults.CenterHeightOffset, 0f, "%f");

            ImGui.Spacing();
            ImGui.Spacing();

            if (ImGui.Button($"Reset##???"))
                worldCamera.Mode = 1;
            ImGui.SameLine();
            ImGui.SliderInt("???", ref worldCamera.Mode, 0, 2);

            if (cameraNoCollideReplacer.IsValid)
            {
                ImGui.Spacing();
                var _ = cameraNoCollideReplacer.IsEnabled;
                if (ImGui.Checkbox("Disable Camera Collision", ref _))
                    cameraNoCollideReplacer.Toggle();
            }

            ImGui.Spacing();
            ImGui.Spacing();

            if (ImGui.Button("Save Defaults"))
                SavePreset();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Saves zoom, fov, and vertical rotation values.");
            ImGui.SameLine();
            if (ImGui.Button("Load Defaults"))
                LoadPreset(false);
            ImGui.SameLine();
            if (ImGui.Checkbox("Load saved settings automatically", ref Cammy.Config.AutoLoadCameraPreset))
                Cammy.Config.Save();

            ImGui.End();
        }

        public void Dispose()
        {
            if (freeCamera != null)
                ToggleFreecam();

            GetZoomDeltaHook?.Dispose();
        }
    }
}
