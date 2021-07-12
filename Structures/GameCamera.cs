using System;

namespace Cammy.Structures
{
    public unsafe class GameCamera
    {
        public static class Offsets
        {
            public const ushort X                  = 0x90;
            public const ushort Z                  = 0x94;
            public const ushort Y                  = 0x98;
            public const ushort CurrentZoom        = 0x114;
            public const ushort MinZoom            = 0x118;
            public const ushort MaxZoom            = 0x11C;
            public const ushort CurrentFoV         = 0x120;
            public const ushort MinFoV             = 0x124;
            public const ushort MaxFoV             = 0x128;
            public const ushort AddedFoV           = 0x12C;
            public const ushort HRotation          = 0x130;
            public const ushort CurrentVRotation   = 0x134;
            public const ushort MinVRotation       = 0x148;
            public const ushort MaxVRotation       = 0x14C;
            public const ushort Tilt               = 0x160;
            public const ushort Mode               = 0x170;
            public const ushort CenterHeightOffset = 0x218;
            public const ushort Z2                 = 0x2B4;
        }

        public readonly IntPtr Address;
        public GameCamera(IntPtr ptr) => Address = ptr;
        public IntPtr this[int k] => Address + k;

        public ref float X => ref *(float*)(Address + Offsets.X);
        public ref float Y => ref *(float*)(Address + Offsets.Y);
        public ref float Z => ref *(float*)(Address + Offsets.Z);
        public ref float Z2 => ref *(float*)(Address + Offsets.Z2);
        public ref float CurrentZoom => ref *(float*)(Address + Offsets.CurrentZoom); // 6
        public ref float MinZoom => ref *(float*)(Address + Offsets.MinZoom); // 1.5
        public ref float MaxZoom => ref *(float*)(Address + Offsets.MaxZoom); // 20
        public ref float CurrentFoV => ref *(float*)(Address + Offsets.CurrentFoV); // 0.78
        public ref float MinFoV => ref *(float*)(Address + Offsets.MinFoV); // 0.69
        public ref float MaxFoV => ref *(float*)(Address + Offsets.MaxFoV); // 0.78
        public ref float AddedFoV => ref *(float*)(Address + Offsets.AddedFoV); // 0
        public ref float HRotation => ref *(float*)(Address + Offsets.HRotation); // -pi -> pi, default is pi
        public ref float CurrentVRotation => ref *(float*)(Address + Offsets.CurrentVRotation); // -0.349066
        public ref float MinVRotation => ref *(float*)(Address + Offsets.MinVRotation); // -1.483530, should be -+pi/2 for straight down/up but camera breaks so use -+1.569
        public ref float MaxVRotation => ref *(float*)(Address + Offsets.MaxVRotation); // 0.785398 (pi/4)
        public ref float Tilt => ref *(float*)(Address + Offsets.Tilt);
        public ref int Mode => ref *(int*)(Address + Offsets.Mode); // camera mode??? (0 = 1st person, 1 = 3rd person, 2+ = weird controller mode? cant look up/down)
        public ref float CenterHeightOffset => ref *(float*)(Address + Offsets.CenterHeightOffset);
    }
}