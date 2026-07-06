using System;

namespace PFound.GameDataStore.Entries
{
    public class EntryManager : IDisposable
    {
        public static EntryManager Instance
        {
            get
            {
                if (_instance == null)
                    throw new Exception("DataStore not initialized yet!");
                return _instance;
            }
        }

        private static EntryManager _instance;
        private static DataIdentifierRegistry _loadedDataIdentifierRegistry;

        public static DataIdentifierRegistry DataIdentifierRegistry
        {
            get
            {
                if (_loadedDataIdentifierRegistry == null)
                {
                    throw new NullReferenceException("Tried to get DataIdentifierRegistry but it was not loaded yet!");
                }

                return _loadedDataIdentifierRegistry;
            }
        }
        public EntryManager(DataDefinitionConfig config)
        {
            _loadedDataIdentifierRegistry = new DataIdentifierRegistry(config.CreateDataDefinitionList());
            DataIdentifierRegistry.PrepareMaps();
            _instance = this;
        }

        public static void UnloadDataIdentifierRegistry()
        {
            _loadedDataIdentifierRegistry = null;
        }

        public void Dispose()
        {
            _instance = null;
            UnloadDataIdentifierRegistry();
        }

    }
}