using System;
using System.Numerics;
using Cammy.Structures;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;

namespace Cammy
{
    public static unsafe class FreeCam
    {
        private static GameCamera* gameCamera;
        public static bool Enabled => gameCamera != null;

        private static bool locked = false;
        private static float speed = 1;
        public static Vector3 position;
        private static bool onDeath = false;

        public static void Toggle(bool death = false)
        {
            var enable = !Enabled;
            var isMainMenu = !DalamudApi.Condition.Any();
            if (enable)
            {
                locked = false;
                speed = 1;
                position = DalamudApi.ClientState.LocalPlayer?.Position is { } pos ? new(pos.X, pos.Z, pos.Y + 1) : new();
                onDeath = death;

                gameCamera = isMainMenu ? Game.cameraManager->MenuCamera : Game.cameraManager->WorldCamera;
                if (isMainMenu)
                    *(byte*)((IntPtr)gameCamera + 0x2A0) = 0;
                gameCamera->MinVRotation = -1.559f;
                gameCamera->MaxVRotation = 1.559f;
                gameCamera->CurrentFoV = gameCamera->MinFoV = gameCamera->MaxFoV = 0.78f;
                gameCamera->CurrentZoom = gameCamera->MinZoom = gameCamera->MaxZoom = 0.1f;
                Game.zoomDelta = 0;
                gameCamera->AddedFoV = gameCamera->LookAtHeightOffset = 0;
                gameCamera->Mode = 1;
                Game.cameraNoCollideReplacer.Enable();

                if (!isMainMenu)
                {
                    Game.ForceDisableMovement++;
                    Cammy.PrintEcho("Additional Controls:" +
                        //"\nMove Keybinds - Move," +
                        //"\nJump / Ascend - Up," +
                        //"\nDescend - Down," +
                        "\nShift (Hold) - Speed up" +
                        "\nZoom / Controller Zoom (Autorun + Look Up / Down) - Change Speed" +
                        "\nC / Controller Dismount (Autorun + Change Hotbar Set) - Reset" +
                        "\nCycle through Enemies (Nearest to Farthest) / Controller Select HUD - Lock" +
                        "\nCycle through Enemies (Farthest to Nearest) / Controller Open Main Menu - Stop");
                }
            }
            else
            {
                gameCamera = null;
                Game.cameraNoCollideReplacer.Disable();

                if (!isMainMenu)
                {
                    if (!locked && Game.ForceDisableMovement > 0)
                        Game.ForceDisableMovement--;
                    new CameraConfigPreset().Apply();
                    PresetManager.DisableCameraPresets();
                }
            }

            if (!isMainMenu) return;

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

        public static void Update()
        {
            if (!Enabled && Cammy.Config.FreeCamOnDeath && DalamudApi.Condition[ConditionFlag.Unconscious] || Enabled && onDeath && !DalamudApi.Condition[ConditionFlag.Unconscious])
                Toggle(true);

            if (!Enabled) return;

            if (Game.IsInputIDPressed(366) || Game.IsInputIDReleased(433)) // Cycle through Enemies (Nearest to Farthest) / Controller Select HUD
            {
                locked ^= true;
                if (locked && Game.ForceDisableMovement > 0)
                    Game.ForceDisableMovement--;
                else
                    Game.ForceDisableMovement++;
            }

            var loggedIn = DalamudApi.ClientState.IsLoggedIn;

            if (Game.IsInputIDPressed(367) || Game.IsInputIDPressed(35) || (loggedIn ? !locked && Game.ForceDisableMovement == 0 : DalamudApi.GameGui.GetAddonByName("Title", 1) == IntPtr.Zero)) // Cycle through Enemies (Farthest to Nearest) / Controller Open Main Menu
            {
                Toggle();
                return;
            }

            if (locked) return;

            var movePos = Vector3.Zero;

            var analogInputX = Game.GetAnalogInputID(4); // Controller Move Forward / Back
            if (analogInputX != 0)
                movePos.X = analogInputX;

            var analogInputY = Game.GetAnalogInputID(3); // Controller Move Left / Right
            if (analogInputY != 0)
                movePos.Y = -analogInputY;

            if (Game.IsInputIDHeld(321) || Game.IsInputIDHeld(36) && Game.IsInputIDHeld(37)) // Move Forward / Left + Right Click
                movePos.X += 1;

            if (Game.IsInputIDHeld(323) || Game.IsInputIDHeld(325)) // Move / Strafe Left
                movePos.Y += 1;

            if (Game.IsInputIDHeld(322)) // Move Back
                movePos.X += -1;

            if (Game.IsInputIDHeld(324) || Game.IsInputIDHeld(326)) // Move / Strafe Right
                movePos.Y += -1;

            if (Game.IsInputIDHeld(348) || Game.IsInputIDHeld(444) || Game.IsInputIDHeld(5)) // Jump / Ascend / Controller Jump
                movePos.Z += 1;

            if (Game.IsInputIDHeld(443) || Game.IsInputIDHeld(2)) // Descent / Controller Cancel
                movePos.Z -= 1;

            if (DalamudApi.KeyState[67] || Game.IsInputIDPressed(18)) // C / Controller Dismount (Autorun + Change Hotbar Set)
            {
                DalamudApi.KeyState[67] = false;
                speed = 1;

                if (loggedIn)
                {
                    position = DalamudApi.ClientState.LocalPlayer?.Position is { } pos ? new(pos.X, pos.Z, pos.Y + 1) : new();
                }
                else
                {
                    gameCamera->X = 0;
                    gameCamera->Y = 0;
                    gameCamera->Z = 0;
                    gameCamera->Z2 = 0;
                }
            }

            var mouseWheelStatus = Game.GetMouseWheelStatus();
            if (mouseWheelStatus != 0)
                speed *= 1 + 0.2f * mouseWheelStatus;

            if (Game.IsInputIDHeld(17)) // Controller Autorun
            {
                switch (Game.GetAnalogInputID(6)) // Controller Move Camera Up / Down
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
            const double halfPI = Math.PI / 2f;
            var hAngle = gameCamera->CurrentHRotation + halfPI;
            var vAngle = gameCamera->CurrentVRotation;
            var direction = new Vector3((float)(Math.Cos(hAngle) * Math.Cos(vAngle)), -(float)(Math.Sin(hAngle) * Math.Cos(vAngle)), (float)Math.Sin(vAngle));

            var amount = direction * movePos.X;
            var x = amount.X + movePos.Y * (float)Math.Sin(gameCamera->CurrentHRotation - halfPI);
            var y = amount.Y + movePos.Y * (float)Math.Cos(gameCamera->CurrentHRotation - halfPI);
            var z = amount.Z + movePos.Z;

            if (loggedIn)
            {
                position.X += x;
                position.Y += y;
                position.Z += z;
            }
            else
            {
                gameCamera->X += x;
                gameCamera->Y += y;
                gameCamera->Z2 = gameCamera->Z += z;
            }
        }
    }
}
