using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface.Internal.Notifications;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Hypostasis.Game.Structures;
using ImGuiNET;

namespace Cammy;

public static unsafe class FreeCam
{
    public const string ControlsString = "Additional Controls:" +
        //"\nMove Keybinds - Move," +
        //"\nJump / Ascend - Up," +
        //"\nDescend - Down," +
        "\nShift (Hold) - Speed up" +
        "\nZoom / Controller Zoom (Autorun + Look Up / Down) - Change Speed" +
        "\nCycle through Enemies (Nearest to Farthest) / Controller Select HUD - Lock" +
        "\nCycle through Enemies (Farthest to Nearest) / Controller Open Main Menu - Stop";

    public static bool Enabled => gameCamera != null;
    public static Vector3 Position => position;

    private static GameCamera* gameCamera;
    private static bool locked = false;
    private static float speed = 1;
    private static Vector3 position;
    private static bool onDeath = false;
    private static bool onDeathActivated = false;
    private static float prevZoom = 0;
    private static float prevFoV = 0;
    private static bool displayedControls = false;

    private enum FreeCamBindings
    {
        Forward,
        Backward,
        Left,
        Left2,
        Right,
        Right2,
        Ascend,
        Ascend2,
        Descend,
        ToggleLock,
        EndFreeCam,
        ControllerAscend,
        ControllerDescend,
        ControllerToggleLock,
        ControllerAdjustSpeedModifier,
        ControllerEndFreeCam
    }

    private static readonly Dictionary<FreeCamBindings, uint> keybindings = new()
    {
        [FreeCamBindings.Forward] = 321, // Move Forward
        [FreeCamBindings.Backward] = 322, // Move Back
        [FreeCamBindings.Left] = 323,  // Move
        [FreeCamBindings.Left2] = 325, // Strafe Left
        [FreeCamBindings.Right] = 324, // Move
        [FreeCamBindings.Right2] = 326, // Strafe Right
        [FreeCamBindings.Ascend] = 348, // Jump
        [FreeCamBindings.Ascend2] = 444, // Ascend
        [FreeCamBindings.Descend] = 443, // Descent
        [FreeCamBindings.ToggleLock] = 366, // Cycle through Enemies (Nearest to Farthest)
        [FreeCamBindings.EndFreeCam] = 367, // Cycle through Enemies (Farthest to Nearest)
        [FreeCamBindings.ControllerAscend] = 5, // Controller Jump
        [FreeCamBindings.ControllerDescend] = 2, // Controller Cancel
        [FreeCamBindings.ControllerAdjustSpeedModifier] = 17, // Controller Autorun
        [FreeCamBindings.ControllerToggleLock] = 433, // Controller Select HUD
        [FreeCamBindings.ControllerEndFreeCam] = 35 // Controller Open Main Menu
    };

    private static readonly CameraConfigPreset freeCamPreset = new()
    {
        MinVRotation = -1.559f,
        MaxVRotation = 1.559f,
        MinZoom = 0.06f,
        MaxZoom = 0.06f,
        UseStartZoom = true,
        StartZoom = 0.06f,
        ZoomDelta = 0
    };

    public static void Toggle(bool death = false)
    {
        var enable = !Enabled;
        var isMainMenu = !DalamudApi.Condition.Any();
        if (enable)
        {
            EnableInputBlockers();

            gameCamera = isMainMenu ? Common.CameraManager->menuCamera : Common.CameraManager->worldCamera;

            locked = false;
            speed = 1;
            position = new(gameCamera->viewX, gameCamera->viewY, gameCamera->viewZ);
            onDeath = death;
            prevZoom = gameCamera->currentZoom;
            prevFoV = gameCamera->currentFoV;

            freeCamPreset.MinFoV = freeCamPreset.MaxFoV = gameCamera->currentFoV;
            freeCamPreset.Apply();
            gameCamera->mode = 1;
            Game.cameraNoClippyReplacer.Enable();

            if (!isMainMenu)
            {
                Game.ForceDisableMovement++;

                if (!death && !displayedControls)
                {
                    Cammy.ShowNotification(ControlsString, NotificationType.Info, 10_000);
                    displayedControls = true;
                }
            }
            else
            {
                gameCamera->lockPosition = 0;
            }
        }
        else
        {
            DisableInputBlockers();

            if (!isMainMenu)
            {
                if (!locked && Game.ForceDisableMovement > 0)
                    Game.ForceDisableMovement--;
                PresetManager.DefaultPreset.Apply();
                PresetManager.DisableCameraPresets();
            }

            gameCamera->currentZoom = gameCamera->interpolatedZoom = prevZoom;
            gameCamera->currentFoV = prevFoV;
            gameCamera = null;
            if (!Cammy.Config.EnableCameraNoClippy)
                Game.cameraNoClippyReplacer.Disable();
        }

        if (!isMainMenu) return;

        static void ToggleAddonVisible(string name)
        {
            var addon = DalamudApi.GameGui.GetAddonByName(name, 1);
            if (addon == nint.Zero) return;
            ((AtkUnitBase*)addon)->IsVisible ^= true;
        }

        ToggleAddonVisible("_TitleRights");
        ToggleAddonVisible("_TitleRevision");
        ToggleAddonVisible("_TitleMenu");
        ToggleAddonVisible("_TitleLogo");
    }

