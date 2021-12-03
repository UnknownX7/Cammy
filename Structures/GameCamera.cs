using System;
using System.Runtime.InteropServices;

namespace Cammy.Structures
{
    [StructLayout(LayoutKind.Explicit)]
    public unsafe struct GameCamera
    {
        [FieldOffset(0x0)] public IntPtr* VTable;
        [FieldOffset(0x90)] public float X;
        [FieldOffset(0x94)] public float Z;
        [FieldOffset(0x98)] public float Y;
        [FieldOffset(0x114)] public float CurrentZoom; // 6
        [FieldOffset(0x118)] public float MinZoom; // 1.5
        [FieldOffset(0x11C)] public float MaxZoom; // 20
        [FieldOffset(0x120)] public float CurrentFoV; // 0.78
        [FieldOffset(0x124)] public float MinFoV; // 0.69
        [FieldOffset(0x128)] public float MaxFoV; // 0.78
        [FieldOffset(0x12C)] public float AddedFoV; // 0
        [FieldOffset(0x130)] public float CurrentHRotation; // -pi -> pi, default is pi
        [FieldOffset(0x134)] public float CurrentVRotation; // -0.349066
        //[FieldOffset(0x138)] public float HRotationDelta;
        [FieldOffset(0x148)] public float MinVRotation; // -1.483530, should be -+pi/2 for straight down/up but camera breaks so use -+1.569
        [FieldOffset(0x14C)] public float MaxVRotation; // 0.785398 (pi/4)
        [FieldOffset(0x160)] public float Tilt;
        [FieldOffset(0x170)] public int Mode; // camera mode??? (0 = 1st person, 1 = 3rd person, 2+ = weird controller mode? cant look up/down)
        //[FieldOffset(0x174)] public int ControlType; // 0 first person, 1 legacy, 2 standard, 3/5/6 ???, 4 ???
        [FieldOffset(0x218)] public float LookAtHeightOffset; // No idea what to call this
        [FieldOffset(0x21C)] public byte ResetLookatHeightOffset; // No idea what to call this
        [FieldOffset(0x2B4)] public float Z2;
    }

    /*
    public unsafe class VirtualTable
    {
        public delegate*<IntPtr, byte, void> vf0; // Dispose
        public delegate*<IntPtr, void> vf1; // Init
        public delegate*<IntPtr, void> vf2; // ??? (new in endwalker)
        public delegate*<IntPtr, void> vf3; // Update
        public delegate*<IntPtr, IntPtr> vf4; // ??? crashes (calls scene camera vf1)
        public delegate*<IntPtr, void> vf5; // reset camera angle
        public delegate*<IntPtr, IntPtr> vf6; // ??? gets something (might need a float array)
        public delegate*<IntPtr, IntPtr> vf7; // ??? get position / rotation? (might need a float array)
        public delegate*<IntPtr, void> vf8; // duplicate of 4
        public delegate*<IntPtr, void> vf9; // ??? (runs whenever the camera is swapped to)
        public delegate*<void> vf10; // empty function (for the world camera anyway) (runs whenever the camera is swapped from)
        public delegate*<void> vf11; // empty function
        public delegate*<IntPtr, IntPtr, bool> vf12; // ??? looks like it returns a bool? (runs whenever the camera gets too close to the character) (compares vf16 return to 2nd argument)
        public delegate*<IntPtr, byte> vf13; // ??? looks like it does something with inputs (returns 0/1 depending on some input)
        public delegate*<IntPtr, IntPtr, IntPtr, IntPtr> vf14; // applies center height offset (might need a float array)
        public delegate*<IntPtr, IntPtr, IntPtr, byte, void> vf15; // set position (requires 4 arguments, might need a float array)
        public delegate*<IntPtr, byte> vf16; // get control type? returns 1 for legacy, 2 for standard (this value is applied to 0x174)
        public delegate*<IntPtr, IntPtr> vf17; // get camera target
        public delegate*<IntPtr, IntPtr, float> vf18; // ??? crashes
        public delegate*<IntPtr, IntPtr, void> vf19; // ??? requires 2 arguments (might need a float array)
        public delegate*<IntPtr, IntPtr> vf20; // ??? looks like it does something with targeting
        public delegate*<IntPtr, byte, int> vf21; // ??? requires 2 arguments
        public delegate*<IntPtr, bool> vf22; // can change perspective (1st <-> 3rd) (SOMETHING CHANGED IN ENDWALKER)
        public delegate*<IntPtr, void> vf23; // ??? causes a "camera position set" toast with no obvious effect (switch statement with vf15 return)
        public delegate*<IntPtr, void> vf24; // loads the camera angle from 22 (switch statement with vf15 return)
        public delegate*<IntPtr, void> vf25; // causes a "camera position restored to default" toast and causes an effect similar to 1, but doesnt change horizontal angle to default (switch statement with vf15 return)
        public delegate*<IntPtr, float, float> vf26; // ??? places the camera really high above character
        public delegate*<float> vf27; // get max distance? doesnt seem to return anything except 20 ever though
        public delegate*<float> vf28; // get scroll amount (0.75)
        public delegate*<float> vf29; // get ??? (1)
        public delegate*<float> vf30; // get ??? (0.5 or 1) (uses actionmanager/g_layoutworld and uimodule vf87?)
        public delegate*<float> vf31; // duplicate of 28
        public delegate*<float> vf32; // duplicate of 28

        public VirtualTable(IntPtr* address)
        {
            foreach (var f in GetType().GetFields())
            {
                var i = ushort.Parse(f.Name[2..]);
                var vfunc = *(address + i);
                f.SetValue(this, f.FieldType.Cast(vfunc));
            }
        }
    }
     */
}