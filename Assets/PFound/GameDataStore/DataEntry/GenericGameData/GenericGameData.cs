using System;
using Sirenix.OdinInspector;

namespace PFound.GameDataStore.Entries
{
    [Serializable]
    public struct GenericGameData
    {
        [HorizontalGroup("HorizontalGroup", MinWidth = 90, MaxWidth = 150)] 
        public ProcessedKey Key;
        [HideLabel] 
        [HorizontalGroup("HorizontalGroup", Gap =  10)]
        public Entry Entry;
        
        public GenericGameData(Entry entry, ProcessedKey key)
        {
            Entry = entry;
            Key = key;
        }
        
        public override string ToString()
        {
            return $"{Key}: {Entry}";
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Key.GetHashCode(), Entry.GetHashCode());
        }
    }
}