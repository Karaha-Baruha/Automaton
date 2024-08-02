using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.STD;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Automaton.Utilities;

public static class Structs
{
    [StructLayout(LayoutKind.Explicit)]
    public unsafe struct PlayerController
    {
        [FieldOffset(0x10)] public PlayerMoveControllerWalk MoveControllerWalk;
        [FieldOffset(0x150)] public PlayerMoveControllerFly MoveControllerFly;
        [FieldOffset(0x559)] public byte ControlMode;
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x140)]
    public unsafe struct PlayerMoveControllerWalk
    {
        [FieldOffset(0x10)] public Vector3 MovementDir;
        [FieldOffset(0x58)] public float BaseMovementSpeed;
        [FieldOffset(0x90)] public float MovementDirRelToCharacterFacing;
        [FieldOffset(0x94)] public byte Forced;
        [FieldOffset(0xA0)] public Vector3 MovementDirWorld;
        [FieldOffset(0xB0)] public float RotationDir;
        [FieldOffset(0x110)] public uint MovementState;
        [FieldOffset(0x114)] public float MovementLeft;
        [FieldOffset(0x118)] public float MovementFwd;
    }

    [StructLayout(LayoutKind.Explicit, Size = 0xB0)]
    public unsafe struct PlayerMoveControllerFly
    {
        [FieldOffset(0x10)] public float unk10; // x coord?
        [FieldOffset(0x14)] public float unk14; // y coord?
        [FieldOffset(0x18)] public float unk18; // z coord?
        [FieldOffset(0x40)] public float unk40;
        [FieldOffset(0x44)] public float unk44;
        [FieldOffset(0x48)] public uint unk48;
        [FieldOffset(0x4C)] public uint unk4C;
        [FieldOffset(0x50)] public uint unk50;
        [FieldOffset(0x58)] public float unk58;
        [FieldOffset(0x5C)] public float unk5C;
        [FieldOffset(0x66)] public byte IsFlying;
        [FieldOffset(0x88)] public uint unk88;
        [FieldOffset(0x8C)] public uint unk8C;
        [FieldOffset(0x90)] public uint unk90;
        [FieldOffset(0x94)] public float unk94; // speed?
        [FieldOffset(0x98)] public float unk98;
        [FieldOffset(0x9C)] public float AngularAscent;
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x2B0)]
    public unsafe struct CameraEx
    {
        [FieldOffset(0x130)] public float DirH; // 0 is north, increases CW
        [FieldOffset(0x134)] public float DirV; // 0 is horizontal, positive is looking up, negative looking down
        [FieldOffset(0x138)] public float InputDeltaHAdjusted;
        [FieldOffset(0x13C)] public float InputDeltaVAdjusted;
        [FieldOffset(0x140)] public float InputDeltaH;
        [FieldOffset(0x144)] public float InputDeltaV;
        [FieldOffset(0x148)] public float DirVMin; // -85deg by default
        [FieldOffset(0x14C)] public float DirVMax; // +45deg by default
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x1BD0)]
    public unsafe partial struct Character
    {
        [FieldOffset(0x60C)] public byte IsFlying;
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x38)]
    public struct SnapPosition
    {
        [FieldOffset(0x00)]
        public Vector3 PositionA;

        [FieldOffset(0x10)]
        public float RotationA;

        [FieldOffset(0x20)] public Vector3 PositionB;

        [FieldOffset(0x30)]
        public float RotationB;
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x40)]
    public unsafe partial struct AgentMJICraftSchedule
    {
        [StructLayout(LayoutKind.Explicit, Size = 0x98)]
        public unsafe partial struct ItemData
        {
            [FieldOffset(0x10)] public fixed ushort Materials[4];
            [FieldOffset(0x20)] public ushort ObjectId;
        }

        [StructLayout(LayoutKind.Explicit, Size = 0xC)]
        public unsafe partial struct EntryData
        {
            [FieldOffset(0x0)] public ushort CraftObjectId;
            [FieldOffset(0x2)] public ushort u2;
            [FieldOffset(0x4)] public uint u4;
            [FieldOffset(0x8)] public byte StartingSlot;
            [FieldOffset(0x9)] public byte Duration;
            [FieldOffset(0xA)] public byte Started;
            [FieldOffset(0xB)] public byte Efficient;
        }

        [StructLayout(LayoutKind.Explicit, Size = 0x54)]
        public unsafe partial struct WorkshopData
        {
            [FieldOffset(0x00)] public byte NumScheduleEntries;
            [FieldOffset(0x08)] public fixed byte EntryData[6 * 0xC];
            [FieldOffset(0x50)] public uint UsedTimeSlots;

            public Span<EntryData> Entries => new(Unsafe.AsPointer(ref EntryData[0]), 6);
        }

        [StructLayout(LayoutKind.Explicit, Size = 0xB60)]
        public unsafe partial struct AgentData
        {
            [FieldOffset(0x000)] public int InitState;
            [FieldOffset(0x004)] public int SettingAddonId;
            [FieldOffset(0x0D0)] public StdVector<ItemData> Items;
            [FieldOffset(0x400)] public fixed byte WorkshopData[4 * 0x54];
            [FieldOffset(0x5A8)] public uint CurScheduleSettingObjectIndex;
            [FieldOffset(0x5AC)] public int CurScheduleSettingWorkshop;
            [FieldOffset(0x5B0)] public int CurScheduleSettingStartingSlot;
            [FieldOffset(0x7E8)] public byte CurScheduleSettingNumMaterials;
            [FieldOffset(0x810)] public uint RestCycles;
            [FieldOffset(0x814)] public uint NewRestCycles;
            [FieldOffset(0xB58)] public byte CurrentCycle; // currently viewed
            [FieldOffset(0xB59)] public byte CycleInProgress;
            [FieldOffset(0xB5A)] public byte CurrentIslandRank; // incorrect!

            public Span<WorkshopData> Workshops => new(Unsafe.AsPointer(ref WorkshopData[0]), 4);
        }

        [FieldOffset(0)] public AgentInterface AgentInterface;
        [FieldOffset(0x28)] public AgentData* Data;
    }
}
