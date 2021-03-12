using System;
using System.Numerics;
using ImGuiNET;

namespace Cammy
{
    public class CameraEditor
    {
        public class CameraPreset
        {
            public float CurrentZoom = 6f;
            public float MinZoom = 1.5f;
            public float MaxZoom = 20f;
            public float ScrollSpeed = 1f;

            public float CurrentFoV = 0.78f;
            public float MinFoV = 0.69f;
            public float MaxFoV = 0.78f;

            public float CurrentVRotation = -0.349066f;
            public float MinVRotation = -1.483530f;
            public float MaxVRotation = 0.785398f;

            public float CenterHeightOffset = 0f;
        }

        private readonly IntPtr baseAddr;
        public IntPtr this[int k] => baseAddr + k;

        private unsafe ref float CurrentZoom => ref *(float*)(baseAddr + 0x114); // 6
        private unsafe ref float MinZoom => ref *(float*)(baseAddr + 0x118); // 1.5
        private unsafe ref float MaxZoom => ref *(float*)(baseAddr + 0x11C); // 20
        private unsafe ref float CurrentFoV => ref *(float*)(baseAddr + 0x120); // 0.78
        private unsafe ref float MinFoV => ref *(float*)(baseAddr + 0x124); // 0.69
        private unsafe ref float MaxFoV => ref *(float*)(baseAddr + 0x128); // 0.78
        private unsafe ref float AdditionalFoV => ref *(float*)(baseAddr + 0x12C); // 0
        private unsafe ref float HRotation => ref *(float*)(baseAddr + 0x130); // -pi -> pi, default is pi
        private unsafe ref float CurrentVRotation => ref *(float*)(baseAddr + 0x134); // -0.349066
        private unsafe ref float MinVRotation => ref *(float*)(baseAddr + 0x148); // -1.483530, should be -+pi/2 for straight down/up but camera breaks so use -+1.569
        private unsafe ref float MaxVRotation => ref *(float*)(baseAddr + 0x14C); // 0.785398 (pi/4)
        private unsafe ref float Tilt => ref *(float*)(baseAddr + 0x160);
        private unsafe ref int Mode => ref *(int*)(baseAddr + 0x170); // camera mode??? (0 = 1st person, 1 = 3rd person, 2+ = weird controller mode? cant look up/down)
        private unsafe ref float CenterHeightOffset => ref *(float*)(baseAddr + 0x218);

        private CameraPreset Defaults => Cammy.Config.CameraPreset;
        public bool editorVisible = false;
        private bool zoomInitialized = false;
        private float scrollSpeed = 1f;
        private float prevZoom = 0f;

        public unsafe CameraEditor()
        {
            // 48 8D 35 ?? ?? ?? ?? 48 8B 34 C6 F3 44 0F 10 86 90 00 00 00
            var structPtr = Cammy.Interface.TargetModuleScanner.GetStaticAddressFromSig("48 8D 35 ?? ?? ?? ?? 48 8B 34 C6 F3");
            baseAddr = *(IntPtr*)structPtr;

            if (Cammy.Config.AutoLoadCameraPreset && Cammy.Interface.ClientState.LocalPlayer != null)
                LoadPreset(true);
        }

        public void SavePreset()
        {
            Cammy.Config.CameraPreset = new CameraPreset
            {
                CurrentZoom = CurrentZoom,
                MinZoom = MinZoom,
                MaxZoom = MaxZoom,
                ScrollSpeed = scrollSpeed,

                CurrentFoV = CurrentFoV,
                MinFoV = MinFoV,
                MaxFoV = MaxFoV,

                CurrentVRotation = CurrentVRotation,
                MinVRotation = MinVRotation,
                MaxVRotation = MaxVRotation,

                CenterHeightOffset = CenterHeightOffset
            };
            Cammy.Config.Save();
        }

