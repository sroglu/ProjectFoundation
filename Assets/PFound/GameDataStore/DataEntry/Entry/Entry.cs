using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Cysharp.Text;
using Sirenix.OdinInspector;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace PFound.GameDataStore.Entries
{
    [Serializable]
    [StructLayout(LayoutKind.Explicit)]
    public struct Entry : IEquatable<Entry>, IComparable<Entry>, IConvertible
    {
        #region Data Fields

        //@formatter:off
        [FieldOffset(0), HideLabel, HorizontalGroup(width: 100)] public EntryType Type;
        
        [FieldOffset(4), ShowInInspector, HideLabel, HorizontalGroup, ShowIf("IsShowInt"), NonSerialized] internal int InternalInt;
        [FieldOffset(4), ShowInInspector, HideLabel, HorizontalGroup, ShowIf("IsShowInt2"), NonSerialized] internal int2 InternalInt2;
        [FieldOffset(4), ShowInInspector, HideLabel, HorizontalGroup, ShowIf("IsShowInt3"), /*SERIALIZED*/SerializeField] internal int3 InternalInt3;
        [FieldOffset(4), ShowInInspector, HideLabel, HorizontalGroup, ShowIf("IsShowInt64"), NonSerialized] internal Int64 InternalInt64;
        [FieldOffset(4), ShowInInspector, HideLabel, HorizontalGroup, ShowIf("IsShowUInt"), NonSerialized] internal uint InternalUInt;
        [FieldOffset(4), ShowInInspector, HideLabel, HorizontalGroup, ShowIf("IsShowUInt2"), NonSerialized] internal uint2 InternalUInt2;
        [FieldOffset(4), ShowInInspector, HideLabel, HorizontalGroup, ShowIf("IsShowUInt3"), NonSerialized] internal uint3 InternalUInt3;
        [FieldOffset(4), ShowInInspector, HideLabel, HorizontalGroup, ShowIf("IsShowUInt64"), NonSerialized] internal UInt64 InternalUInt64;
        [FieldOffset(4), ShowInInspector, HideLabel, HorizontalGroup, ShowIf("IsShowShort"), NonSerialized] internal short InternalShort;
        [FieldOffset(4), ShowInInspector, HideLabel, HorizontalGroup, ShowIf("IsShowUshort"), NonSerialized] internal ushort InternalUShort;
        [FieldOffset(4), ShowInInspector, HideLabel, HorizontalGroup, ShowIf("IsShowByte"), NonSerialized] internal byte InternalByte;
        [FieldOffset(4), ShowInInspector, HideLabel, HorizontalGroup, ShowIf("IsShowSbyte"), NonSerialized] internal sbyte InternalSByte;
        [FieldOffset(4), ShowInInspector, HideLabel, HorizontalGroup, ShowIf("IsShowFloat"), NonSerialized] internal float InternalFloat;
        [FieldOffset(4), ShowInInspector, HideLabel, HorizontalGroup, ShowIf("IsShowFloat2"), NonSerialized] internal float2 InternalFloat2;
        [FieldOffset(4), ShowInInspector, HideLabel, HorizontalGroup, ShowIf("IsShowFloat3"), NonSerialized] internal float3 InternalFloat3;
        [FieldOffset(4), ShowInInspector, HideLabel, HorizontalGroup, ShowIf("IsShowDouble"), NonSerialized] internal double InternalDouble;
        [FieldOffset(4), ShowInInspector, HideLabel, HorizontalGroup, ShowIf("IsShowChar"), NonSerialized] internal char InternalChar;
        [FieldOffset(16), ShowInInspector, HideLabel, HorizontalGroup, ShowIf("IsShowString"), /*SERIALIZED*/SerializeField] internal string InternalString;
        [FieldOffset(4), ShowInInspector, HideLabel, HorizontalGroup, ShowIf("IsShowBool"), NonSerialized] internal bool InternalBool;
        [FieldOffset(4), ShowInInspector, HideLabel, HorizontalGroup, ShowIf("IsShowEntityType"), NonSerialized] internal EntityType InternalEntityType;
        [FieldOffset(4), ShowInInspector, HideLabel, HorizontalGroup, ShowIf("IsShowEntityLevel"), NonSerialized] internal EntityLevel InternalEntityLevel;
        [FieldOffset(4), ShowInInspector, HideLabel, HorizontalGroup, ShowIf("IsShowWeekDay"), NonSerialized] internal WeekDay InternalWeekDay;
        //@formatter:on
        
        #endregion

        #region Accesors for UI

        public int ValueInt
        {
            get
            {
                if (Type != EntryType.Int)
                    throw new InvalidCastException($"Entry is not of type Int. Current type: {Type}");
                return InternalInt;
            }
            set
            {
                if (Type != EntryType.Int)
                    throw new InvalidCastException($"Entry is not of type Int. Current type: {Type}");
                InternalInt = value;
            }
        }
        public (int x, int y) ValueInt2
        {
            get
            {
                if (Type != EntryType.Int2)
                    throw new InvalidCastException($"Entry is not of type Int2. Current type: {Type}");
                return (InternalInt2.x, InternalInt2.y);
            }
            set
            {
                if (Type != EntryType.Int2)
                    throw new InvalidCastException($"Entry is not of type Int2. Current type: {Type}");
                InternalInt2 = new int2(value.x, value.y);
            }
        }
        public (int x, int y, int z) ValueInt3
        {
            get
            {
                if (Type != EntryType.Int3)
                    throw new InvalidCastException($"Entry is not of type Int3. Current type: {Type}");
                return (InternalInt3.x, InternalInt3.y, InternalInt3.z);
            }
            set
            {
                if (Type != EntryType.Int3)
                    throw new InvalidCastException($"Entry is not of type Int3. Current type: {Type}");
                InternalInt3 = new int3(value.x, value.y, value.z);
            }
        }
        public long ValueInt64
        {
            get
            {
                if (Type != EntryType.Int64)
                    throw new InvalidCastException($"Entry is not of type Int64. Current type: {Type}");
                return InternalInt64;
            }
            set
            {
                if (Type != EntryType.Int64)
                    throw new InvalidCastException($"Entry is not of type Int64. Current type: {Type}");
                InternalInt64 = value;
            }
        }
        public uint ValueUInt
        {
            get
            {
                if (Type != EntryType.UInt)
                    throw new InvalidCastException($"Entry is not of type UInt. Current type: {Type}");
                return InternalUInt;
            }
            set
            {
                if (Type != EntryType.UInt)
                    throw new InvalidCastException($"Entry is not of type UInt. Current type: {Type}");
                InternalUInt = value;
            }
        }
        public (uint x, uint y) ValueUInt2
        {
            get
            {
                if (Type != EntryType.UInt2)
                    throw new InvalidCastException($"Entry is not of type UInt2. Current type: {Type}");
                return (InternalUInt2.x, InternalUInt2.y);
            }
            set
            {
                if (Type != EntryType.UInt2)
                    throw new InvalidCastException($"Entry is not of type UInt2. Current type: {Type}");
                InternalUInt2 = new uint2(value.x, value.y);
            }
        }
        public (uint x, uint y, uint z) ValueUInt3
        {
            get
            {
                if (Type != EntryType.UInt3)
                    throw new InvalidCastException($"Entry is not of type UInt3. Current type: {Type}");
                return (InternalUInt3.x, InternalUInt3.y, InternalUInt3.z);
            }
            set
            {
                if (Type != EntryType.UInt3)
                    throw new InvalidCastException($"Entry is not of type UInt3. Current type: {Type}");
                InternalUInt3 = new uint3(value.x, value.y, value.z);
            }
        }
        public ulong ValueUInt64
        {
            get
            {
                if (Type != EntryType.UInt64)
                    throw new InvalidCastException($"Entry is not of type UInt64. Current type: {Type}");
                return InternalUInt64;
            }
            set
            {
                if (Type != EntryType.UInt64)
                    throw new InvalidCastException($"Entry is not of type UInt64. Current type: {Type}");
                InternalUInt64 = value;
            }
        }
        public short ValueShort
        {
            get
            {
                if (Type != EntryType.Short)
                    throw new InvalidCastException($"Entry is not of type Short. Current type: {Type}");
                return InternalShort;
            }
            set
            {
                if (Type != EntryType.Short)
                    throw new InvalidCastException($"Entry is not of type Short. Current type: {Type}");
                InternalShort = value;
            }
        }
        public ushort ValueUShort
        {
            get
            {
                if (Type != EntryType.UShort)
                    throw new InvalidCastException($"Entry is not of type UShort. Current type: {Type}");
                return InternalUShort;
            }
            set
            {
                if (Type != EntryType.UShort)
                    throw new InvalidCastException($"Entry is not of type UShort. Current type: {Type}");
                InternalUShort = value;
            }
        }
        public byte ValueByte
        {
            get
            {
                if (Type != EntryType.Byte)
                    throw new InvalidCastException($"Entry is not of type Byte. Current type: {Type}");
                return InternalByte;
            }
            set
            {
                if (Type != EntryType.Byte)
                    throw new InvalidCastException($"Entry is not of type Byte. Current type: {Type}");
                InternalByte = value;
            }
        }
        public sbyte ValueSByte
        {
            get
            {
                if (Type != EntryType.SByte)
                    throw new InvalidCastException($"Entry is not of type Sbyte. Current type: {Type}");
                return InternalSByte;
            }
            set
            {
                if (Type != EntryType.SByte)
                    throw new InvalidCastException($"Entry is not of type Sbyte. Current type: {Type}");
                InternalSByte = value;
            }
        }
        public float ValueFloat
        {
            get
            {
                if (Type != EntryType.Float)
                    throw new InvalidCastException($"Entry is not of type Float. Current type: {Type}");
                return InternalFloat;
            }
            set
            {
                if (Type != EntryType.Float)
                    throw new InvalidCastException($"Entry is not of type Float. Current type: {Type}");
                InternalFloat = value;
            }
        }
        public (float x, float y) ValueFloat2
        {
            get
            {
                if (Type != EntryType.Float2)
                    throw new InvalidCastException($"Entry is not of type Float2. Current type: {Type}");
                return (InternalFloat2.x, InternalFloat2.y);
            }
            set
            {
                if (Type != EntryType.Float2)
                    throw new InvalidCastException($"Entry is not of type Float2. Current type: {Type}");
                InternalFloat2 = new float2(value.x, value.y);
            }
        }
        public (float x, float y, float z) ValueFloat3
        {
            get
            {
                if (Type != EntryType.Float3)
                    throw new InvalidCastException($"Entry is not of type Float3. Current type: {Type}");
                return (InternalFloat3.x, InternalFloat3.y, InternalFloat3.z);
            }
            set
            {
                if (Type != EntryType.Float3)
                    throw new InvalidCastException($"Entry is not of type Float3. Current type: {Type}");
                InternalFloat3 = new float3(value.x, value.y, value.z);
            }
        }
        public double ValueDouble
        {
            get
            {
                if (Type != EntryType.Double)
                    throw new InvalidCastException($"Entry is not of type Double. Current type: {Type}");
                return InternalDouble;
            }
            set
            {
                if (Type != EntryType.Double)
                    throw new InvalidCastException($"Entry is not of type Double. Current type: {Type}");
                InternalDouble = value;
            }
        }
        public char ValueChar
        {
            get
            {
                if (Type != EntryType.Char)
                    throw new InvalidCastException($"Entry is not of type Char. Current type: {Type}");
                return InternalChar;
            }
            set
            {
                if (Type != EntryType.Char)
                    throw new InvalidCastException($"Entry is not of type Char. Current type: {Type}");
                InternalChar = value;
            }
        }
        public string ValueString
        {
            get
            {
                if (Type != EntryType.String)
                    throw new InvalidCastException($"Entry is not of type String. Current type: {Type}");
                return InternalString;
            }
            set
            {
                if (Type != EntryType.String)
                    throw new InvalidCastException($"Entry is not of type String. Current type: {Type}");
                InternalString = value;
            }
        }
        public bool ValueBool
        {
            get
            {
                if (Type != EntryType.Bool)
                    throw new InvalidCastException($"Entry is not of type Bool. Current type: {Type}");
                return InternalBool;
            }
            set
            {
                if (Type != EntryType.Bool)
                    throw new InvalidCastException($"Entry is not of type Bool. Current type: {Type}");
                InternalBool = value;
            }
        }
        public EntityType ValueEntityType
        {
            get
            {
                if (Type != EntryType.EntityType)
                    throw new InvalidCastException($"Entry is not of type EntityType. Current type: {Type}");
                return InternalEntityType;
            }
            set
            {
                if (Type != EntryType.EntityType)
                    throw new InvalidCastException($"Entry is not of type EntityType. Current type: {Type}");
                InternalEntityType = value;
            }
        }
        public EntityLevel ValueEntityLevel
        {
            get
            {
                if (Type != EntryType.EntityLevel)
                    throw new InvalidCastException($"Entry is not of type EntityLevel. Current type: {Type}");
                return InternalEntityLevel;
            }
            set
            {
                if (Type != EntryType.EntityLevel)
                    throw new InvalidCastException($"Entry is not of type EntityLevel. Current type: {Type}");
                InternalEntityLevel = value;
            }
        }
        public WeekDay ValueWeekDay
        {
            get
            {
                if (Type != EntryType.WeekDay)
                    throw new InvalidCastException($"Entry is not of type WeekDay. Current type: {Type}");
                return InternalWeekDay;
            }
            set
            {
                if (Type != EntryType.WeekDay)
                    throw new InvalidCastException($"Entry is not of type WeekDay. Current type: {Type}");
                InternalWeekDay = value;
            }
        }
        
        #endregion
        
        #region Constructors
        
        //@formatter:off
        public Entry(int value):this() { Type = EntryType.Int; InternalInt = value; }
        public Entry(int2 value):this() { Type = EntryType.Int2; InternalInt2 = value; }
        public Entry(int3 value):this() { Type = EntryType.Int3; InternalInt3 = value; }
        public Entry(Int64 value):this() { Type = EntryType.Int64; InternalInt64 = value; }
        public Entry(uint value):this() { Type = EntryType.UInt; InternalUInt = value; }
        public Entry(uint2 value):this() { Type = EntryType.UInt2; InternalUInt2 = value; }
        public Entry(uint3 value):this() { Type = EntryType.UInt3; InternalUInt3 = value; }
        public Entry(UInt64 value):this() { Type = EntryType.UInt64; InternalUInt64 = value; }
        public Entry(short value):this() { Type = EntryType.Short; InternalShort = value; }
        public Entry(ushort value):this() { Type = EntryType.UShort; InternalUShort = value; }
        public Entry(byte value):this() { Type = EntryType.Byte; InternalByte = value; }
        public Entry(sbyte value):this() { Type = EntryType.SByte; InternalSByte = value; }
        public Entry(float value):this() { Type = EntryType.Float; InternalFloat = value; }
        public Entry(float2 value):this() { Type = EntryType.Float2; InternalFloat2 = value; }
        public Entry(float3 value):this() { Type = EntryType.Float3; InternalFloat3 = value; }
        public Entry(double value):this() { Type = EntryType.Double; InternalDouble = value; }
        public Entry(char value):this() { Type = EntryType.Char; InternalChar = value; }
        public Entry(string value):this() { Type = EntryType.String; InternalString = value; }
        public Entry(bool value):this() { Type = EntryType.Bool; InternalBool = value; }
        public Entry(EntityType value):this() { Type = EntryType.EntityType; InternalEntityType = value; }
        public Entry(EntityLevel value):this() { Type = EntryType.EntityLevel; InternalEntityLevel = value; }
        public Entry(WeekDay value):this() { Type = EntryType.WeekDay; InternalWeekDay = value; }
        //@formatter:on

        #endregion
        
        public static void Initialize()
        {
            if(InvalidEntriesByEntryType != null) return;
            
            InitializeInvalidEntries();
            InitializeInvalidSystemEntries();
            InitializeDefaultSystemValues();
        }
        
        #region Getters

        public bool IsValid => Type != EntryType.Invalid;
        public bool IsInvalid => Type == EntryType.Invalid;

        //@formatter:off
        public bool TryGetInt(out int value) { value = InternalInt; return Type == EntryType.Int; }
        public bool TryGetInt2(out int2 value) { value = InternalInt2; return Type == EntryType.Int2; }
        public bool TryGetInt3(out int3 value) { value = InternalInt3; return Type == EntryType.Int3; }
        public bool TryGetInt64(out Int64 value) { value = InternalInt64; return Type == EntryType.Int64; }
        public bool TryGetUint(out uint value) { value = InternalUInt; return Type == EntryType.UInt; }
        public bool TryGetUint2(out uint2 value) { value = InternalUInt2; return Type == EntryType.UInt2; }
        public bool TryGetUint3(out uint3 value) { value = InternalUInt3; return Type == EntryType.UInt3; }
        public bool TryGetUint64(out UInt64 value) { value = InternalUInt64; return Type == EntryType.UInt64; }
        public bool TryGetShort(out short value) { value = InternalShort; return Type == EntryType.Short; }
        public bool TryGetUshort(out ushort value) { value = InternalUShort; return Type == EntryType.UShort; }
        public bool TryGetByte(out byte value) { value = InternalByte; return Type == EntryType.Byte; }
        public bool TryGetSbyte(out sbyte value) { value = InternalSByte; return Type == EntryType.SByte; }
        public bool TryGetFloat(out float value) { value = InternalFloat; return Type == EntryType.Float; }
        public bool TryGetFloat2(out float2 value) { value = InternalFloat2; return Type == EntryType.Float2; }
        public bool TryGetFloat3(out float3 value) { value = InternalFloat3; return Type == EntryType.Float3; }
        public bool TryGetDouble(out double value) { value = InternalDouble; return Type == EntryType.Double; }
        public bool TryGetChar(out char value) { value = InternalChar; return Type == EntryType.Char; }
        public bool TryGetString(out string value) { value = InternalString; return Type == EntryType.String; }
        public bool TryGetBool(out bool value) { value = InternalBool; return Type == EntryType.Bool; }
        public bool TryGetEntityType(out EntityType value) { value = InternalEntityType; return Type == EntryType.EntityType; }
        public bool TryGetEntityLevel(out EntityLevel value) { value = InternalEntityLevel; return Type == EntryType.EntityLevel; }
        public bool TryGetWeekDay(out WeekDay value) { value = InternalWeekDay; return Type == EntryType.WeekDay; }
        //@formatter:on
        

        //@formatter:off
        public int GetInt() => Type == EntryType.Int ? InternalInt : throw new InvalidCastException();
        public int2 GetInt2() => Type == EntryType.Int2 ? InternalInt2 : throw new InvalidCastException();
        public int3 GetInt3() => Type == EntryType.Int3 ? InternalInt3 : throw new InvalidCastException();
        public Int64 GetInt64() => Type == EntryType.Int64 ? InternalInt64 : throw new InvalidCastException();
        public uint GetUint() => Type == EntryType.UInt ? InternalUInt : throw new InvalidCastException();
        public uint2 GetUint2() => Type == EntryType.UInt2 ? InternalUInt2 : throw new InvalidCastException();
        public uint3 GetUint3() => Type == EntryType.UInt3 ? InternalUInt3 : throw new InvalidCastException();
        public UInt64 GetUint64() => Type == EntryType.UInt64 ? InternalUInt64 : throw new InvalidCastException();
        public short GetShort() => Type == EntryType.Short ? InternalShort : throw new InvalidCastException();
        public ushort GetUshort() => Type == EntryType.UShort ? InternalUShort : throw new InvalidCastException();
        public byte GetByte() => Type == EntryType.Byte ? InternalByte : throw new InvalidCastException();
        public sbyte GetSbyte() => Type == EntryType.SByte ? InternalSByte : throw new InvalidCastException();
        public float GetFloat() => Type == EntryType.Float ? InternalFloat : throw new InvalidCastException();
        public float2 GetFloat2() => Type == EntryType.Float2 ? InternalFloat2 : throw new InvalidCastException();
        public float3 GetFloat3() => Type == EntryType.Float3 ? InternalFloat3 : throw new InvalidCastException();
        public double GetDouble() => Type == EntryType.Double ? InternalDouble : throw new InvalidCastException();
        public char GetChar() => Type == EntryType.Char ? InternalChar : throw new InvalidCastException();
        public string GetString() => Type == EntryType.String ? InternalString : throw new InvalidCastException();
        public bool GetBool() => Type == EntryType.Bool ? InternalBool : throw new InvalidCastException();
        public EntityType GetEntityType() => Type == EntryType.EntityType ? InternalEntityType : throw new InvalidCastException();
        public EntityLevel GetEntityLevel() => Type == EntryType.EntityLevel ? InternalEntityLevel : throw new InvalidCastException();
        public WeekDay GetWeekDay() => Type == EntryType.WeekDay ? InternalWeekDay : throw new InvalidCastException();
        //@formatter:on

        #endregion

        #region Invalid Values

        public static readonly Entry Invalid = default;
        public static readonly int InvalidInt = int.MinValue;
        public static readonly int2 InvalidInt2 = int.MinValue;
        public static readonly int3 InvalidInt3 = int.MinValue;
        public static readonly Int64 InvalidInt64 = Int64.MinValue;
        public static readonly uint InvalidUint = uint.MinValue;
        public static readonly uint2 InvalidUint2 = uint.MinValue;
        public static readonly uint3 InvalidUint3 = uint.MinValue;
        public static readonly UInt64 InvalidUInt64 = UInt64.MinValue;
        public static readonly short InvalidShort = short.MinValue;
        public static readonly ushort InvalidUshort = ushort.MinValue;
        public static readonly byte InvalidByte = byte.MinValue;
        public static readonly sbyte InvalidSbyte = sbyte.MinValue;
        public static readonly float InvalidFloat = float.MinValue;
        public static readonly float2 InvalidFloat2 = float.MinValue;
        public static readonly float3 InvalidFloat3 = float.MinValue;
        public static readonly double InvalidDouble = double.MinValue;
        public static readonly char InvalidChar = char.MinValue;
        public static readonly string InvalidString = string.Empty;
        public static readonly bool InvalidBool = false;
        public static readonly EntityType InvalidEntityType = default;
        public static readonly EntityLevel InvalidEntityLevel = default;
        public static readonly WeekDay InvalidWeekDay = WeekDay.Invalid;

        #endregion
        
        #region Invalid/Default Value Dictionary Initialization

        public static Entry[] InvalidEntriesByEntryType;
        public static object[] InvalidEntriesBySystemType;
        public static object[] DefaultSystemValuesByEntryType;
        public static Entry GetInvalidValue(EntryType entryType)
        {
            return InvalidEntriesByEntryType[(int) entryType];
        }
        public static object GetInvalidSystemValue(EntryType entryType)
        {
            return InvalidEntriesBySystemType[(int)entryType];
        }
        
        private static void InitializeInvalidEntries()
        {
            InvalidEntriesByEntryType = new Entry[EntryTypeTools.EntryTypeRadixArrayLength];
            //@formatter:off
            InvalidEntriesByEntryType[(int) EntryType.Invalid] = default;
            InvalidEntriesByEntryType[(int) EntryType.Int] = new Entry(InvalidInt);
            InvalidEntriesByEntryType[(int) EntryType.Int2] = new Entry(InvalidInt2);
            InvalidEntriesByEntryType[(int) EntryType.Int3] = new Entry(InvalidInt3);
            InvalidEntriesByEntryType[(int) EntryType.Int64] = new Entry(InvalidInt64);
            InvalidEntriesByEntryType[(int) EntryType.UInt] = new Entry(InvalidUint);
            InvalidEntriesByEntryType[(int) EntryType.UInt2] = new Entry(InvalidUint2);
            InvalidEntriesByEntryType[(int) EntryType.UInt3] = new Entry(InvalidUint3);
            InvalidEntriesByEntryType[(int) EntryType.UInt64] = new Entry(InvalidUInt64);
            InvalidEntriesByEntryType[(int) EntryType.Short] = new Entry(InvalidShort);
            InvalidEntriesByEntryType[(int) EntryType.UShort] = new Entry(InvalidUshort);
            InvalidEntriesByEntryType[(int) EntryType.Byte] = new Entry(InvalidByte);
            InvalidEntriesByEntryType[(int) EntryType.SByte] = new Entry(InvalidSbyte);
            InvalidEntriesByEntryType[(int) EntryType.Float] = new Entry(InvalidFloat);
            InvalidEntriesByEntryType[(int) EntryType.Float2] = new Entry(InvalidFloat2);
            InvalidEntriesByEntryType[(int) EntryType.Float3] = new Entry(InvalidFloat3);
            InvalidEntriesByEntryType[(int) EntryType.Double] = new Entry(InvalidDouble);
            InvalidEntriesByEntryType[(int) EntryType.Char] = new Entry(InvalidChar);
            InvalidEntriesByEntryType[(int) EntryType.String] = new Entry(InvalidString);
            InvalidEntriesByEntryType[(int) EntryType.Bool] = new Entry(InvalidBool);
            InvalidEntriesByEntryType[(int) EntryType.EntityType] = new Entry(InvalidEntityType);
            InvalidEntriesByEntryType[(int) EntryType.EntityLevel] = new Entry(InvalidEntityLevel);
            InvalidEntriesByEntryType[(int) EntryType.WeekDay] = new Entry(InvalidWeekDay);
            //@formatter:on
        }
        private static void InitializeInvalidSystemEntries()
        {
            InvalidEntriesBySystemType = new object[EntryTypeTools.EntryTypeRadixArrayLength];
            //@formatter:off
            InvalidEntriesBySystemType[(int) EntryType.Invalid] = default;
            InvalidEntriesBySystemType[(int) EntryType.Int] = InvalidInt;
            InvalidEntriesBySystemType[(int) EntryType.Int2] = InvalidInt2;
            InvalidEntriesBySystemType[(int) EntryType.Int3] = InvalidInt3;
            InvalidEntriesBySystemType[(int) EntryType.Int64] = InvalidInt64;
            InvalidEntriesBySystemType[(int) EntryType.UInt] = InvalidUint;
            InvalidEntriesBySystemType[(int) EntryType.UInt2] = InvalidUint2;
            InvalidEntriesBySystemType[(int) EntryType.UInt3] = InvalidUint3;
            InvalidEntriesBySystemType[(int) EntryType.UInt64] = InvalidUInt64;
            InvalidEntriesBySystemType[(int) EntryType.Short] = InvalidShort;
            InvalidEntriesBySystemType[(int) EntryType.UShort] = InvalidUshort;
            InvalidEntriesBySystemType[(int) EntryType.Byte] = InvalidByte;
            InvalidEntriesBySystemType[(int) EntryType.SByte] = InvalidSbyte;
            InvalidEntriesBySystemType[(int) EntryType.Float] = InvalidFloat;
            InvalidEntriesBySystemType[(int) EntryType.Float2] = InvalidFloat2;
            InvalidEntriesBySystemType[(int) EntryType.Float3] = InvalidFloat3;
            InvalidEntriesBySystemType[(int) EntryType.Double] = InvalidDouble;
            InvalidEntriesBySystemType[(int) EntryType.Char] = InvalidChar;
            InvalidEntriesBySystemType[(int) EntryType.String] = InvalidString;
            InvalidEntriesBySystemType[(int) EntryType.Bool] = InvalidBool;
            InvalidEntriesBySystemType[(int) EntryType.EntityType] = InvalidEntityType;
            InvalidEntriesBySystemType[(int) EntryType.EntityLevel] = InvalidEntityLevel;
            InvalidEntriesBySystemType[(int) EntryType.WeekDay] = InvalidWeekDay;
            //@formatter:on
        }
        
        private static void InitializeDefaultSystemValues()
        {
            DefaultSystemValuesByEntryType = new object[EntryTypeTools.EntryTypeRadixArrayLength];
            //@formatter:off
            DefaultSystemValuesByEntryType[(int)EntryType.Int] = default(int);
            DefaultSystemValuesByEntryType[(int)EntryType.Int2] = default(int2);
            DefaultSystemValuesByEntryType[(int)EntryType.Int3] = default(int3);
            DefaultSystemValuesByEntryType[(int)EntryType.Int64] = default(Int64);
            DefaultSystemValuesByEntryType[(int)EntryType.UInt] = default(uint);
            DefaultSystemValuesByEntryType[(int)EntryType.UInt2] = default(uint2);
            DefaultSystemValuesByEntryType[(int)EntryType.UInt3] = default(uint3);
            DefaultSystemValuesByEntryType[(int)EntryType.UInt64] = default(UInt64);
            DefaultSystemValuesByEntryType[(int)EntryType.Short] = default(short);
            DefaultSystemValuesByEntryType[(int)EntryType.UShort] = default(ushort);
            DefaultSystemValuesByEntryType[(int)EntryType.Byte] = default(byte);
            DefaultSystemValuesByEntryType[(int)EntryType.SByte] = default(sbyte);
            DefaultSystemValuesByEntryType[(int)EntryType.Float] = default(float);
            DefaultSystemValuesByEntryType[(int)EntryType.Float2] = default(float2);
            DefaultSystemValuesByEntryType[(int)EntryType.Float3] = default(float3);
            DefaultSystemValuesByEntryType[(int)EntryType.Double] = default(double);
            DefaultSystemValuesByEntryType[(int)EntryType.Char] = default(char);
            DefaultSystemValuesByEntryType[(int)EntryType.String] = default(string);
            DefaultSystemValuesByEntryType[(int)EntryType.Bool] = default(bool);
            DefaultSystemValuesByEntryType[(int)EntryType.EntityType] = default(EntityType);
            DefaultSystemValuesByEntryType[(int)EntryType.EntityLevel] = default(EntityLevel);
            DefaultSystemValuesByEntryType[(int)EntryType.WeekDay] = default(WeekDay);
            //@formatter:on
        }

        #endregion

        #region Components

        public bool TryGetComponentValue(string componentName, out Entry componentValue)
        {
            switch (Type)
            {
                //@formatter:off
                case EntryType.Invalid: break;
                case EntryType.Int: break;
                case EntryType.Int2: switch (componentName){case "x": componentValue = new Entry(InternalInt2.x); return true; case "y": componentValue = new Entry(InternalInt2.y); return true;} break;
                case EntryType.Int3: switch (componentName){case "x": componentValue = new Entry(InternalInt3.x); return true; case "y": componentValue = new Entry(InternalInt3.y); return true; case "z": componentValue = new Entry(InternalInt3.z); return true;} break;
                case EntryType.Int64: break;
                case EntryType.UInt: break;
                case EntryType.UInt2: switch (componentName){case "x": componentValue = new Entry(InternalUInt2.x); return true; case "y": componentValue = new Entry(InternalUInt2.y); return true;} break;
                case EntryType.UInt3: switch (componentName){case "x": componentValue = new Entry(InternalUInt3.x); return true; case "y": componentValue = new Entry(InternalUInt3.y); return true; case "z": componentValue = new Entry(InternalUInt3.z); return true;} break;
                case EntryType.UInt64: break;
                case EntryType.Short: break;
                case EntryType.UShort: break;
                case EntryType.Byte: break;
                case EntryType.SByte: break;
                case EntryType.Float: break;
                case EntryType.Float2: switch (componentName){case "x": componentValue = new Entry(InternalFloat2.x); return true; case "y": componentValue = new Entry(InternalFloat2.y); return true;} break;
                case EntryType.Float3: switch (componentName){case "x": componentValue = new Entry(InternalFloat3.x); return true; case "y": componentValue = new Entry(InternalFloat3.y); return true; case "z": componentValue = new Entry(InternalFloat3.z); return true;} break;
                case EntryType.Double: break;
                case EntryType.Char: break;
                case EntryType.String: break;
                case EntryType.Bool: break;
                case EntryType.EntityType: break;
                case EntryType.EntityLevel: break;
                case EntryType.WeekDay: break;
                //@formatter:on
                default: throw new ArgumentOutOfRangeException();
            }

            componentValue = default;
            return false;
        }

        public Entry GetComponent(string componentName)
        {
            if (TryGetComponentValue(componentName, out var componentValue)) 
                return componentValue;
            throw new Exception($"Tried to get component {componentName} from Entry of type {Type}");
        }

        #endregion

        #region Implicit Casts
        //@formatter:off
        public static implicit operator Entry(int value) => new Entry(value);
        public static implicit operator Entry(int2 value) => new Entry(value);
        public static implicit operator Entry(int3 value) => new Entry(value);
        public static implicit operator Entry(Int64 value) => new Entry(value);
        public static implicit operator Entry(uint value) => new Entry(value);
        public static implicit operator Entry(uint2 value) => new Entry(value);
        public static implicit operator Entry(uint3 value) => new Entry(value);
        public static implicit operator Entry(UInt64 value) => new Entry(value);
        public static implicit operator Entry(short value) => new Entry(value);
        public static implicit operator Entry(ushort value) => new Entry(value);
        public static implicit operator Entry(byte value) => new Entry(value);
        public static implicit operator Entry(sbyte value) => new Entry(value);
        public static implicit operator Entry(float value) => new Entry(value);
        public static implicit operator Entry(float2 value) => new Entry(value);
        public static implicit operator Entry(float3 value) => new Entry(value);
        public static implicit operator Entry(double value) => new Entry(value);
        public static implicit operator Entry(char value) => new Entry(value);
        public static implicit operator Entry(string value) => new Entry(value);
        public static implicit operator Entry(bool value) => new Entry(value);
        public static implicit operator Entry(EntityType value) => new Entry(value);
        public static implicit operator Entry(EntityLevel value) => new Entry(value);
        public static implicit operator Entry(WeekDay value) => new Entry(value);
        //@formatter:on
        #endregion
        
        #region Comparison

        public int CompareTo(Entry other)
        {
            if(Type != other.Type)
                throw new Exception($"Both entries must have the same type to be compared. Entry1: {Type}, Entry2: {other.Type}");

            switch (Type)
            {
                //@formatter:off
                case EntryType.Invalid: return 0;
                case EntryType.Int:     return InternalInt.CompareTo(other.InternalInt);
                case EntryType.Int2:
                {
                    var cmp = InternalInt2.x.CompareTo(other.InternalInt2.x);
                    return cmp != 0 ? cmp : InternalInt2.y.CompareTo(other.InternalInt2.y);
                }
                case EntryType.Int3:
                {
                    var cmp = InternalInt3.x.CompareTo(other.InternalInt3.x);
                    if (cmp != 0) return cmp;
                    cmp = InternalInt3.y.CompareTo(other.InternalInt3.y);
                    return cmp != 0 ? cmp : InternalInt3.z.CompareTo(other.InternalInt3.z);
                }
                case EntryType.Int64:   return InternalInt64.CompareTo(other.InternalInt64);
                case EntryType.UInt:    return InternalUInt.CompareTo(other.InternalUInt);
                case EntryType.UInt2:
                {
                    var cmp = InternalUInt2.x.CompareTo(other.InternalUInt2.x);
                    return cmp != 0 ? cmp : InternalUInt2.y.CompareTo(other.InternalUInt2.y);
                }
                case EntryType.UInt3:
                {
                    var cmp = InternalUInt3.x.CompareTo(other.InternalUInt3.x);
                    if (cmp != 0) return cmp;
                    cmp = InternalUInt3.y.CompareTo(other.InternalUInt3.y);
                    return cmp != 0 ? cmp : InternalUInt3.z.CompareTo(other.InternalUInt3.z);
                }
                case EntryType.UInt64:  return InternalUInt64.CompareTo(other.InternalUInt64);
                case EntryType.Short:   return InternalShort.CompareTo(other.InternalShort);
                case EntryType.UShort:  return InternalUShort.CompareTo(other.InternalUShort);
                case EntryType.Byte:    return InternalByte.CompareTo(other.InternalByte);
                case EntryType.SByte:   return InternalSByte.CompareTo(other.InternalSByte);
                case EntryType.Float:   return InternalFloat.CompareTo(other.InternalFloat);
                case EntryType.Float2:
                {
                    var cmp = InternalFloat2.x.CompareTo(other.InternalFloat2.x);
                    return cmp != 0 ? cmp : InternalFloat2.y.CompareTo(other.InternalFloat2.y);
                }
                case EntryType.Float3:
                {
                    var cmp = InternalFloat3.x.CompareTo(other.InternalFloat3.x);
                    if (cmp != 0) return cmp;
                    cmp = InternalFloat3.y.CompareTo(other.InternalFloat3.y);
                    return cmp != 0 ? cmp : InternalFloat3.z.CompareTo(other.InternalFloat3.z);
                }
                case EntryType.Double:      return InternalDouble.CompareTo(other.InternalDouble);
                case EntryType.Char:        return InternalChar.CompareTo(other.InternalChar);
                case EntryType.String:      return string.Compare(InternalString, other.InternalString, StringComparison.Ordinal);
                case EntryType.Bool:        return InternalBool.CompareTo(other.InternalBool);
                case EntryType.EntityType:  return String.CompareOrdinal(InternalEntityType.TextId, other.InternalEntityType.TextId);
                case EntryType.EntityLevel: return InternalEntityLevel.CompareTo(other.InternalEntityLevel);
                case EntryType.WeekDay:     return InternalWeekDay.CompareTo(other.InternalWeekDay);
                //@formatter:on
            }
            throw new ArgumentOutOfRangeException(nameof(Type), Type, "Unknown EntryType in CompareTo — did you forget to add a case for a new type?");
        }
        
        #endregion

        #region FromString

        public static bool TryParse(EntryType entryType, string text, out Entry entry)
        {
            switch (entryType)
            {
                //@formatter:off
                case EntryType.Invalid: break;
                case EntryType.Int: if (int.TryParse(text, out var intValue)) { entry = new Entry(intValue); return true; } break;
                case EntryType.Int2:
                {
                    var parts = text.Split(',');
                    if (parts.Length == 2 && int.TryParse(parts[0].Trim(), out var i2x) && int.TryParse(parts[1].Trim(), out var i2y))
                    { entry = new Entry(new int2(i2x, i2y)); return true; }
                    break;
                }
                case EntryType.Int3:
                {
                    var parts = text.Split(',');
                    if (parts.Length == 3 && int.TryParse(parts[0].Trim(), out var i3x) && int.TryParse(parts[1].Trim(), out var i3y) && int.TryParse(parts[2].Trim(), out var i3z))
                    { entry = new Entry(new int3(i3x, i3y, i3z)); return true; }
                    break;
                }
                case EntryType.Int64: if (Int64.TryParse(text, out var int64Value)) { entry = new Entry(int64Value); return true; } break;
                case EntryType.UInt: if (uint.TryParse(text, out var uintValue)) { entry = new Entry(uintValue); return true; } break;
                case EntryType.UInt2:
                {
                    var parts = text.Split(',');
                    if (parts.Length == 2 && uint.TryParse(parts[0].Trim(), out var u2x) && uint.TryParse(parts[1].Trim(), out var u2y))
                    { entry = new Entry(new uint2(u2x, u2y)); return true; }
                    break;
                }
                case EntryType.UInt3:
                {
                    var parts = text.Split(',');
                    if (parts.Length == 3 && uint.TryParse(parts[0].Trim(), out var u3x) && uint.TryParse(parts[1].Trim(), out var u3y) && uint.TryParse(parts[2].Trim(), out var u3z))
                    { entry = new Entry(new uint3(u3x, u3y, u3z)); return true; }
                    break;
                }
                case EntryType.UInt64: if (UInt64.TryParse(text, out var uInt64Value)) { entry = new Entry(uInt64Value); return true; } break;
                case EntryType.Short: if (short.TryParse(text, out var shortValue)) { entry = new Entry(shortValue); return true; } break;
                case EntryType.UShort: if (ushort.TryParse(text, out var ushortValue)) { entry = new Entry(ushortValue); return true; } break;
                case EntryType.Byte: if (byte.TryParse(text, out var byteValue)) { entry = new Entry(byteValue); return true; } break;
                case EntryType.SByte: if (sbyte.TryParse(text, out var sbyteValue)) { entry = new Entry(sbyteValue); return true; } break;
                case EntryType.Float: if (float.TryParse(text, out var floatValue)) { entry = new Entry(floatValue); return true; } break;
                case EntryType.Float2:
                {
                    var parts = text.Split(',');
                    if (parts.Length == 2 && float.TryParse(parts[0].Trim(), out var f2x) && float.TryParse(parts[1].Trim(), out var f2y))
                    { entry = new Entry(new float2(f2x, f2y)); return true; }
                    break;
                }
                case EntryType.Float3:
                {
                    var parts = text.Split(',');
                    if (parts.Length == 3 && float.TryParse(parts[0].Trim(), out var f3x) && float.TryParse(parts[1].Trim(), out var f3y) && float.TryParse(parts[2].Trim(), out var f3z))
                    { entry = new Entry(new float3(f3x, f3y, f3z)); return true; }
                    break;
                }
                case EntryType.Double: if (double.TryParse(text, out var doubleValue)) { entry = new Entry(doubleValue); return true; } break;
                case EntryType.Char: if (char.TryParse(text, out var charValue)) { entry = new Entry(charValue); return true; } break;
                case EntryType.String: entry = new Entry(text); return true;
                case EntryType.Bool: if (bool.TryParse(text, out var boolValue)) { entry = new Entry(boolValue); return true; } break;
                case EntryType.EntityType: if (EntityType.TryParse(text, out var entityType)) { entry = new Entry(entityType); return true; } break;
                case EntryType.EntityLevel: if (EntityLevel.TryParse(text, out var entityLevel)) { entry = new Entry(entityLevel); return true; } break;
                case EntryType.WeekDay: if (Enum.TryParse(text, out WeekDay weekDay)) { entry = new Entry(weekDay); return true; } break;
                
                //@formatter:on
                default: throw new ArgumentOutOfRangeException(nameof(entryType), entryType, null);
            }

            entry = Invalid;
            return false;
        }
        
        #endregion

        #region ToString

        public string ToStringWithType()
        {
            return ZString.Concat(GetTypePrefix(Type),ToString());
        }
        
        public void ToStringWithType(ref Utf16ValueStringBuilder stringBuilder)
        {
            stringBuilder.Append(GetTypePrefix(Type));
            ToString(ref stringBuilder);
        }

        public override string ToString()
        {
            switch (Type)
            {
                //@formatter:off
                case EntryType.Invalid: return string.Empty;
                case EntryType.Int: return InternalInt.ToString();
                case EntryType.Int2: return InternalInt2.ToString();
                case EntryType.Int3: return InternalInt3.ToString();
                case EntryType.Int64: return InternalInt64.ToString();
                case EntryType.UInt: return InternalUInt.ToString();
                case EntryType.UInt2: return InternalUInt2.ToString();
                case EntryType.UInt3: return InternalUInt3.ToString();
                case EntryType.UInt64: return InternalUInt64.ToString();
                case EntryType.Short: return InternalShort.ToString();
                case EntryType.UShort: return InternalUShort.ToString();
                case EntryType.Byte: return InternalByte.ToString();
                case EntryType.SByte: return InternalSByte.ToString();
                case EntryType.Float: return InternalFloat.ToString();
                case EntryType.Float2: return InternalFloat2.ToString();
                case EntryType.Float3: return InternalFloat3.ToString();
                case EntryType.Double: return InternalDouble.ToString();
                case EntryType.Char: return InternalChar.ToString();
                case EntryType.String: return InternalString;
                case EntryType.Bool: return InternalBool.ToString();
                case EntryType.EntityType: return InternalEntityType.TextId;
                case EntryType.EntityLevel: return InternalEntityLevel.ToString();
                case EntryType.WeekDay: return InternalWeekDay.ToString();
                //@formatter:on
                
                default: throw new ArgumentOutOfRangeException();
            }
        }
        
		public void ToString(ref Utf16ValueStringBuilder stringBuilder)
		{
			switch (Type)
			{
				// @formatter:off
				   case EntryType.Invalid                         : stringBuilder.Append(string.Empty                            ); break;
				// case EntryType.AnyType                         : stringBuilder.Append(                                        ); break; Not meaningful in this context. It's just an abstraction.
				   case EntryType.Int                             : stringBuilder.Append(InternalInt                             ); break;
				   case EntryType.Int2                            : stringBuilder.Append(InternalInt2                            ); break;
				   case EntryType.Int3                            : stringBuilder.Append(InternalInt3                            ); break;
				   case EntryType.Int64                           : stringBuilder.Append(InternalInt64                           ); break;
				   case EntryType.UInt                            : stringBuilder.Append(InternalUInt                            ); break;
				   case EntryType.UInt2                           : stringBuilder.Append(InternalUInt2                           ); break;
				   case EntryType.UInt3                           : stringBuilder.Append(InternalUInt3                           ); break;
				   case EntryType.UInt64                          : stringBuilder.Append(InternalUInt64                          ); break;
				   case EntryType.Short                           : stringBuilder.Append(InternalShort                           ); break;
				   case EntryType.UShort                          : stringBuilder.Append(InternalUShort                          ); break;
				   case EntryType.Byte                            : stringBuilder.Append(InternalByte                            ); break;
				   case EntryType.SByte                           : stringBuilder.Append(InternalSByte                           ); break;
				   case EntryType.Float                           : stringBuilder.Append(InternalFloat                           ); break;
				   case EntryType.Float2                          : stringBuilder.Append(InternalFloat2                          ); break;
				   case EntryType.Float3                          : stringBuilder.Append(InternalFloat3                          ); break;
				   case EntryType.Double                          : stringBuilder.Append(InternalDouble                          ); break;
				   case EntryType.Char                            : stringBuilder.Append(InternalChar                            ); break;
				   case EntryType.String                          : stringBuilder.Append(InternalString                          ); break;
				   case EntryType.Bool                            : stringBuilder.Append(InternalBool                            ); break;
				   case EntryType.EntityType                      : stringBuilder.Append(InternalEntityType                      .TextId); break;
				   case EntryType.EntityLevel                     : stringBuilder.Append(InternalEntityLevel                     ); break;
				   case EntryType.WeekDay                         : stringBuilder.Append(InternalWeekDay                         ); break;
				// case EntryType.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA: stringBuilder.Append(InternalAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA WhatToDoHere); break;
				// @formatter:on // See 11864343 to modify supported data types.

				default:
					throw new ArgumentOutOfRangeException();
			}
		}

        private const string PrefixSeparator = "| ";
        private static string GetTypePrefix(EntryType entryType)
        {
            switch (entryType)
            {
                //@formatter:off
                case EntryType.Invalid: return string.Concat(nameof(EntryType.Invalid), PrefixSeparator);
                case EntryType.Int: return string.Concat(nameof(EntryType.Int), PrefixSeparator);
                case EntryType.Int2: return string.Concat(nameof(EntryType.Int2), PrefixSeparator);
                case EntryType.Int3: return string.Concat(nameof(EntryType.Int3), PrefixSeparator);
                case EntryType.Int64: return string.Concat(nameof(EntryType.Int64), PrefixSeparator);
                case EntryType.UInt: return string.Concat(nameof(EntryType.UInt), PrefixSeparator);
                case EntryType.UInt2: return string.Concat(nameof(EntryType.UInt2), PrefixSeparator);
                case EntryType.UInt3: return string.Concat(nameof(EntryType.UInt3), PrefixSeparator);
                case EntryType.UInt64: return string.Concat(nameof(EntryType.UInt64), PrefixSeparator);
                case EntryType.Short: return string.Concat(nameof(EntryType.Short), PrefixSeparator);
                case EntryType.UShort: return string.Concat(nameof(EntryType.UShort), PrefixSeparator);
                case EntryType.Byte: return string.Concat(nameof(EntryType.Byte), PrefixSeparator);
                case EntryType.SByte: return string.Concat(nameof(EntryType.SByte), PrefixSeparator);
                case EntryType.Float: return string.Concat(nameof(EntryType.Float), PrefixSeparator);
                case EntryType.Float2: return string.Concat(nameof(EntryType.Float2), PrefixSeparator);
                case EntryType.Float3: return string.Concat(nameof(EntryType.Float3), PrefixSeparator);
                case EntryType.Double: return string.Concat(nameof(EntryType.Double), PrefixSeparator);
                case EntryType.Char: return string.Concat(nameof(EntryType.Char), PrefixSeparator);
                case EntryType.String: return string.Concat(nameof(EntryType.String), PrefixSeparator);
                case EntryType.Bool: return string.Concat(nameof(EntryType.Bool), PrefixSeparator);
                case EntryType.EntityType: return string.Concat(nameof(EntryType.EntityType), PrefixSeparator);
                case EntryType.EntityLevel: return string.Concat(nameof(EntryType.EntityLevel), PrefixSeparator);
                case EntryType.WeekDay: return string.Concat(nameof(EntryType.WeekDay), PrefixSeparator);
                //@formatter:on
                
                default: throw new ArgumentOutOfRangeException();
            }
        }
        
        #endregion

        #region Boxing and Unboxing
        
        public object ToObject()
        {
            switch (Type)
            {
                //@formatter:off
                case EntryType.Invalid: return default;
                case EntryType.Int: return InternalInt;
                case EntryType.Int2: return InternalInt2;
                case EntryType.Int3: return InternalInt3;
                case EntryType.Int64: return InternalInt64;
                case EntryType.UInt: return InternalUInt;
                case EntryType.UInt2: return InternalUInt2;
                case EntryType.UInt3: return InternalUInt3;
                case EntryType.UInt64: return InternalUInt64;
                case EntryType.Short: return InternalShort;
                case EntryType.UShort: return InternalUShort;
                case EntryType.Byte: return InternalByte;
                case EntryType.SByte: return InternalSByte;
                case EntryType.Float: return InternalFloat;
                case EntryType.Float2: return InternalFloat2;
                case EntryType.Float3: return InternalFloat3;
                case EntryType.Double: return InternalDouble;
                case EntryType.Char: return InternalChar;
                case EntryType.String: return InternalString;
                case EntryType.Bool: return InternalBool;
                case EntryType.EntityType: return InternalEntityType;
                case EntryType.EntityLevel: return InternalEntityLevel;
                case EntryType.WeekDay: return InternalWeekDay;
                //@formatter:on

                default: throw new ArgumentOutOfRangeException();
            }
        }

        public static Entry FromObject(object obj)
        {
            var objectType = obj.GetType();
            var mappedType = EntryTypeTools.SystemTypeToEntryTypeMap[objectType];

            switch (mappedType)
            {
                //@formatter:off
                case EntryType.Int: return new Entry((int)obj);
                case EntryType.Int2: return new Entry((int2)obj);
                case EntryType.Int3: return new Entry((int3)obj);
                case EntryType.Int64: return new Entry((Int64)obj);
                case EntryType.UInt: return new Entry((uint)obj);
                case EntryType.UInt2: return new Entry((uint2)obj);
                case EntryType.UInt3: return new Entry((uint3)obj);
                case EntryType.UInt64: return new Entry((UInt64)obj);
                case EntryType.Short: return new Entry((short)obj);
                case EntryType.UShort: return new Entry((ushort)obj);
                case EntryType.Byte: return new Entry((byte)obj);
                case EntryType.SByte: return new Entry((sbyte)obj);
                case EntryType.Float: return new Entry((float)obj);
                case EntryType.Float2: return new Entry((float2)obj);
                case EntryType.Float3: return new Entry((float3)obj);
                case EntryType.Double: return new Entry((double)obj);
                case EntryType.Char: return new Entry((char)obj);
                case EntryType.String: return new Entry((string)obj);
                case EntryType.Bool: return new Entry((bool)obj);
                case EntryType.EntityType: return new Entry((EntityType)obj);
                case EntryType.EntityLevel: return new Entry((EntityLevel)obj);
                case EntryType.WeekDay: return new Entry((WeekDay)obj);
                //@formatter:on
                
                default: throw new ArgumentOutOfRangeException();
            }
        }

        #endregion

        #region Conversions

        public object ImplicitlyConvertValueToSpecifiedSystemObject(Type resultingTypeAsObject)
        {
            if (TryImplicitlyConvertValueToSpecifiedSystemObject(resultingTypeAsObject, out object value))
                return value;
            //throw new DataConversionException($"Cannot convert Entry of type {Type} to {resultingTypeAsObject}");   
            throw new Exception($"Cannot convert Entry of type {Type} to {resultingTypeAsObject}");   
        }

        public bool TryImplicitlyConvertValueToSpecifiedSystemObject(Type resultingTypeAsObject, out object value)
        {
            if(EntryTypeTools.SystemTypeToEntryTypeMap.TryGetValue(resultingTypeAsObject, out EntryType resultingType))
            {
                var converted = TryImplicitlyConvertValueToSpecifiedEntryType(resultingType, out Entry entry);
                if (converted)
                {
                    value = entry;
                    return true;
                }
            }
            value = default;
            return false;
        }

        private bool TryImplicitlyConvertValueToSpecifiedEntryType(EntryType resultingType, out Entry entry)
        {
            if (resultingType == Type)
            {
                entry = this;
                return true;
            }

            if (Type == EntryType.String)
            {
                switch (resultingType)
                {
                    case EntryType.EntityType: entry = new Entry(EntryManager.DataIdentifierRegistry.GetEntityType(InternalString)); return true;
                    case EntryType.WeekDay: if(Enum.TryParse<WeekDay>(InternalString, out var valueAsEnum)) {entry = new Entry(valueAsEnum); return true;} break;
                }
            }
            
            if(resultingType == EntryType.String)
            {
                switch (Type)
                {
                    case EntryType.EntityType: entry = new Entry(EntryManager.DataIdentifierRegistry.GetEntityType(InternalString)); return true;
                    case EntryType.WeekDay: if(Enum.TryParse<WeekDay>(InternalString, out var valueAsEnum)) {entry = new Entry(valueAsEnum); return true;} break;
                }
            }
            
            entry = default;
            return false;
        }

        public bool TryConvertValueToBool(out bool value, bool logOnError)
        {
            switch (Type)
            {
                //@formatter:off
                case EntryType.Invalid: break;
                case EntryType.Int: value = InternalInt != 0; return true;
                case EntryType.Int2: value = InternalInt2.x != 0 || InternalInt2.y != 0; return true;
                case EntryType.Int3: value = InternalInt3.x != 0 || InternalInt3.y != 0 || InternalInt3.z != 0; return true;
                case EntryType.Int64: value = InternalInt64 != 0; return true;
                case EntryType.UInt: value = InternalUInt != 0; return true;
                case EntryType.UInt2: value = InternalUInt2.x != 0 || InternalUInt2.y != 0; return true;
                case EntryType.UInt3: value = InternalUInt3.x != 0 || InternalUInt3.y != 0 || InternalUInt3.z != 0; return true;
                case EntryType.UInt64: value = InternalUInt64 != 0; return true;
                case EntryType.Short: value = InternalShort != 0; return true;
                case EntryType.UShort: value = InternalUShort != 0; return true;
                case EntryType.Byte: value = InternalByte != 0; return true;
                case EntryType.SByte: value = InternalSByte != 0; return true;
                case EntryType.Float: value = InternalFloat != 0; return true;
                case EntryType.Float2: value = InternalFloat2.x != 0 || InternalFloat2.y != 0; return true;
                case EntryType.Float3: value = InternalFloat3.x != 0 || InternalFloat3.y != 0 || InternalFloat3.z != 0; return true;
                case EntryType.Double: value = InternalDouble != 0; return true;
                case EntryType.Char: value = InternalChar != 0; return true;
                case EntryType.String: return StringToBool(InternalString, out value);
                case EntryType.Bool: value = InternalBool; return true;
                case EntryType.EntityType: break;
                case EntryType.EntityLevel: break;
                //@formatter:on
                
                default: throw new ArgumentOutOfRangeException();
            }
            
            if (logOnError && Type != EntryType.Invalid)
            {
                throw new Exception($"Cannot convert Entry of type {Type} to bool");
            }
            value = default;
            return false;

            bool StringToBool(string text, out bool value)
            {
                switch (text.Length)
                {
                    case 1: 
                        if(text[0] == '1') {value = true; return true;}
                        else if(text[0] == '0') {value = false; return true;}
                        break;
                    case 4:
                        if (text.Equals("True") || text.Equals("true")){value = true; return true;}
                        break;
                    case 5:
                        if (text.Equals("False") || text.Equals("false")){value = false; return true;}
                        break;
                }
                value = default;
                return false;
            }
        }

        public bool TryConvertValueToInt(out int value, bool logOnError)
        {
            switch (Type)
            {
                //@formatter:off
                case EntryType.Invalid: break;
                case EntryType.Int: value = InternalInt; return true;
                case EntryType.Int2: break;
                case EntryType.Int3: break;
                case EntryType.Int64: value = (int)InternalInt64; return true;
                case EntryType.UInt: value = (int)InternalUInt; return true;
                case EntryType.UInt2: break;
                case EntryType.UInt3: break;
                case EntryType.UInt64: break;
                case EntryType.Short: value = InternalShort; return true;
                case EntryType.UShort: value = InternalUShort; return true;
                case EntryType.Byte: value = InternalByte; return true;
                case EntryType.SByte: value = InternalSByte; return true;
                case EntryType.Float: value = (int)InternalFloat; return true;
                case EntryType.Float2: break;
                case EntryType.Float3: break;
                case EntryType.Double: value = (int)InternalDouble; return true;
                case EntryType.Char: value = InternalChar; return true;
                case EntryType.String: if (int.TryParse(InternalString, out value)) return true; break;
                case EntryType.Bool: value = InternalBool ? 1 : 0; return true;
                case EntryType.EntityType: break;
                case EntryType.EntityLevel: break;
                case EntryType.WeekDay: value = (int)InternalWeekDay; return true;
                //@formatter:on
                
                default: throw new ArgumentOutOfRangeException();
            }

            if (logOnError && Type != EntryType.Invalid)
            {
                throw new Exception($"Cannot convert Entry of type {Type} to int");
            }
            value = default;
            return false;
        }

        public bool TryConvertValueToFloat(out float value, bool logOnError)
        {
            switch (Type)
            {
                //@formatter:off
                case EntryType.Invalid: break;
                case EntryType.Int: value = (float)InternalInt; return true;
                case EntryType.Int2: break;
                case EntryType.Int3: break;
                case EntryType.Int64: value = (float)InternalInt64; return true;
                case EntryType.UInt: value = (float)InternalUInt; return true;
                case EntryType.UInt2: break;
                case EntryType.UInt3: break;
                case EntryType.UInt64: break;
                case EntryType.Short: value = (float)InternalShort; return true;
                case EntryType.UShort: value = (float)InternalUShort; return true;
                case EntryType.Byte: value = (float)InternalByte; return true;
                case EntryType.SByte: value = (float)InternalSByte; return true;
                case EntryType.Float: value = InternalFloat; return true;
                case EntryType.Float2: break;
                case EntryType.Float3: break;
                case EntryType.Double: value = (float)InternalDouble; return true;
                case EntryType.Char: break;
                case EntryType.String: if (float.TryParse(InternalString, out value)) return true; break;
                case EntryType.Bool: value = InternalBool ? 1f : 0f; return true;
                case EntryType.EntityType: break;
                case EntryType.EntityLevel: break;
                case EntryType.WeekDay: break;
                //@formatter:on
                
                default: throw new ArgumentOutOfRangeException();
            }
            
            if (logOnError && Type != EntryType.Invalid)
            {
                throw new Exception($"Cannot convert Entry of type {Type} to float");
            }
            value = default;
            return false;
        }
        
        public bool TryConvertValueToDouble(out double value, bool logOnError)
        {
            switch (Type)
            {
                //@formatter:off
                case EntryType.Invalid: break;
                case EntryType.Int: value = (double)InternalInt; return true;
                case EntryType.Int2: break;
                case EntryType.Int3: break;
                case EntryType.Int64: value = (double)InternalInt64; return true;
                case EntryType.UInt: value = (double)InternalUInt; return true;
                case EntryType.UInt2: break;
                case EntryType.UInt3: break;
                case EntryType.UInt64: break;
                case EntryType.Short: value = (double)InternalShort; return true;
                case EntryType.UShort: value = (double)InternalUShort; return true;
                case EntryType.Byte: value = (double)InternalByte; return true;
                case EntryType.SByte: value = (double)InternalSByte; return true;
                case EntryType.Float: value = InternalFloat; return true;
                case EntryType.Float2: break;
                case EntryType.Float3: break;
                case EntryType.Double: value = InternalDouble; return true;
                case EntryType.Char: break;
                case EntryType.String: if (double.TryParse(InternalString, out value)) return true; break;
                case EntryType.Bool: value = InternalBool ? 1d : 0d; return true;
                case EntryType.EntityType: break;
                case EntryType.EntityLevel: break;
                case EntryType.WeekDay: break;
                //@formatter:on
                
                default: throw new ArgumentOutOfRangeException();
            }
            
            if (logOnError && Type != EntryType.Invalid)
            {
                throw new Exception($"Cannot convert Entry of type {Type} to double");
            }
            value = default;
            return false;
        }
        
        //----------------------------------------------------------------------

        public bool ConvertValueToBool()
        {
            if (TryConvertValueToBool(out var value, false))
                return value;
            //throw new DataConversionException($"Tried to convert {Type} to bool but failed");
            throw new Exception($"Tried to convert {Type} to bool but failed");
        }

        public int ConvertValueToInt()
        {
            if (TryConvertValueToInt(out var value, false))
                return value;
            //throw new DataConversionException($"Tried to convert {Type} to bool but failed");
            throw new Exception($"Tried to convert {Type} to int but failed");
        }
        
        public float ConvertValueToFloat()
        {
            if (TryConvertValueToFloat(out var value, false))
                return value;
            //throw new DataConversionException($"Tried to convert {Type} to bool but failed");
            throw new Exception($"Tried to convert {Type} to float but failed");
        }        
        
        public double ConvertValueToDouble()
        {
            if (TryConvertValueToDouble(out var value, false))
                return value;
            //throw new DataConversionException($"Tried to convert {Type} to bool but failed");
            throw new Exception($"Tried to convert {Type} to double but failed");
        }
        
        #endregion
        
        #region IConvertible Implementation
        public TypeCode GetTypeCode()
        {
            switch (Type)
            {
                case EntryType.Invalid: return TypeCode.Empty;
                case EntryType.Int: return TypeCode.Int32;
                case EntryType.Int2: return TypeCode.Object;
                case EntryType.Int3: return TypeCode.Object;
                case EntryType.Int64: return TypeCode.Int64;
                case EntryType.UInt: return TypeCode.UInt32;
                case EntryType.UInt2: return TypeCode.Object;
                case EntryType.UInt3: return TypeCode.Object;
                case EntryType.UInt64: return TypeCode.UInt64;
                case EntryType.Short: return TypeCode.Int16;
                case EntryType.UShort: return TypeCode.UInt16;
                case EntryType.Byte: return TypeCode.Byte;
                case EntryType.SByte: return TypeCode.SByte;
                case EntryType.Float: return TypeCode.Single;
                case EntryType.Float2: return TypeCode.Object;
                case EntryType.Float3: return TypeCode.Object;
                case EntryType.Double: return TypeCode.Double;
                case EntryType.Char: return TypeCode.Char;
                case EntryType.String: return TypeCode.String;
                case EntryType.Bool: return TypeCode.Boolean;
                case EntryType.EntityType: return TypeCode.Object;
                case EntryType.EntityLevel: return TypeCode.Object;
                case EntryType.WeekDay: return TypeCode.Int32;
                default: throw new ArgumentOutOfRangeException();
            }
        }

        public bool ToBoolean(IFormatProvider provider)
        {
            if (Type == EntryType.Bool)
                return InternalBool;
            if (TryConvertValueToBool(out var value, true))
                return value;
            throw new InvalidCastException($"Cannot convert Entry of type {Type} to bool");
        }

        public byte ToByte(IFormatProvider provider)
        {
            if (Type == EntryType.Byte)
                return InternalByte;
            if (TryConvertValueToInt(out var intValue, true))
            {
                if (intValue >= byte.MinValue && intValue <= byte.MaxValue)
                    return (byte)intValue;
                throw new OverflowException($"Cannot convert Entry of type {Type} with value {intValue} to byte because it is out of range");
            }
            throw new InvalidCastException($"Cannot convert Entry of type {Type} to byte");
        }

        public char ToChar(IFormatProvider provider)
        {
            if (Type == EntryType.Char)
                return InternalChar;
            if (TryConvertValueToInt(out var intValue, true))
            {
                if (intValue >= char.MinValue && intValue <= char.MaxValue)
                    return (char)intValue;
                throw new OverflowException($"Cannot convert Entry of type {Type} with value {intValue} to char because it is out of range");
            }
            throw new InvalidCastException($"Cannot convert Entry of type {Type} to char");
        }

        public DateTime ToDateTime(IFormatProvider provider)
        {
            if(Type == EntryType.String)
            {
                if(DateTime.TryParse(InternalString, provider, DateTimeStyles.None, out var dateTime))
                    return dateTime;
                throw new InvalidCastException($"Cannot convert Entry of type {Type} with value {InternalString} to DateTime");
            }
            throw new InvalidCastException($"Cannot convert Entry of type {Type} to DateTime");
        }

        public decimal ToDecimal(IFormatProvider provider)
        {
            if (Type == EntryType.Double)
                return (decimal)InternalDouble;
            if (TryConvertValueToDouble(out var doubleValue, true))
            {
                if (doubleValue >= (double)decimal.MinValue && doubleValue <= (double)decimal.MaxValue)
                    return (decimal)doubleValue;
                throw new OverflowException($"Cannot convert Entry of type {Type} with value {doubleValue} to decimal because it is out of range");
            }
            throw new InvalidCastException($"Cannot convert Entry of type {Type} to decimal");
        }

        public double ToDouble(IFormatProvider provider)
        {
            if (Type == EntryType.Double)
                return InternalDouble;
            if (TryConvertValueToDouble(out var doubleValue, true))
                return doubleValue;
            throw new InvalidCastException($"Cannot convert Entry of type {Type} to double");
        }

        public short ToInt16(IFormatProvider provider)
        {
            if (Type == EntryType.Short)
                return InternalShort;
            if (TryConvertValueToInt(out var intValue, true))
            {
                if (intValue >= short.MinValue && intValue <= short.MaxValue)
                    return (short)intValue;
                throw new OverflowException($"Cannot convert Entry of type {Type} with value {intValue} to short because it is out of range");
            }
            throw new InvalidCastException($"Cannot convert Entry of type {Type} to short");
        }

        public int ToInt32(IFormatProvider provider)
        {
            if (Type == EntryType.Int)
                return InternalInt;
            if (TryConvertValueToInt(out var intValue, true))
                return intValue;
            throw new InvalidCastException($"Cannot convert Entry of type {Type} to int");
        }

        public long ToInt64(IFormatProvider provider)
        {
            if (Type == EntryType.Int64)
                return InternalInt64;
            if (TryConvertValueToInt(out var intValue, true))
                return intValue;
            throw new InvalidCastException($"Cannot convert Entry of type {Type} to long");
        }

        public sbyte ToSByte(IFormatProvider provider)
        {
            if (Type == EntryType.SByte)
                return InternalSByte;
            if (TryConvertValueToInt(out var intValue, true))
            {
                if (intValue >= sbyte.MinValue && intValue <= sbyte.MaxValue)
                    return (sbyte)intValue;
                throw new OverflowException($"Cannot convert Entry of type {Type} with value {intValue} to sbyte because it is out of range");
            }
            throw new InvalidCastException($"Cannot convert Entry of type {Type} to sbyte");
        }

        public float ToSingle(IFormatProvider provider)
        {
            if (Type == EntryType.Float)
                return InternalFloat;
            if (TryConvertValueToFloat(out var floatValue, true))
                return floatValue;
            throw new InvalidCastException($"Cannot convert Entry of type {Type} to float");
        }

        public string ToString(IFormatProvider provider)
        {
            if (Type == EntryType.String)
                return InternalString;
            return ToString();
        }

        public object ToType(Type conversionType, IFormatProvider provider)
        {
            if (TryImplicitlyConvertValueToSpecifiedSystemObject(conversionType, out var value))
                return value;
            throw new InvalidCastException($"Cannot convert Entry of type {Type} to {conversionType}");
        }

        public ushort ToUInt16(IFormatProvider provider)
        {
            if (Type == EntryType.UShort)
                return InternalUShort;
            if (TryConvertValueToInt(out var intValue, true))
            {
                if (intValue >= ushort.MinValue && intValue <= ushort.MaxValue)
                    return (ushort)intValue;
                throw new OverflowException($"Cannot convert Entry of type {Type} with value {intValue} to ushort because it is out of range");
            }
            throw new InvalidCastException($"Cannot convert Entry of type {Type} to ushort");
        }

        public uint ToUInt32(IFormatProvider provider)
        {
            if (Type == EntryType.UInt)
                return InternalUInt;
            if (TryConvertValueToInt(out var intValue, true))
            {
                if (intValue >= 0)
                    return (uint)intValue;
                throw new OverflowException($"Cannot convert Entry of type {Type} with value {intValue} to uint because it is out of range");
            }
            throw new InvalidCastException($"Cannot convert Entry of type {Type} to uint");
        }

        public ulong ToUInt64(IFormatProvider provider)
        {
            if (Type == EntryType.UInt64)
                return InternalUInt64;
            if (TryConvertValueToInt(out var intValue, true))
            {
                if (intValue >= 0)
                    return (ulong)intValue;
                throw new OverflowException($"Cannot convert Entry of type {Type} with value {intValue} to ulong because it is out of range");
            }
            throw new InvalidCastException($"Cannot convert Entry of type {Type} to ulong");
        }

        #endregion

        #region Equality

        public bool Equals(Entry other)
        {
            switch (Type)
            {
                case EntryType.String:
                    return Type == other.Type && String.Equals(InternalString, other.InternalString, StringComparison.Ordinal);
                default:
                    //Compare largest data type InternalInt3 3*4 = 12 bytes
                    return Type == other.Type && InternalInt3.Equals(other.InternalInt3);
            }
        }

        public override bool Equals(object other)
        {
            return other is Entry otherEntry && Equals(otherEntry);
        }
        
        public override int GetHashCode()
        {
            switch (Type)
            {
                //@formatter:off
                case EntryType.Invalid: return 0;
                case EntryType.Int: return (Type,InternalInt).GetHashCode();
                case EntryType.Int2: return (Type,InternalInt2).GetHashCode();
                case EntryType.Int3: return (Type,InternalInt3).GetHashCode();
                case EntryType.Int64: return (Type,InternalInt64).GetHashCode();
                case EntryType.UInt: return (Type,InternalUInt).GetHashCode();
                case EntryType.UInt2: return (Type,InternalUInt2).GetHashCode();
                case EntryType.UInt3: return (Type,InternalUInt3).GetHashCode();
                case EntryType.UInt64: return (Type,InternalUInt64).GetHashCode();
                case EntryType.Short: return (Type,InternalShort).GetHashCode();
                case EntryType.UShort: return (Type,InternalUShort).GetHashCode();
                case EntryType.Byte: return (Type,InternalByte).GetHashCode();
                case EntryType.SByte: return (Type,InternalSByte).GetHashCode();
                case EntryType.Float: return (Type,InternalFloat).GetHashCode();
                case EntryType.Float2: return (Type,InternalFloat2).GetHashCode();
                case EntryType.Float3: return (Type,InternalFloat3).GetHashCode();
                case EntryType.Double: return (Type,InternalDouble).GetHashCode();
                case EntryType.Char: return (Type,InternalChar).GetHashCode();
                case EntryType.String: return (Type,InternalString).GetHashCode();
                case EntryType.Bool: return (Type,InternalBool).GetHashCode();
                case EntryType.EntityType: return (Type,InternalEntityType).GetHashCode();
                case EntryType.EntityLevel: return (Type,InternalEntityLevel).GetHashCode();
                case EntryType.WeekDay: return (Type,InternalWeekDay).GetHashCode();
                //@formatter:on
                
                default: throw new ArgumentOutOfRangeException();
            }
        }

        #endregion
        
#if UNITY_EDITOR
        #region Serialization Checkers
        
        private bool IsShowInt => Type == EntryType.Int;
        private bool IsShowInt2 => Type == EntryType.Int2;
        private bool IsShowInt3 => Type == EntryType.Int3;
        private bool IsShowInt64 => Type == EntryType.Int64;
        private bool IsShowUInt => Type == EntryType.UInt;
        private bool IsShowUInt2 => Type == EntryType.UInt2;
        private bool IsShowUInt3 => Type == EntryType.UInt3;
        private bool IsShowUInt64 => Type == EntryType.UInt64;
        private bool IsShowShort => Type == EntryType.Short;
        private bool IsShowUshort => Type == EntryType.UShort;
        private bool IsShowByte => Type == EntryType.Byte;
        private bool IsShowSbyte => Type == EntryType.SByte;
        private bool IsShowFloat => Type == EntryType.Float;
        private bool IsShowFloat2 => Type == EntryType.Float2;
        private bool IsShowFloat3 => Type == EntryType.Float3;
        private bool IsShowDouble => Type == EntryType.Double;
        private bool IsShowChar => Type == EntryType.Char;
        private bool IsShowString => Type == EntryType.String;
        private bool IsShowBool => Type == EntryType.Bool;
        private bool IsShowEntityType => Type == EntryType.EntityType;
        private bool IsShowEntityLevel => Type == EntryType.EntityLevel;
        private bool IsShowWeekDay => Type == EntryType.WeekDay;
        
        #endregion
#endif

        #region Enum Conversion Methods

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryConvertFromEnum<TEnum, T>(TEnum enumValue, out T value)
            where TEnum : struct, Enum where T : struct,
            IConvertible, IFormattable, IComparable
        {
            if (UnsafeUtility.SizeOf<T>() == UnsafeUtility.SizeOf<TEnum>())
            {
                value = UnsafeUtility.As<TEnum, T>(ref enumValue);
                return true;
            }
            value = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryConvertToEnum<T, TEnum>(T value, out TEnum enumValue)
            where TEnum : struct, Enum where T : struct,
            IConvertible, IFormattable, IComparable
        {
            if (UnsafeUtility.SizeOf<T>() == UnsafeUtility.SizeOf<TEnum>())
            {
                enumValue = UnsafeUtility.As<T, TEnum>(ref value);
                return true;
            }
            enumValue = default;
            return false;
        }
        
        

        #endregion

        /*
        ╔══════════════════════════════════════════════════════════════════╗
        ║  NEW ENTRY TYPE TEMPLATE                                       ║
        ║  Copy-paste and find-replace to add a new type.                ║
        ║  Search "NewType" → your type name, "newtype" → lowercase.    ║
        ║  After adding, run EntryComparisonTests — guard tests will     ║
        ║  catch any missed location.                                    ║
        ╚══════════════════════════════════════════════════════════════════╝

        ── 1. EntryType.cs ──────────────────────────────────────────────

        // In enum EntryType:
        NewType = <next_int>,

        // In EntryTypeOrAnyType:
        NewType = EntryType.NewType,


        ── 2. Entry.cs — Data Field ─────────────────────────────────────

        // In #region Data Fields (FieldOffset 4 for value types, 16 for reference types):
        [FieldOffset(4), ShowInInspector, HideLabel, HorizontalGroup, ShowIf("IsShowNewType"), NonSerialized] internal NewTypeT InternalNewType;


        ── 3. Entry.cs — Accessor ───────────────────────────────────────

        // Value property:
        public NewTypeT ValueNewType
        {
            get { if (Type != EntryType.NewType) throw new InvalidCastException(...); return InternalNewType; }
            set { if (Type != EntryType.NewType) throw new InvalidCastException(...); InternalNewType = value; }
        }

        // TryGet + Get:
        public bool TryGetNewType(out NewTypeT value) { value = InternalNewType; return Type == EntryType.NewType; }
        public NewTypeT GetNewType() => Type == EntryType.NewType ? InternalNewType : throw new InvalidCastException();


        ── 4. Entry.cs — Constructor + Implicit ─────────────────────────

        public Entry(NewTypeT value):this() { Type = EntryType.NewType; InternalNewType = value; }
        public static implicit operator Entry(NewTypeT value) => new Entry(value);


        ── 5. Entry.cs — Switch Cases (add to each existing switch) ─────

        // CompareTo:   case EntryType.NewType: return InternalNewType.CompareTo(other.InternalNewType);
        // TryParse:    case EntryType.NewType: if (NewTypeT.TryParse(text, out var ntVal)) { entry = new Entry(ntVal); return true; } break;
        // ToString:    case EntryType.NewType: return InternalNewType.ToString();
        // GetTypePrefix: case EntryType.NewType: return "nt:";
        // ToObject:    case EntryType.NewType: return InternalNewType;
        // FromObject:  case EntryType.NewType: return new Entry((NewTypeT)value);
        // GetHashCode: case EntryType.NewType: return (Type,InternalNewType).GetHashCode();
        // GetTypeCode: case EntryType.NewType: return TypeCode.Object;

        // NOTE: Equals does NOT need a case — the InternalInt3 proxy covers all value types ≤12 bytes.
        //       If your type is >12 bytes or is a reference type, add an explicit Equals case like String.


        ── 6. Entry.cs — Editor ─────────────────────────────────────────

        // IsShow property (inside #if UNITY_EDITOR):
        private bool IsShowNewType => Type == EntryType.NewType;


        ── 7. EntryType.cs — EntryTypeTools ─────────────────────────────

        // EntryTypeInfoList:  info[(int) EntryType.NewType] = Components(EntryType.NewType);
        // EntryTypeToSystemTypeMap:  EntryTypeToSystemTypeMap[(int) EntryType.NewType] = typeof(NewTypeT);
        // SystemTypeToEntryTypeMap:  SystemTypeToEntryTypeMap[typeof(NewTypeT)] = EntryType.NewType;
        // TryParse:  case "NewType": entryType = EntryType.NewType; break;
        // Parse:     case "NewType": return EntryType.NewType;
        // ToString:  case EntryType.NewType: return "NewType";


        ── 8. EntryComparisonTests.cs ───────────────────────────────────

        // TestPairs:      (EntryType.NewType, new Entry(someValue1), new Entry(someValue2)),
        // ParseTestCases: (EntryType.NewType, "valueStr", new Entry(expectedValue)),
        // CreateTestEntry: case EntryType.NewType: return new Entry(testValue);

        */
    }
}