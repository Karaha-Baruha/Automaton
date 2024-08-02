namespace Automaton.Utilities;
public class Events
{
    public static event Action<uint, uint, uint>? AchievementProgressUpdate;
    public static void OnAchievementProgressUpdate(uint id, uint current, uint max) => AchievementProgressUpdate?.Invoke(id, current, max);

    public static event Action<nint, nint, nint, byte>? PacketSent;
    public static void OnPacketSent(nint addon, nint opcode, nint data, byte result) => PacketSent?.Invoke(addon, opcode, data, result);

    public static event Action<nint, uint, nint>? PacketReceived;
    public static void OnPacketRecieved(nint addon, uint opcode, nint data) => PacketReceived?.Invoke(addon, opcode, data);
}
