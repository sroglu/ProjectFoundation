namespace PFound.GameDataStore.Entries
{
    public interface IDataIdentifier
    {
        ushort NumericId { get; }
        string TextId { get; }
    }

    [System.Serializable]
    public struct DataIdentifierDefinition
    {
        public ushort NumericId;
        public string TextId;
    }
}