using System.Collections.Generic;

namespace PFound.GameDataStore.Entries
{
    [System.Serializable]
    public struct DataDefinitionMap<T> where T : IDataIdentifier
    {
        public IReadOnlyDictionary<T, string> NumericToTextMap { get; }
        public IReadOnlyDictionary<string, T> TextToNumericMap { get; }

        public DataDefinitionMap(IReadOnlyDictionary<T, string> numericToTextMap,
            IReadOnlyDictionary<string, T> textToNumericMap)
        {
            NumericToTextMap = numericToTextMap;
            TextToNumericMap = textToNumericMap;
        }
    }
}