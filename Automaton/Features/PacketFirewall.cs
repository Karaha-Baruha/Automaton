using ImGuiNET;

namespace Automaton.Features;

[Tweak(debug: true)]
internal class PacketFirewall : Tweak
{
    public override string Name => "Packet Firewall";
    public override string Description => "Selectively enable sending and receiving server packets.";

    public override void Enable()
    {
        P.Memory.PacketDispatcher_OnReceivePacketHook.Enable();
        P.Memory.PacketDispatcher_OnSendPacketHook.Enable();
    }

    public override void Disable()
    {
        P.Memory.PacketDispatcher_OnReceivePacketHook.Disable();
        P.Memory.PacketDispatcher_OnSendPacketHook.Disable();
    }

    private int packet;
    private bool sending;
    public override void DrawConfig()
    {
        ImGui.Checkbox("Sending?", ref sending);
        if (ImGui.InputInt("Packet #", ref packet, 1, 10, ImGuiInputTextFlags.EnterReturnsTrue))
        {
            if (sending)
                P.Memory.DisallowedSentPackets.Add((uint)packet);
            else
                P.Memory.DisallowedReceivedPackets.Add((uint)packet);
        }

        if (ImGui.Button("Block all sending"))
            P.Memory.DisallowedSentPackets.AddRange(Enumerable.Range(0, 10000).Select(x => (uint)x));
        ImGui.SameLine();
        if (ImGui.Button("Block all receiving"))
            P.Memory.DisallowedReceivedPackets.AddRange(Enumerable.Range(0, 10000).Select(x => (uint)x));

        if (ImGui.Button("Clear"))
        {
            P.Memory.DisallowedSentPackets.Clear();
            P.Memory.DisallowedReceivedPackets.Clear();
        }

        ImGui.TextUnformatted($"Blocked Sending Packets: {string.Join(", ", P.Memory.DisallowedSentPackets)}");
        ImGui.TextUnformatted($"Blocked Receiving Packets: {string.Join(", ", P.Memory.DisallowedReceivedPackets)}");
    }
}