        public void LoadPreset(bool init)
        {
            var preset = Defaults;
            if (!init)
                CurrentZoom = preset.CurrentZoom;
            MinZoom = preset.MinZoom;
            MaxZoom = preset.MaxZoom;
            scrollSpeed = preset.ScrollSpeed;

            if (!init)
                CurrentFoV = preset.CurrentFoV;
            MinFoV = preset.MinFoV;
            MaxFoV = preset.MaxFoV;

            //if (!init)
            //    CurrentVRotation = preset.CurrentVRotation;
            MinVRotation = preset.MinVRotation;
            MaxVRotation = preset.MaxVRotation;

            //CenterHeightOffset = preset.CenterHeightOffset;

            zoomInitialized = false;
        }

        public void OnLogin()
        {
            if (Cammy.Config.AutoLoadCameraPreset)
                LoadPreset(false);
        }

        public void OnLogout() => zoomInitialized = false;

        public void Draw()
        {
            if (editorVisible)
            {
                var scale = ImGui.GetIO().FontGlobalScale;
                ImGui.SetNextWindowSize(new Vector2(550, 0) * scale);
                ImGui.Begin("Camera Editor", ref editorVisible, ImGuiWindowFlags.NoResize);

                void ResetSliderFloat(string id, ref float val, float min, float max, float reset, float def, string format)
                {
                    if (ImGui.Button($"Reset##{id}"))
                    {
                        val = reset;
                        zoomInitialized = false;
                    }
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip("Right click to reset to game default.");
                        if (ImGui.IsMouseReleased(ImGuiMouseButton.Right))
                        {
                            val = def;
                            zoomInitialized = false;
                        }
                    }
                    ImGui.SameLine();
                    if (ImGui.SliderFloat(id, ref val, min, max, format))
                        zoomInitialized = false;
                }

                ResetSliderFloat("Current Zoom", ref CurrentZoom, MinZoom, MaxZoom, Defaults.CurrentZoom, 6f, "%.2f");
                ResetSliderFloat("Minimum Zoom", ref MinZoom, 1.5f, MaxZoom, Defaults.MinZoom, 1.5f, "%.2f");
                ResetSliderFloat("Maximum Zoom", ref MaxZoom, MinZoom, 100f, Defaults.MaxZoom, 20f, "%.2f");
                ResetSliderFloat("Scroll Speed", ref scrollSpeed, 0, 5f, Defaults.ScrollSpeed, 1f, "%.1f");
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Custom logic, not actually modifying memory so probably a bit buggy.");

                ImGui.Spacing();
                ImGui.Spacing();

                if (ImGui.Checkbox("Fix FoV \"Bug\"", ref Cammy.Config.FixFoVBug))
                    Cammy.Config.Save();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Currently, the game uses 0.69 -> 0.78 as the default FoV range,\n" +
                        "but scrolling when zoomed in only lowers this from 0.78 -> 0.692734.\n" +
                        "This option will make the default minimum FoV 0.692734 instead,\n" +
                        "so that there is no \"dead scroll\" when zoom swaps from distance to FoV\n" +
                        "or when FoV swaps to first person.");

                ResetSliderFloat("Current FoV", ref CurrentFoV, MinFoV, MaxFoV, Defaults.CurrentFoV, 0.78f, "%f");
                ResetSliderFloat("Minimum FoV", ref MinFoV, 0.01f, MaxFoV, Defaults.MinFoV, Cammy.Config.FixFoVBug ? 0.692734f : 0.69f, "%f");
                ResetSliderFloat("Maximum FoV", ref MaxFoV, MinFoV, 3f, Defaults.MaxFoV, 0.78f, "%f");
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Do not set above 3 unless you enjoy freezing the game and breaking the UI.");
                ResetSliderFloat("Added FoV (unsaved)", ref AdditionalFoV, 0f, 3f, 0f, 0f, "%f"); // Slightly useless but that's ok

                ImGui.Spacing();
                ImGui.Spacing();

                ResetSliderFloat("H Rotation", ref HRotation, (float)-Math.PI, (float)Math.PI, (float)Math.PI, (float)Math.PI, "%f");

                ImGui.Spacing();
                ImGui.Spacing();

