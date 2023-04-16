using System;
using System.Numerics;
using Dalamud.Interface;
using ImGuiNET;

namespace Cammy;

public static class PluginUI
{
    private static bool isVisible = false;
    public static bool IsVisible
    {
        get => isVisible;
        set => isVisible = value;
    }

    private static int selectedPreset = -1;
    private static CameraConfigPreset CurrentPreset => 0 <= selectedPreset && selectedPreset < Cammy.Config.Presets.Count ? Cammy.Config.Presets[selectedPreset] : null;

    public static void Draw()
    {
        if (!isVisible) return;

        ImGui.SetNextWindowSizeConstraints(new Vector2(700, 685) * ImGuiHelpers.GlobalScale, new Vector2(9999));
        ImGui.Begin("Cammy Configuration", ref isVisible);
        ImGuiEx.AddDonationHeader();

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

        ImGuiEx.SetItemTooltip("You can CTRL + Left Click sliders to input values manually.");

        ImGui.BeginChild("CammyPresetList", new Vector2(250 * ImGuiHelpers.GlobalScale, 0), true);

        for (int i = 0; i < Cammy.Config.Presets.Count; i++)
        {
            var preset = Cammy.Config.Presets[i];

            ImGui.PushID(i);

            var isActive = preset == PresetManager.ActivePreset;
            var isOverride = preset == PresetManager.PresetOverride;

            if (isActive || isOverride)
                ImGui.PushStyleColor(ImGuiCol.Text, !isOverride ? 0xFF00FF00 : 0xFFFFAF00);

            if (ImGui.Selectable(preset.Name, selectedPreset == i))
                selectedPreset = i;

            if (isActive || isOverride)
                ImGui.PopStyleColor();

            if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                PresetManager.CurrentPreset = !isOverride ? preset : null;

            ImGui.PopID();
        }

        ImGui.EndChild();

        if (!hasSelectedPreset) return;

        ImGui.SameLine();
        ImGui.BeginChild("CammyPresetEditor", Vector2.Zero, true);
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
        if (CurrentPreset == PresetManager.CurrentPreset)
            CurrentPreset.Apply();
    }