    public static void CheckDeath()
    {
        var dead = DalamudApi.Condition[ConditionFlag.Unconscious];
        if (onDeathActivated)
        {
            onDeathActivated = dead;
            if (!onDeathActivated && onDeath && Enabled)
                Toggle(true);
            return;
        }

        if (!dead) return;

        if (!Enabled)
            Toggle(true);
        onDeathActivated = true;
    }

    public static void Update()
    {
        if (Cammy.Config.DeathCamMode == 2)
            CheckDeath();

        if (!Enabled) return;

        if (InputData.isInputIDPressed.Original(Common.InputData, keybindings[FreeCamBindings.ToggleLock]) || InputData.isInputIDReleased.Original(Common.InputData, keybindings[FreeCamBindings.ControllerToggleLock]))
        {
            locked ^= true;
            if (locked && Game.ForceDisableMovement > 0)
                Game.ForceDisableMovement--;
            else
                Game.ForceDisableMovement++;

            if (locked)
                DisableInputBlockers();
            else
                EnableInputBlockers();
        }

        var loggedIn = DalamudApi.ClientState.IsLoggedIn;

        if (InputData.isInputIDPressed.Original(Common.InputData, keybindings[FreeCamBindings.EndFreeCam]) || InputData.isInputIDPressed.Original(Common.InputData, keybindings[FreeCamBindings.ControllerEndFreeCam]) || (loggedIn ? !locked && Game.ForceDisableMovement == 0 : DalamudApi.GameGui.GetAddonByName("Title") == nint.Zero))
        {
            Toggle();
            return;
        }

        if (locked) return;

        var movePos = Vector3.Zero;

        var analogInputX = InputData.getAxisInput.Original(Common.InputData, 4) / 100f; // Controller Move Forward / Back
        if (analogInputX != 0)
            movePos.X = analogInputX;

        var analogInputZ = InputData.getAxisInput.Original(Common.InputData, 3) / 100f; // Controller Move Left / Right
        if (analogInputZ != 0)
            movePos.Z = -analogInputZ;

        if (InputData.isInputIDHeld.Original(Common.InputData, keybindings[FreeCamBindings.Forward]) || InputData.isInputIDHeld.Original(Common.InputData, 36) && InputData.isInputIDHeld.Original(Common.InputData, 37)) // Left + Right Click
            movePos.X += 1;

        if (InputData.isInputIDHeld.Original(Common.InputData, keybindings[FreeCamBindings.Backward]))
            movePos.X -= 1;

        if (InputData.isInputIDHeld.Original(Common.InputData, keybindings[FreeCamBindings.Left]) || InputData.isInputIDHeld.Original(Common.InputData, keybindings[FreeCamBindings.Left2]))
            movePos.Z += 1;

        if (InputData.isInputIDHeld.Original(Common.InputData, keybindings[FreeCamBindings.Right]) || InputData.isInputIDHeld.Original(Common.InputData, keybindings[FreeCamBindings.Right2]))
            movePos.Z -= 1;

        if (InputData.isInputIDHeld.Original(Common.InputData, keybindings[FreeCamBindings.Ascend]) || InputData.isInputIDHeld.Original(Common.InputData, keybindings[FreeCamBindings.Ascend2]) || InputData.isInputIDHeld.Original(Common.InputData, keybindings[FreeCamBindings.ControllerAscend]))
            movePos.Y += 1;

        if (InputData.isInputIDHeld.Original(Common.InputData, keybindings[FreeCamBindings.Descend]) || InputData.isInputIDHeld.Original(Common.InputData, keybindings[FreeCamBindings.ControllerDescend]))
            movePos.Y -= 1;

        var mouseWheelStatus = InputData.GetMouseWheelStatus();
        if (mouseWheelStatus != 0)
            speed *= 1 + 0.2f * mouseWheelStatus;

        if (InputData.isInputIDHeld.Original(Common.InputData, keybindings[FreeCamBindings.ControllerAdjustSpeedModifier]))
        {
            switch (InputData.getAxisInput.Original(Common.InputData, 6) / 100f) // Controller Move Camera Up / Down
            {
                case >= 0.6f:
                    speed *= 1 + 1.5f * (float)DalamudApi.Framework.UpdateDelta.TotalSeconds;
                    break;
                case <= -0.6f:
                    speed *= 1 - 1.5f * (float)DalamudApi.Framework.UpdateDelta.TotalSeconds;
                    break;
            }
        }

        if (movePos == Vector3.Zero) return;

        movePos *= (float)(DalamudApi.Framework.UpdateDelta.TotalSeconds * 20) * speed;

        if (ImGui.GetIO().KeyShift) // Shift
            movePos *= 10;
        const float halfPI = MathF.PI / 2f;
        var hAngle = gameCamera->currentHRotation + halfPI;
        var vAngle = gameCamera->currentVRotation;
        var direction = new Vector3(MathF.Cos(hAngle) * MathF.Cos(vAngle), MathF.Sin(vAngle), -(MathF.Sin(hAngle) * MathF.Cos(vAngle)));

        var amount = direction * movePos.X;
        var x = amount.X + movePos.Z * MathF.Sin(gameCamera->currentHRotation - halfPI);
        var y = amount.Y + movePos.Y;
        var z = amount.Z + movePos.Z * MathF.Cos(gameCamera->currentHRotation - halfPI);

        if (loggedIn)
        {
            position.X += x;
            position.Y += y;
            position.Z += z;
        }
        else
        {
            gameCamera->lookAtX += x;
            gameCamera->lookAtY = gameCamera->lookAtY2 += y;
            gameCamera->lookAtZ += z;
        }
    }

