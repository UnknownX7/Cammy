using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Logging;

namespace Cammy
{
    public static class Memory
    {
        public class Replacer : IDisposable
        {
            public nint Address { get; private set; } = nint.Zero;
            private readonly byte[] newBytes;
            private readonly byte[] oldBytes;
            public bool IsEnabled { get; private set; } = false;
            public bool IsValid => Address != nint.Zero;
            public string ReadBytes => !IsValid ? string.Empty : oldBytes.Aggregate(string.Empty, (current, b) => current + (b.ToString("X2") + " "));

            public Replacer(nint addr, byte[] bytes, bool startEnabled = false)
            {
                if (addr == nint.Zero) return;

                Address = addr;
                newBytes = bytes;
                SafeMemory.ReadBytes(addr, bytes.Length, out oldBytes);
                createdReplacers.Add(this);

                if (startEnabled)
                    Enable();
            }

            public Replacer(string sig, byte[] bytes, bool startEnabled = false)
            {
                var addr = nint.Zero;
                try { addr = DalamudApi.SigScanner.ScanModule(sig); }
                catch { PluginLog.LogError($"Failed to find signature {sig}"); }
                if (addr == nint.Zero) return;

                Address = addr;
                newBytes = bytes;
                SafeMemory.ReadBytes(addr, bytes.Length, out oldBytes);
                createdReplacers.Add(this);

                if (startEnabled)
                    Enable();
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

        private static readonly List<Replacer> createdReplacers = new();

        public static void Dispose()
        {
            foreach (var rep in createdReplacers)
                rep.Dispose();
        }
    }
}
