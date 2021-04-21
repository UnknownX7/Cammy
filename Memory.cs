using System;
using System.Collections.Generic;
using Dalamud;

namespace Cammy
{
    public static class Memory
    {
        public class Replacer : IDisposable
        {
            public IntPtr Address { get; private set; } = IntPtr.Zero;
            private readonly byte[] newBytes;
            private readonly byte[] oldBytes;
            public bool IsEnabled { get; private set; } = false;
            public bool IsValid => Address != IntPtr.Zero;

            public Replacer(IntPtr addr, byte[] bytes)
            {
                if (addr == IntPtr.Zero) return;

                Address = addr;
                newBytes = bytes;
                SafeMemory.ReadBytes(addr, bytes.Length, out oldBytes);
                createdReplacers.Add(this);
            }

            public Replacer(string sig, byte[] bytes)
            {
                var addr = IntPtr.Zero;
                try { addr = Cammy.Interface.TargetModuleScanner.ScanModule(sig); }
                catch { }
                if (addr == IntPtr.Zero) return;

                Address = addr;
                newBytes = bytes;
                SafeMemory.ReadBytes(addr, bytes.Length, out oldBytes);
                createdReplacers.Add(this);
            }

            public void Enable()
            {
                if (!IsValid) return;
                SafeMemory.WriteBytes(Address, newBytes);
                IsEnabled = true;
            }

            public void Disable()
            {
                if (!IsValid) return;
                SafeMemory.WriteBytes(Address, oldBytes);
                IsEnabled = false;
            }

            public void Toggle()
            {
                if (!IsEnabled)
                    Enable();
                else
                    Disable();
            }

            public void Dispose()
            {
                if (IsEnabled)
                    Disable();
            }
        }

        private static readonly List<Replacer> createdReplacers = new List<Replacer>();

        public static void Dispose()
        {
            foreach (var rep in createdReplacers)
                rep.Dispose();
        }
    }
}
