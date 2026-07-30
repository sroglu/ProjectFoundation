using System.Collections.Generic;

namespace PFound.GameDataStore.Entries
{
    public record EntryTypeInfo
    {
        public readonly EntryType FieldType;
        public readonly IReadOnlyList<EntryComponentInfo> Components;
        public bool HasComponents => Components.Count > 0;
        public EntryTypeInfo(EntryType fieldType, params EntryComponentInfo[] components)
        {
            FieldType = fieldType;
            Components = new List<EntryComponentInfo>(components);
        }
    }
    
    public record EntryComponentInfo
    {
        public readonly EntryType FieldType;
        public readonly string FieldName;
        public EntryComponentInfo(EntryType fieldType, string fieldName)
        {
            FieldType = fieldType;
            FieldName = fieldName;
        }
    }
}