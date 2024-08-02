using System.Runtime.InteropServices;

namespace Automaton.Utilities;
public unsafe class Debug
{
    // this persists through LocalPlayer going null unlike setting via PMC
    public static void SetSpeed(float speedBase)
    {
        Svc.SigScanner.TryScanText("F3 0F 59 05 ?? ?? ?? ?? F3 0F 59 05 ?? ?? ?? ?? F3 0F 58 05 ?? ?? ?? ?? 44 0F 28 C8", out var address);
        address = address + 4 + Marshal.ReadInt32(address + 4) + 4;
        Dalamud.SafeMemory.Write(address + 20, speedBase);
        SetMoveControlData(speedBase);
    }

    private static unsafe void SetMoveControlData(float speed)
        => Dalamud.SafeMemory.Write(((delegate* unmanaged[Stdcall]<byte, nint>)Svc.SigScanner.ScanText("E8 ?? ?? ?? ?? 48 85 C0 74 AE 83 FD 05"))(1) + 8, speed);
}