    private static void AddSubtractAction(string id, float step, Action<float> action)
    {
        var save = false;

        ImGui.BeginGroup();
        ImGui.PushButtonRepeat(true);
        if (ImGui.ArrowButton($"##Subtract{id}", ImGuiDir.Down))
        {
            action(-step);
            save = true;
        }
        ImGui.SameLine();
        if (ImGui.ArrowButton($"##Add{id}", ImGuiDir.Up))
        {
            action(step);
            save = true;
        }
        ImGui.PopButtonRepeat();
        ImGui.SameLine();
        ImGui.TextUnformatted(id);
        ImGui.EndGroup();

        if (!save) return;
        Cammy.Config.Save();
        if (CurrentPreset == PresetManager.CurrentPreset)
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
        if (CurrentPreset == PresetManager.CurrentPreset)
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
        ImGui.Separator();
        ImGui.Spacing();

        var arrowOffset = ImGui.GetStyle().WindowPadding.X + ImGui.GetStyle().ItemSpacing.X + 25 * ImGuiHelpers.GlobalScale;
        ImGui.Spacing();
        ImGui.SameLine(arrowOffset);
        AddSubtractAction("Zoom", 0.1f, x =>
        {
            preset.StartZoom += x;
            preset.MinZoom += x;
            preset.MaxZoom += x;
        });

        if (preset.UseStartZoom)
            ResetSliderFloat("Starting##Zoom", ref preset.StartZoom, preset.MinZoom, preset.MaxZoom, 6, "%.2f");
        ResetSliderFloat("Minimum##Zoom", ref preset.MinZoom, 1, preset.MaxZoom, 1.5f, "%.2f");
        ResetSliderFloat("Maximum##Zoom", ref preset.MaxZoom, preset.MinZoom, 100, 20, "%.2f");
        ResetSliderFloat("Delta##Zoom", ref preset.ZoomDelta, 0, 5, 0.75f, "%.2f");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.Spacing();
        ImGui.SameLine(arrowOffset);
        AddSubtractAction("Field of View", 0.01f, x =>
        {
            preset.StartFoV += x;
            preset.MinFoV += x;
            preset.MaxFoV += x;
        });
        ImGuiEx.SetItemTooltip("In some weather, the FoV will cause lag or crash if the total is 3.14.");

        if (preset.UseStartFoV)
            ResetSliderFloat("Starting##FoV", ref preset.StartFoV, preset.MinFoV, preset.MaxFoV, 0.78f, "%f");
        ResetSliderFloat("Minimum##FoV", ref preset.MinFoV, 0.01f, preset.MaxFoV, 0.69f, "%f");
        ResetSliderFloat("Maximum##FoV", ref preset.MaxFoV, preset.MinFoV, 3, 0.78f, "%f");
        ResetSliderFloat("Delta##FoV", ref preset.FoVDelta, 0, 0.5f, 0.08726646751f, "%f");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ResetSliderFloat("Minimum V Rotation", ref preset.MinVRotation, -1.569f, preset.MaxVRotation, -1.483530f, "%f");
        ResetSliderFloat("Maximum V Rotation", ref preset.MaxVRotation, preset.MinVRotation, 1.569f, 0.785398f, "%f");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ResetSliderFloat("Camera Height Offset", ref preset.HeightOffset, -1, 1, 0, "%.2f");
        ResetSliderFloat("Camera Side Offset", ref preset.SideOffset, -1, 1, 0, "%.2f");
        //ResetSliderFloat("Tilt", ref preset.Tilt, -MathF.PI, MathF.PI, 0, "%f");
        ResetSliderFloat("Look at Height Offset", ref preset.LookAtHeightOffset, -10, 10, () => Game.GetDefaultLookAtHeightOffset() ?? 0, "%f");

        if (ImGuiEx.EnumCombo("View Bobbing", ref preset.ViewBobMode))
            Cammy.Config.Save();

        ImGui.Spacing();
        ImGui.Separator();
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

        ImGuiEx.SetItemTooltip("Uses a QoL Bar Condition Set to automatically swap to this preset." +
            "\nPresets higher in the list will have priority over lower ones." +
            "\nCondition Sets should be made using the QoL Bar plugin config." +
            "\nPlease see the \"Other Settings\" tab to verify if QoL Bar was detected.");
    }

    private static void DrawOtherSettings()
    {
        var save = false;

        if (ImGuiEx.BeginGroupBox("Miscellaneous Options", 0.5f))
        {
            if (Game.cameraNoClippyReplacer.IsValid)
            {
                if (ImGui.Checkbox("Disable Camera Collision", ref Cammy.Config.EnableCameraNoClippy))
                {
                    if (!FreeCam.Enabled)
                        Game.cameraNoClippyReplacer.Toggle();
                    save = true;
                }
            }

            ImGui.TextUnformatted("Death Cam Mode");
            ImGuiEx.Prefix(true);
            save |= ImGuiEx.EnumCombo("##DeathCam", ref Cammy.Config.DeathCamMode);

            ImGuiEx.EndGroupBox();
        }

        ImGui.SameLine();

        if (ImGuiEx.BeginGroupBox("Other", 0.5f))
        {
            ImGui.TextUnformatted("QoL Bar Status:");
            ImGui.SameLine();
            if (!IPC.QoLBarEnabled)
                ImGui.TextColored(new Vector4(1, 0, 0, 1), "Disabled");
            else
                ImGui.TextColored(new Vector4(0, 1, 0, 1), "Enabled");

            var _ = Game.EnableSpectating;
            if (ImGui.Checkbox("Spectate Focus / Soft Target", ref _))
                Game.EnableSpectating = _;

            var __ = FreeCam.Enabled;
            if (ImGui.Checkbox("Free Cam", ref __))
                FreeCam.Toggle();
            ImGuiEx.SetItemTooltip(FreeCam.ControlsString);

            ImGuiEx.EndGroupBox();
        }


        if (save)
            Cammy.Config.Save();
    }
}