    private static void EnableInputBlockers()
    {
        if (!InputData.isInputIDHeld.IsHooked)
            InputData.isInputIDHeld.CreateHook(IsInputIDHeldDetour);
        InputData.isInputIDHeld.Hook.Enable();

        if (!InputData.isInputIDPressed.IsHooked)
            InputData.isInputIDPressed.CreateHook(IsInputIDPressedDetour);
        InputData.isInputIDPressed.Hook.Enable();

        if (!InputData.isInputIDLongPressed.IsHooked)
            InputData.isInputIDLongPressed.CreateHook(IsInputIDLongPressedDetour);
        InputData.isInputIDLongPressed.Hook.Enable();

        if (!InputData.isInputIDReleased.IsHooked)
            InputData.isInputIDReleased.CreateHook(IsInputIDReleasedDetour);
        InputData.isInputIDReleased.Hook.Enable();

        if (!InputData.getAxisInput.IsHooked)
            InputData.getAxisInput.CreateHook(GetAxisInputDetour);
        InputData.getAxisInput.Hook.Enable();
    }

    private static void DisableInputBlockers()
    {
        InputData.isInputIDHeld.Hook.Disable();
        InputData.isInputIDPressed.Hook.Disable();
        InputData.isInputIDLongPressed.Hook.Disable();
        InputData.isInputIDReleased.Hook.Disable();
        InputData.getAxisInput.Hook.Disable();
    }

    // Obnoxious
    private static Bool IsInputIDHeldDetour(InputData* inputData, uint inputID) => !keybindings.ContainsValue(inputID) && InputData.isInputIDHeld.Original(inputData, inputID);
    private static Bool IsInputIDPressedDetour(InputData* inputData, uint inputID) => !keybindings.ContainsValue(inputID) && InputData.isInputIDPressed.Original(inputData, inputID);
    private static Bool IsInputIDLongPressedDetour(InputData* inputData, uint inputID) => !keybindings.ContainsValue(inputID) && InputData.isInputIDLongPressed.Original(inputData, inputID);
    private static Bool IsInputIDReleasedDetour(InputData* inputData, uint inputID) => !keybindings.ContainsValue(inputID) && InputData.isInputIDReleased.Original(inputData, inputID);
    private static int GetAxisInputDetour(InputData* inputData, uint inputID) => inputID is not (3 or 4 or 6) ? InputData.getAxisInput.Original(inputData, inputID) : 0;
}