                ResetSliderFloat("Current V Rotation", ref CurrentVRotation, MinVRotation, MaxVRotation, Defaults.CurrentVRotation, -0.349066f, "%f");
                ResetSliderFloat("Minimum V Rotation", ref MinVRotation, -1.569f, MaxVRotation, Defaults.MinVRotation, -1.483530f, "%f");
                ResetSliderFloat("Maximum V Rotation", ref MaxVRotation, MinVRotation, 1.569f, Defaults.MaxVRotation, 0.785398f, "%f");

                ImGui.Spacing();
                ImGui.Spacing();

                ResetSliderFloat("Tilt", ref Tilt, (float)-Math.PI, (float)Math.PI, 0f, 0f, "%f");

                ImGui.Spacing();
                ImGui.Spacing();

                ResetSliderFloat("Center Height Offset", ref CenterHeightOffset, -10f, 10f, Defaults.CenterHeightOffset, 0f, "%f");

                ImGui.Spacing();
                ImGui.Spacing();

                if (ImGui.Button($"Reset##???"))
                    Mode = 1;
                ImGui.SameLine();
                ImGui.SliderInt("???", ref Mode, 0, 2);

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

            if (Cammy.Interface.ClientState.LocalPlayer == null)
                zoomInitialized = false;

            var delta = CurrentZoom - prevZoom;
            if (delta != 0 && zoomInitialized && scrollSpeed != 1)
                CurrentZoom = Math.Min(Math.Max(CurrentZoom + delta * (scrollSpeed - 1f), MinZoom), MaxZoom);

            prevZoom = CurrentZoom;
            zoomInitialized = true;
        }

        // 0x0 static ptr to something (a camera?) (possibly world camera)
        // 0x8 ??? 0 (might be another camera ptr)
        // 0x10 static ptr to something (a camera?) (gpose?)
        // 0x18 ??? 0 (might be another camera ptr)
        // 0x20 ??? 0
        // 0x28 ??? 0
        // 0x30 ptr to 0x10?
        // 0x38 ptr to 0x10?
        // 0x40 ??? 0
        // 0x48 ??? 3?
        // 0x4C ??? 0
        // 0x50 ??? 0
        // 0x54 ??? 0
        // 0x58 ??? 0
        // 0x5C ??? 0
        // 0x60 camera pos? cant change
        // 0x64 camera pos? cant change
        // 0x68 camera pos? cant change
        // 0x6C ??? no apparent effect
        // 0x70 ??? no apparent effect
        // 0x74 ??? no apparent effect
        // 0x78 ??? no apparent effect
        // 0x7C ??? 1? no apparent effect
        // 0x80 ??? 1? no apparent effect
        // 0x84 ??? 1? no apparent effect
        // 0x88 ??? 1? no apparent effect
        // 0x8C ??? no apparent effect
        // 0x90 camera pos? cant change
        // 0x94 camera pos? cant change
        // 0x98 camera pos? cant change
        // 0x9C ??? no apparent effect
        // 0xA0 camera angle? cant change
        // 0xA4 camera angle? cant change
        // 0xA8 camera angle? cant change
        // 0xAC ??? no apparent effect
        // 0xB0 seems to be camera angle??? to determine whether to show name tags
        // 0xB4 seems to be camera angle??? to determine whether to show name tags
        // 0xB8 seems to be camera angle??? to determine whether to show name tags
        // 0xBC ??? no apparent effect
        // 0xC0 ??? randomly changes when moving camera, cant change
        // 0xC4 copy of horizontal angle? cant change
        // 0xC8 copy of vertical angle? cant change
        // 0xCC ??? no apparent effect
        // 0xD0 seems to be camera angle??? to determine whether to show name tags
        // 0xD4 seems to be camera angle??? to determine whether to show name tags
        // 0xD8 seems to be camera angle??? to determine whether to show name tags
        // 0xDC ??? no apparent effect
        // 0xE0 seems to be camera position??? to determine whether to show name tags
        // 0xE4 seems to be camera position??? to determine whether to show name tags
        // 0xE8 seems to be camera position??? to determine whether to show name tags
        // 0xEC ??? no apparent effect
        // 0xF0 pointer to something
        // 0xF8 ??? no apparent effect, cant change from 0?
        // 0xFC ??? possible bitset, changing to even numbers completely kills sound and makes the entire screen blue, changing back seems to load things again, but not restore sound, 3 is default?
        // 0x100 ??? randomly changing value, cant change
        // 0x104 ??? no apparent effect
        // 0x108 ??? bitset, bit 0b10 is set when moving the camera with a mouse, was 3, no idea what first bit is
        // 0x10C ??? no apparent effect
        // 0x110 ??? changes from 4 -> 2 when hitting the camera on an object, and sometimes 1 if hit fast enough

