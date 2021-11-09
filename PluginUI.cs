using System;
using System.Numerics;
using Dalamud.Interface;
using ImGuiNET;

namespace Cammy
{
    public static class PluginUI
    {
        public static bool isVisible = false;

        private static int selectedPreset = -1;
        private static CameraConfigPreset CurrentPreset => 0 <= selectedPreset && selectedPreset < Cammy.Config.Presets.Count ? Cammy.Config.Presets[selectedPreset] : null;

        public static void Draw()
        {
            if (!FreeCam.Enabled && DalamudApi.GameGui.GetAddonByName("Title", 1) != IntPtr.Zero)
                DrawFreeCamButton();

            if (!isVisible) return;

            ImGui.SetNextWindowSizeConstraints(new Vector2(700, 600) * ImGuiHelpers.GlobalScale, new Vector2(9999));
            ImGui.Begin("Cammy Configuration", ref isVisible);

            if (ImGui.BeginTabBar("CammyTabs"))
            {
                if (ImGui.BeginTabItem("Presets"))
                {
                    DrawPresetList();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Other Settings"))
                {
                    DrawOtherSettings();
                    ImGui.EndTabItem();
                }

                ImGui.EndTabBar();
            }

            ImGui.End();
        }

        private static void DrawPresetList()
        {
            var currentPreset = CurrentPreset;
            var hasSelectedPreset = currentPreset != null;

            ImGui.PushFont(UiBuilder.IconFont);

            if (ImGui.Button(FontAwesomeIcon.PlusCircle.ToIconString()))
            {
                Cammy.Config.Presets.Add(new());
                Cammy.Config.Save();
            }

            ImGui.SameLine();

            if (ImGui.Button(FontAwesomeIcon.Copyright.ToIconString()) && hasSelectedPreset)
            {
                Cammy.Config.Presets.Add(CurrentPreset.Clone());
                Cammy.Config.Save();
            }

            ImGui.SameLine();

            if (ImGui.Button(FontAwesomeIcon.ArrowCircleUp.ToIconString()) && hasSelectedPreset)
            {
                var preset = CurrentPreset;
                Cammy.Config.Presets.RemoveAt(selectedPreset);

                selectedPreset = Math.Max(selectedPreset - 1, 0);

                Cammy.Config.Presets.Insert(selectedPreset, preset);
                Cammy.Config.Save();
            }

            ImGui.SameLine();

            if (ImGui.Button(FontAwesomeIcon.ArrowCircleDown.ToIconString()) && hasSelectedPreset)
            {
                var preset = CurrentPreset;
                Cammy.Config.Presets.RemoveAt(selectedPreset);

                selectedPreset = Math.Min(selectedPreset + 1, Cammy.Config.Presets.Count);

                Cammy.Config.Presets.Insert(selectedPreset, preset);
                Cammy.Config.Save();
            }

            ImGui.SameLine();

            ImGui.Button(FontAwesomeIcon.TimesCircle.ToIconString());
            if (hasSelectedPreset && ImGui.BeginPopupContextItem(null, ImGuiPopupFlags.MouseButtonLeft))
            {
                if (ImGui.Selectable(FontAwesomeIcon.TrashAlt.ToIconString()))
                {
                    Cammy.Config.Presets.RemoveAt(selectedPreset);
                    selectedPreset = Math.Min(selectedPreset, Cammy.Config.Presets.Count - 1);
                    currentPreset = CurrentPreset;
                    hasSelectedPreset = currentPreset != null;
                    Cammy.Config.Save();
                }
                ImGui.EndPopup();
            }

            ImGui.SameLine();

            ImGui.TextUnformatted(FontAwesomeIcon.InfoCircle.ToIconString());

            ImGui.PopFont();

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("You can CTRL + Left Click sliders to input values manually.");

            ImGui.BeginChild("CammyPresetList", new Vector2(250 * ImGuiHelpers.GlobalScale, 0), true);

            for (int i = 0; i < Cammy.Config.Presets.Count; i++)
            {
                var preset = Cammy.Config.Presets[i];

                ImGui.PushID(i);

                var isActive = preset == PresetManager.activePreset;
                var isOverride = preset == PresetManager.presetOverride;

                if (isActive)
                    ImGui.PushStyleColor(ImGuiCol.Text, !isOverride ? 0xFF00FF00 : 0xFFFFAF00);

                if (ImGui.Selectable(preset.Name, selectedPreset == i))
                    selectedPreset = i;

                if (isActive)
                    ImGui.PopStyleColor();

                if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                    PresetManager.SetPresetOverride(!isOverride ? preset : null);

                ImGui.PopID();
            }

            ImGui.EndChild();

            if (!hasSelectedPreset) return;

            ImGui.SameLine();
            ImGui.BeginChild("CammyPresetEditor", ImGui.GetContentRegionAvail(), true);
            DrawPresetEditor(currentPreset);
            ImGui.EndChild();
        }

        private static void ResetSliderFloat(string id, ref float val, float min, float max, float reset, string format)
        {
            var save = false;

            ImGui.PushFont(UiBuilder.IconFont);
            if (ImGui.Button($"{FontAwesomeIcon.UndoAlt.ToIconString()}##{id}"))
            {
                val = reset;
                save = true;
            }
            ImGui.PopFont();
            ImGui.SameLine();
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 150 * ImGuiHelpers.GlobalScale);
            save |= ImGui.SliderFloat(id, ref val, min, max, format);

            if (!save) return;
            Cammy.Config.Save();
            if (CurrentPreset == PresetManager.activePreset)
                CurrentPreset.Apply();
        }

