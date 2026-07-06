using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace PFound.GameDataStore.Entries
{
    public enum EntryType : int
    {
        Invalid = 0,
        Int = 2,
        Int2 = 3,
        Int3 = 4,
        Int64 = 5,
        UInt = 6,
        UInt2 = 7,
        UInt3 = 8,
        UInt64 = 9,
        Short = 10,
        UShort = 11,
        Byte = 12,
        SByte = 13,
        Float = 14,
        Float2 = 15,
        Float3 = 16,
        Double = 17,
        Char = 18,
        String = 19,
        Bool = 20,
        EntityType = 21,
        EntityLevel = 22,
        WeekDay = 23,
    }
    
    public enum EntryTypeOrAnyType : int
    {
        Invalid = EntryType.Invalid,
        AnyType = 1,
        Int = EntryType.Int,
        Int2 = EntryType.Int2,
        Int3 = EntryType.Int3,
        Int64 = EntryType.Int64,
        UInt = EntryType.UInt,
        UInt2 = EntryType.UInt2,
        UInt3 = EntryType.UInt3,
        UInt64 = EntryType.UInt64,
        Short = EntryType.Short,
        Ushort = EntryType.UShort,
        Byte = EntryType.Byte,
        Sbyte = EntryType.SByte,
        Float = EntryType.Float,
        Float2 = EntryType.Float2,
        Float3 = EntryType.Float3,
        Double = EntryType.Double,
        Char = EntryType.Char,
        String = EntryType.String,
        Bool = EntryType.Bool,
        EntityType = EntryType.EntityType,
        EntityLevel = EntryType.EntityLevel,
        WeekDay = EntryType.WeekDay,
    }

    public static class EntryTypeTools
    {
        public const int MaxValueOfEntryType = (int) EntryType.WeekDay;
        public const int EntryTypeRadixArrayLength = MaxValueOfEntryType + 1;

        public static readonly IReadOnlyList<EntryTypeInfo> EntryTypeInfoList;
        public static readonly EntryType[] AllEntryTypes;
        
        public static readonly Type[] EntryTypeToSystemTypeMap;
        public static readonly Dictionary<Type, EntryType> SystemTypeToEntryTypeMap;
        
        public static readonly HashSet<Type> SystemSupportsDataIdentifiers;
        public static readonly HashSet<EntryType> EntryTypeToDataIdentifierMap;
        
        static EntryTypeTools()
        {
            var info = new EntryTypeInfo[EntryTypeRadixArrayLength];
            
            info[(int) EntryType.Int] = Components(EntryType.Int);
            info[(int) EntryType.Int2] = Components(EntryType.Int2, (EntryType.Int, "x"), (EntryType.Int, "y"));
            info[(int) EntryType.Int3] = Components(EntryType.Int3, (EntryType.Int, "x"), (EntryType.Int, "y"), (EntryType.Int, "z"));
            info[(int) EntryType.Int64] = Components(EntryType.Int64);
            info[(int) EntryType.UInt] = Components(EntryType.UInt);
            info[(int) EntryType.UInt2] = Components(EntryType.UInt2, (EntryType.UInt, "x"), (EntryType.UInt, "y"));
            info[(int) EntryType.UInt3] = Components(EntryType.UInt3, (EntryType.UInt, "x"), (EntryType.UInt, "y"), (EntryType.UInt, "z"));
            info[(int) EntryType.UInt64] = Components(EntryType.UInt64);
            info[(int) EntryType.Short] = Components(EntryType.Short);
            info[(int) EntryType.UShort] = Components(EntryType.UShort);
            info[(int) EntryType.Byte] = Components(EntryType.Byte);
            info[(int) EntryType.SByte] = Components(EntryType.SByte);
            info[(int) EntryType.Float] = Components(EntryType.Float);
            info[(int) EntryType.Float2] = Components(EntryType.Float2, (EntryType.Float, "x"), (EntryType.Float, "y"));
            info[(int) EntryType.Float3] = Components(EntryType.Float3, (EntryType.Float, "x"), (EntryType.Float, "y"), (EntryType.Float, "z"));
            info[(int) EntryType.Double] = Components(EntryType.Double);
            info[(int) EntryType.Char] = Components(EntryType.Char);
            info[(int) EntryType.String] = Components(EntryType.String);
            info[(int) EntryType.Bool] = Components(EntryType.Bool);
            info[(int) EntryType.EntityType] = Components(EntryType.EntityType);
            info[(int) EntryType.EntityLevel] = Components(EntryType.EntityLevel);
            info[(int) EntryType.WeekDay] = Components(EntryType.WeekDay);
            
            
            EntryTypeInfoList = info;

            EntryTypeToSystemTypeMap = new Type[EntryTypeRadixArrayLength];
            EntryTypeToSystemTypeMap[(int) EntryType.Int] = typeof(int);
            EntryTypeToSystemTypeMap[(int) EntryType.Int2] = typeof(int2);
            EntryTypeToSystemTypeMap[(int) EntryType.Int3] = typeof(int3);
            EntryTypeToSystemTypeMap[(int) EntryType.Int64] = typeof(long);
            EntryTypeToSystemTypeMap[(int) EntryType.UInt] = typeof(uint);
            EntryTypeToSystemTypeMap[(int) EntryType.UInt2] = typeof(uint2);
            EntryTypeToSystemTypeMap[(int) EntryType.UInt3] = typeof(uint3);
            EntryTypeToSystemTypeMap[(int) EntryType.UInt64] = typeof(ulong);
            EntryTypeToSystemTypeMap[(int) EntryType.Short] = typeof(short);
            EntryTypeToSystemTypeMap[(int) EntryType.UShort] = typeof(ushort);
            EntryTypeToSystemTypeMap[(int) EntryType.Byte] = typeof(byte);
            EntryTypeToSystemTypeMap[(int) EntryType.SByte] = typeof(sbyte);
            EntryTypeToSystemTypeMap[(int) EntryType.Float] = typeof(float);
            EntryTypeToSystemTypeMap[(int) EntryType.Float2] = typeof(float2);
            EntryTypeToSystemTypeMap[(int) EntryType.Float3] = typeof(float3);
            EntryTypeToSystemTypeMap[(int) EntryType.Double] = typeof(double);
            EntryTypeToSystemTypeMap[(int) EntryType.Char] = typeof(char);
            EntryTypeToSystemTypeMap[(int) EntryType.String] = typeof(string);
            EntryTypeToSystemTypeMap[(int) EntryType.Bool] = typeof(bool);
            EntryTypeToSystemTypeMap[(int) EntryType.EntityType] = typeof(EntityType);
            EntryTypeToSystemTypeMap[(int) EntryType.EntityLevel] = typeof(EntityLevel);
            EntryTypeToSystemTypeMap[(int) EntryType.WeekDay] = typeof(WeekDay);

            SystemTypeToEntryTypeMap = new Dictionary<Type, EntryType>(EntryTypeRadixArrayLength * 4); // 4 is a magic number to increase access speed.
            SystemTypeToEntryTypeMap[typeof(int)] = EntryType.Int;
            SystemTypeToEntryTypeMap[typeof(int2)] = EntryType.Int2;
            SystemTypeToEntryTypeMap[typeof(int3)] = EntryType.Int3;
            SystemTypeToEntryTypeMap[typeof(long)] = EntryType.Int64;
            SystemTypeToEntryTypeMap[typeof(uint)] = EntryType.UInt;
            SystemTypeToEntryTypeMap[typeof(uint2)] = EntryType.UInt2;
            SystemTypeToEntryTypeMap[typeof(uint3)] = EntryType.UInt3;
            SystemTypeToEntryTypeMap[typeof(ulong)] = EntryType.UInt64;
            SystemTypeToEntryTypeMap[typeof(short)] = EntryType.Short;
            SystemTypeToEntryTypeMap[typeof(ushort)] = EntryType.UShort;
            SystemTypeToEntryTypeMap[typeof(byte)] = EntryType.Byte;
            SystemTypeToEntryTypeMap[typeof(sbyte)] = EntryType.SByte;
            SystemTypeToEntryTypeMap[typeof(float)] = EntryType.Float;
            SystemTypeToEntryTypeMap[typeof(float2)] = EntryType.Float2;
            SystemTypeToEntryTypeMap[typeof(float3)] = EntryType.Float3;
            SystemTypeToEntryTypeMap[typeof(double)] = EntryType.Double;
            SystemTypeToEntryTypeMap[typeof(char)] = EntryType.Char;
            SystemTypeToEntryTypeMap[typeof(string)] = EntryType.String;
            SystemTypeToEntryTypeMap[typeof(bool)] = EntryType.Bool;
            SystemTypeToEntryTypeMap[typeof(EntityType)] = EntryType.EntityType;
            SystemTypeToEntryTypeMap[typeof(EntityLevel)] = EntryType.EntityLevel;
            SystemTypeToEntryTypeMap[typeof(WeekDay)] = EntryType.WeekDay;
            
            var list = new List<EntryType>(info.Length);
            foreach (var entry in Enum.GetValues(typeof(EntryType)))
            {
                var value = (EntryType)entry;
                if(value>0) list.Add(value);
            }
            AllEntryTypes = list.ToArray();

            //For IDataIdentifiers
            SystemSupportsDataIdentifiers = new HashSet<Type>();
            EntryTypeToDataIdentifierMap = new HashSet<EntryType>();

            foreach (var entry in SystemTypeToEntryTypeMap)
            {
                if (entry.Key.IsSubclassOf(typeof(IDataIdentifier)))
                {
                    SystemSupportsDataIdentifiers.Add(entry.Key);
                    EntryTypeToDataIdentifierMap.Add(entry.Value);
                }
            }
        }
        
        public static Type GetSystemType(this EntryType entryType) => EntryTypeToSystemTypeMap[(int) entryType];
        public static EntryType GetEntryType(this Type systemType) => SystemTypeToEntryTypeMap[systemType];
        public static bool TryGetEntryType(this Type systemType, out EntryType entryType) => SystemTypeToEntryTypeMap.TryGetValue(systemType, out entryType);
        public static bool IsEntryType(this Type systemType) => SystemTypeToEntryTypeMap.ContainsKey(systemType);
        
        
        public static EntryTypeOrAnyType ToEntryTypeOrAnyType(this EntryType entryType) => (EntryTypeOrAnyType) entryType;
        public static EntryType ToEntryType(this EntryTypeOrAnyType entryType)
        {
            if(entryType == EntryTypeOrAnyType.AnyType) throw new InvalidCastException("AnyType cannot be converted to EntryType");
            return (EntryType) entryType;
        }
        
        
        public static bool TryParse(string entryTypeString, out EntryType entryType)
        {
            switch (entryTypeString)
            {
                case "Invalid": entryType = EntryType.Invalid; break;
                case "Int": entryType = EntryType.Int; break;
                case "Int2": entryType = EntryType.Int2; break;
                case "Int3": entryType = EntryType.Int3; break;
                case "Int64": entryType = EntryType.Int64; break;
                case "UInt": entryType = EntryType.UInt; break;
                case "UInt2": entryType = EntryType.UInt2; break;
                case "UInt3": entryType = EntryType.UInt3; break;
                case "UInt64": entryType = EntryType.UInt64; break;
                case "Short": entryType = EntryType.Short; break;
                case "UShort": entryType = EntryType.UShort; break;
                case "Byte": entryType = EntryType.Byte; break;
                case "Sbyte": entryType = EntryType.SByte; break;
                case "Float": entryType = EntryType.Float; break;
                case "Float2": entryType = EntryType.Float2; break;
                case "Float3": entryType = EntryType.Float3; break;
                case "Double": entryType = EntryType.Double; break;
                case "Char": entryType = EntryType.Char; break;
                case "String": entryType = EntryType.String; break;
                case "Bool": entryType = EntryType.Bool; break;
                case "EntityType": entryType = EntryType.EntityType; break;
                case "EntityLevel": entryType = EntryType.EntityLevel; break;
                case "WeekDay": entryType = EntryType.WeekDay; break;
                
                
                default: entryType = EntryType.Invalid; return false;
            }
            return true;    
        }
        
        public static EntryType Parse(string entryTypeString)
        {
            switch (entryTypeString)
            {
                case "Invalid": return EntryType.Invalid;
                case "Int": return EntryType.Int;
                case "Int2": return EntryType.Int2;
                case "Int3": return EntryType.Int3;
                case "Int64": return EntryType.Int64;
                case "UInt": return EntryType.UInt;
                case "UInt2": return EntryType.UInt2;
                case "UInt3": return EntryType.UInt3;
                case "UInt64": return EntryType.UInt64;
                case "Short": return EntryType.Short;
                case "UShort": return EntryType.UShort;
                case "Byte": return EntryType.Byte;
                case "Sbyte": return EntryType.SByte;
                case "Float": return EntryType.Float;
                case "Float2": return EntryType.Float2;
                case "Float3": return EntryType.Float3;
                case "Double": return EntryType.Double;
                case "Char": return EntryType.Char;
                case "String": return EntryType.String;
                case "Bool": return EntryType.Bool;
                case "EntityType": return EntryType.EntityType;
                case "EntityLevel": return EntryType.EntityLevel;
                case "WeekDay": return EntryType.WeekDay;
                
                default: throw new Exception($"Tried to parse invalid EntryType: {entryTypeString}");
            }
        }
        
        public static string ToStringConsideringInvalidAsAnyType(this EntryType entryType)
        {
            if(entryType == EntryType.Invalid) return "AnyType";
            return entryType.ToString();
        }
        
        public static string ToString(this EntryType entryType)
        {
            switch (entryType)
            {
                case EntryType.Invalid: return "Invalid";
                case EntryType.Int: return "Int";
                case EntryType.Int2: return "Int2";
                case EntryType.Int3: return "Int3";
                case EntryType.Int64: return "Int64";
                case EntryType.UInt: return "UInt";
                case EntryType.UInt2: return "UInt2";
                case EntryType.UInt3: return "UInt3";
                case EntryType.UInt64: return "UInt64";
                case EntryType.Short: return "Short";
                case EntryType.UShort: return "UShort";
                case EntryType.Byte: return "Byte";
                case EntryType.SByte: return "Sbyte";
                case EntryType.Float: return "Float";
                case EntryType.Float2: return "Float2";
                case EntryType.Float3: return "Float3";
                case EntryType.Double: return "Double";
                case EntryType.Char: return "Char";
                case EntryType.String: return "String";
                case EntryType.Bool: return "Bool";
                case EntryType.EntityType: return "EntityType";
                case EntryType.EntityLevel: return "EntityLevel";
                case EntryType.WeekDay: return "WeekDay";
                
                default: throw new ArgumentOutOfRangeException(nameof( entryType), entryType, null);
            }
        }
        
        
        public static EntryTypeInfo GetEntryTypeInfo(this EntryType entryType) => EntryTypeInfoList[(int) entryType];
        
        
        private static EntryTypeInfo Components(EntryType fieldType, params (EntryType, string)[] components)
        {
            var componentInfos = new EntryComponentInfo[components.Length];
            for (int i = 0; i < components.Length; i++)
            {
                componentInfos[i] = new EntryComponentInfo(components[i].Item1, components[i].Item2);
            }
            return new EntryTypeInfo(fieldType, componentInfos);
        }
        
        public static bool IsEnumType(this EntryType entryType) => entryType != EntryType.Invalid && EntryTypeToSystemTypeMap[(int) entryType].IsEnum;
        public static bool IsValueType(this EntryType entryType) => entryType != EntryType.Invalid && EntryTypeToSystemTypeMap[(int) entryType].IsValueType;
        public static bool IsDataIdentifierType(this EntryType entryType) => entryType != EntryType.Invalid && typeof(IDataIdentifier).IsAssignableFrom(EntryTypeToSystemTypeMap[(int) entryType]);

        public static Entry GetInvalidValueEntry(this EntryType entryType) => Entry.GetInvalidValue(entryType);
        public static object GetInvalidValueAsObject(this EntryType entryType) => Entry.GetInvalidSystemValue(entryType);

    }
}