        // 0x138 ??? delta?
        // 0x13C ??? delta?
        // 0x140 ??? delta?
        // 0x144 ??? delta?

        // 0x150 camera tilt left/right (gpose only)
        // 0x154 camera tilt up/down (gpose only)
        // 0x158 min of 0x154
        // 0x15C max of 0x154

        // 0x164 ??? no apparent effect
        // 0x168 ??? no apparent effect, resets to 0 instantly
        // 0x16C ??? no apparent effect, resets to 0 instantly

        // 0x174 ??? resets instantly (0 in 1st person, 2 in 3rd, 1 when camera mode 2+)
        // 0x178 same as previous
        // 0x17C lerp value for zoom
        // 0x180 max fov again?
        // 0x184 min of something?
        // 0x188 min zoom again?
        // 0x18C bool? 1 when moving
        // 0x190 lerp for 1st <-> 3rd?
        // 0x194 lerp value for fov
        // 0x198 ??? no apparent effect
        // 0x19C ??? no apparent effect

        // movement lerps? cant change
        // 0x1A0
        // 0x1A4
        // 0x1A8
        // 0x1AC 0?
        // 0x1B0
        // 0x1B4
        // 0x1B8
        // 0x1BC 0?
        // 0x1C0
        // 0x1C4
        // 0x1C8

        // 0x1CC ??? no apparent effect
        // 0x1D0 ??? no apparent effect
        // 0x1D4 ??? random float, cant change
        // 0x1D8 ??? no apparent effect
        // 0x1DC ??? no apparent effect, resets to 0 instantly
        // 0x1E0 ??? no apparent effect, resets to 0 instantly
        // 0x1E4 bitset? 0000 0001 0000 0000 0000 0000 0000 0000 (16777216) when turning camera with left/right click
        // 0x1E8 ??? no apparent effect, resets to 0.3 instantly
        // 0x1EC ??? no apparent effect
        // 0x1F0 copy of 0x1B0
        // 0x1F4 copy of 0x1B4
        // 0x1F8 copy of 0x1B8
        // 0x1FC ??? no apparent effect

        // seems to deal with keeping the camera out of objects
        // 0x200 1 by default, not sure what it does
        // 0x204 1 if camera is hitting an object
        // 0x208 max distance camera can be due to object
        // 0x20C same as prev, but has no effect when changed

        // 0x210 seems to be camera height lerp?
        // 0x214 ??? no apparent effect

        // 0x21C ??? no apparent effect
        // 0x220 bitset? resets to 21201
        // 0x224 camera character height lerp

        // seems to be the end
        // 0x228
        // 0x22C
        // 0x230
        // 0x234
        // 0x238
        // 0x23C
        // 0x240
        // 0x244
        // 0x248
        // 0x24C
        // 0x250
        // 0x254
        // 0x258
        // 0x25C

        // some weird object
        // 0x260 0?
        // 0x264 min zoom? does nothing but changes back when going 1st -> 3rd person
        // 0x268 ??? no apparent effect
        // 0x26C ??? no apparent effect, starts off as 1
        // 0x270 weird array of numbers 161-168 to the end
        // 0x274
        // 0x278
        // 0x27C
        // 0x280
        // 0x284
        // 0x288
        // 0x28C
        // 0x290

        // 0s for a bit

        // No longer seems to be camera related, who knows what this is
        // 0x2A8
    }
}