        private static void ResetSliderFloat(string id, ref float val, float min, float max, Func<float> reset, string format)
        {
            var save = false;

            ImGui.PushFont(UiBuilder.IconFont);
            if (ImGui.Button($"{FontAwesomeIcon.UndoAlt.ToIconString()}##{id}"))
            {
                val = reset();
                save = true;
            }
            ImGui.PopFont();
            ImGui.SameLine();
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 150 * ImGuiHelpers.GlobalScale);
            save |= ImGui.SliderFloat(id, ref val, min, max, format);

            if (!save) return;
            Cammy.Config.Save();
            if (CurrentPreset == PresetManager.activePreset)
                CurrentPreset.Apply();
        }

        private static void DrawPresetEditor(CameraConfigPreset preset)
        {
            if (ImGui.InputText("Name", ref preset.Name, 64))
                Cammy.Config.Save();

            ImGui.Spacing();

            ImGui.Columns(3, null, false);
            if (ImGui.Checkbox("Starting Zoom##Use", ref preset.UseStartZoom))
                Cammy.Config.Save();
            ImGui.NextColumn();
            if (ImGui.Checkbox("Starting FoV##Use", ref preset.UseStartFoV))
                Cammy.Config.Save();
            if (preset.UseStartZoom || preset.UseStartFoV)
            {
                ImGui.NextColumn();
                if (ImGui.Checkbox("Only on Login", ref preset.UseStartOnLogin))
                    Cammy.Config.Save();
            }
            ImGui.Columns(1);

            ImGui.Spacing();

            if (preset.UseStartZoom)
                ResetSliderFloat("Starting Zoom", ref preset.StartZoom, preset.MinZoom, preset.MaxZoom, 6, "%.2f");
            ResetSliderFloat("Minimum Zoom", ref preset.MinZoom, 1, preset.MaxZoom, 1.5f, "%.2f");
            ResetSliderFloat("Maximum Zoom", ref preset.MaxZoom, preset.MinZoom, 100, 20, "%.2f");
            ResetSliderFloat("Zoom Delta", ref preset.ZoomDelta, 0, 5, 0.75f, "%.2f");

            ImGui.Spacing();

            if (preset.UseStartFoV)
                ResetSliderFloat("Starting FoV", ref preset.StartFoV, preset.MinFoV, preset.MaxFoV, 0.78f, "%f");
            ResetSliderFloat("Minimum FoV", ref preset.MinFoV, 0.01f, preset.MaxFoV, 0.69f, "%f");
            ResetSliderFloat("Maximum FoV", ref preset.MaxFoV, preset.MinFoV, 3, 0.78f, "%f");
            ResetSliderFloat("FoV Delta", ref preset.FoVDelta, 0, 0.5f, 0.08726646751f, "%f");
            ResetSliderFloat("Added FoV", ref preset.AddedFoV, -1.56f, 2, 0, "%f"); // Slightly useless but that's ok
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("In some weather, the FoV will cause lag or crash if the total is 3.14.");

            ImGui.Spacing();

            ResetSliderFloat("Minimum V Rotation", ref preset.MinVRotation, -1.569f, preset.MaxVRotation, -1.483530f, "%f");
            ResetSliderFloat("Maximum V Rotation", ref preset.MaxVRotation, preset.MinVRotation, 1.569f, 0.785398f, "%f");

            ImGui.Spacing();

            ResetSliderFloat("Camera Height Offset", ref preset.HeightOffset, -1, 1, 0, "%.2f");
            ResetSliderFloat("Tilt", ref preset.Tilt, (float)-Math.PI, (float)Math.PI, 0, "%f");
            ResetSliderFloat("Look at Height Offset", ref preset.LookAtHeightOffset, -10, 10, Game.GetDefaultLookAtHeightOffset, "%f");

            ImGui.Spacing();
            ImGui.Spacing();

            var qolBarEnabled = IPC.QoLBarEnabled;
            var conditionSets = qolBarEnabled ? IPC.QoLBarConditionSets : Array.Empty<string>();
            var display = preset.ConditionSet >= 0
                ? preset.ConditionSet < conditionSets.Length
                    ? $"[{preset.ConditionSet + 1}] {conditionSets[preset.ConditionSet]}"
                    : (preset.ConditionSet + 1).ToString()
                : "None";

            if (ImGui.BeginCombo("Condition Set", display))
            {
                if (ImGui.Selectable("None##ConditionSet", preset.ConditionSet < 0))
                {
                    preset.ConditionSet = -1;
                    Cammy.Config.Save();
                }

                if (qolBarEnabled)
                {
                    for (int i = 0; i < conditionSets.Length; i++)
                    {
                        var name = conditionSets[i];
                        if (!ImGui.Selectable($"[{i + 1}] {name}", i == preset.ConditionSet)) continue;
                        preset.ConditionSet = i;
                        Cammy.Config.Save();
                    }
                }

                ImGui.EndCombo();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Uses a QoL Bar Condition Set to automatically swap to this preset." +
                    "\nPresets higher in the list will have priority over lower ones." +
                    "\nCondition Sets should be made using the QoL Bar plugin config." +
                    "\nPlease see the \"Other Settings\" tab to verify if QoL Bar was detected.");
        }

