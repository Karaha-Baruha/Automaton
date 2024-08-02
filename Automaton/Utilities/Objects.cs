using FFXIVClientStructs.FFXIV.Client.Game.Character;
using System.Runtime.InteropServices;

namespace Automaton.Utilities;

public static class Objects
{
    public static byte GetStatus(DGameObject actor) => Marshal.ReadByte(actor.Address + 0x1980);
    public static bool InCombat(DGameObject actor) => (GetStatus(actor) & 2) > 0;
    public static bool InParty(DGameObject actor) => (GetStatus(actor) & 16) > 0;
    public static bool InAlliance(DGameObject actor) => (GetStatus(actor) & 32) > 0;
}

public static class ObjectExtensions
{
    public static unsafe BattleChara* BattleChara(this DGameObject obj) => (BattleChara*)obj.Address;
    public static unsafe Character* Character(this DGameObject obj) => (Character*)obj.Address;

    public static unsafe BattleChara* BattleChara(this CSGameObject obj) => (BattleChara*)&obj;
    public static unsafe Character* Character(this CSGameObject obj) => (Character*)&obj;

    public static bool IsTargetingPlayer(this DGameObject obj) => obj.TargetObjectId == Player.Object.GameObjectId;
}
