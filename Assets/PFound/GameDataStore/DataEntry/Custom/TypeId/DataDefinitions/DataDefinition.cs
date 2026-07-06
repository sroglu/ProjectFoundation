namespace PFound.GameDataStore.Entries
{
    public struct DataDefinition<T> where T : IDataIdentifier
    {
        public T Type;
        public string Text;

        public override string ToString()
        {
            return $"{Type.TextId} - {Text}";
        }
    }
}