        private static unsafe void DrawOtherSettings()
        {
            ImGui.TextUnformatted("QoL Bar Status:");
            if (!IPC.QoLBarEnabled)
            {
                ImGui.SameLine();
                ImGui.TextColored(new Vector4(1, 0, 0, 1), "Disabled");
                ImGui.SameLine();
                ImGui.PushFont(UiBuilder.IconFont);
                if (ImGui.SmallButton($"{FontAwesomeIcon.UndoAlt.ToIconString()}##CheckQoLBar"))
                {
                    IPC.Dispose();
                    IPC.Initialize();
                }
                ImGui.PopFont();
            }
            else
            {
                ImGui.SameLine();
                ImGui.TextColored(new Vector4(0, 1, 0, 1), "Enabled");
            }

            ImGui.Spacing();
            ImGui.Columns(3, null, false);

            {
                var _ = Game.GetCameraTargetHook.IsEnabled;
                if (ImGui.Checkbox("Spectate Focus / Soft Target", ref _))
                    Game.ToggleSpectate();
            }

            {
                ImGui.NextColumn();
                var _ = FreeCam.Enabled;
                if (ImGui.Checkbox("Free Cam", ref _))
                    FreeCam.Toggle();
            }

            ImGui.NextColumn();
            if (ImGui.Checkbox("Toggle Free Cam on Death/Revive", ref Cammy.Config.FreeCamOnDeath))
                Cammy.Config.Save();

            if (Game.cameraNoCollideReplacer.IsValid)
            {
                ImGui.NextColumn();
                var _ = Game.cameraNoCollideReplacer.IsEnabled;
                if (ImGui.Checkbox("Disable Camera Collision", ref _))
                    Game.cameraNoCollideReplacer.Toggle();
            }

            ImGui.Columns(1);
            ImGui.Spacing();

            {
                ImGui.PushFont(UiBuilder.IconFont);
                if (ImGui.Button($"{FontAwesomeIcon.UndoAlt.ToIconString()}##Reset???"))
                    Game.cameraManager->WorldCamera->Mode = 1;
                ImGui.PopFont();
                ImGui.SameLine();
                var _ = Game.cameraManager->WorldCamera->Mode;
                if (ImGui.SliderInt("???", ref _, 0, 2))
                    Game.cameraManager->WorldCamera->Mode = _;
            }
        }

        private static void DrawFreeCamButton()
        {
            ImGuiHelpers.ForceNextWindowMainViewport();
            var size = new Vector2(50) * ImGuiHelpers.GlobalScale;
            ImGui.SetNextWindowSize(size, ImGuiCond.Always);
            ImGuiHelpers.SetNextWindowPosRelativeMainViewport(new Vector2(ImGuiHelpers.MainViewport.Size.X - size.X, 0), ImGuiCond.Always);
            ImGui.Begin("FreeCam Button", ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoFocusOnAppearing);

            if (ImGui.IsWindowHovered() && ImGui.IsMouseReleased(ImGuiMouseButton.Left))
                FreeCam.Toggle();

            ImGui.End();
        }
    }
}
