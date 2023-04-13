using System;
using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Hypostasis.Game.Structures;
using ImGuiNET;

namespace Cammy;

public static unsafe class FreeCam
{
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
            Game.cameraNoCollideReplacer.Enable();

            if (!isMainMenu)
            {
                Game.ForceDisableMovement++;

                if (!death)
                {
                    Cammy.PrintEcho("Additional Controls:" +
                        //"\nMove Keybinds - Move," +
                        //"\nJump / Ascend - Up," +
                        //"\nDescend - Down," +
                        "\nShift (Hold) - Speed up" +
                        "\nZoom / Controller Zoom (Autorun + Look Up / Down) - Change Speed" +
                        "\nCycle through Enemies (Nearest to Farthest) / Controller Select HUD - Lock" +
                        "\nCycle through Enemies (Farthest to Nearest) / Controller Open Main Menu - Stop");
                }
            }
            else
            {
                gameCamera->lockPosition = 0;
            }
        }
        else
        {
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
            Game.cameraNoCollideReplacer.Disable();
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

        if (Common.InputData->IsInputIDPressed(366) || Common.InputData->IsInputIDReleased(433)) // Cycle through Enemies (Nearest to Farthest) / Controller Select HUD
        {
            locked ^= true;
            if (locked && Game.ForceDisableMovement > 0)
                Game.ForceDisableMovement--;
            else
                Game.ForceDisableMovement++;
        }

        var loggedIn = DalamudApi.ClientState.IsLoggedIn;

        if (Common.InputData->IsInputIDPressed(367) || Common.InputData->IsInputIDPressed(35) || (loggedIn ? !locked && Game.ForceDisableMovement == 0 : DalamudApi.GameGui.GetAddonByName("Title") == nint.Zero)) // Cycle through Enemies (Farthest to Nearest) / Controller Open Main Menu
        {
            Toggle();
            return;
        }

        if (locked) return;

        var movePos = Vector3.Zero;

        var analogInputX = Common.InputData->GetAxisInputFloat(4); // Controller Move Forward / Back
        if (analogInputX != 0)
            movePos.X = analogInputX;

        var analogInputY = Common.InputData->GetAxisInputFloat(3); // Controller Move Left / Right
        if (analogInputY != 0)
            movePos.Z = -analogInputY;

        if (Common.InputData->IsInputIDHeld(321) || Common.InputData->IsInputIDHeld(36) && Common.InputData->IsInputIDHeld(37)) // Move Forward / Left + Right Click
            movePos.X += 1;

        if (Common.InputData->IsInputIDHeld(322)) // Move Back
            movePos.X -= 1;

        if (Common.InputData->IsInputIDHeld(323) || Common.InputData->IsInputIDHeld(325)) // Move / Strafe Left
            movePos.Z += 1;

        if (Common.InputData->IsInputIDHeld(324) || Common.InputData->IsInputIDHeld(326)) // Move / Strafe Right
            movePos.Z -= 1;

        if (Common.InputData->IsInputIDHeld(348) || Common.InputData->IsInputIDHeld(444) || Common.InputData->IsInputIDHeld(5)) // Jump / Ascend / Controller Jump
            movePos.Y += 1;

        if (Common.InputData->IsInputIDHeld(443) || Common.InputData->IsInputIDHeld(2)) // Descent / Controller Cancel
            movePos.Y -= 1;

        var mouseWheelStatus = InputData.GetMouseWheelStatus();
        if (mouseWheelStatus != 0)
            speed *= 1 + 0.2f * mouseWheelStatus;

        if (Common.InputData->IsInputIDHeld(17)) // Controller Autorun
        {
            switch (Common.InputData->GetAxisInputFloat(6)) // Controller Move Camera Up / Down
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
}
