using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace PFound.GameDataStore.Storage
{
    public class DataStoreManager
    {
        private static DataStoreManager _instance;
        public static DataStoreManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new DataStoreManager();
                }

                return _instance;
            }
        }
        
        
        private readonly Dictionary<Type, IDataStoreDatabase> _databases;
        public DataStoreManager()
        {
            _databases = new Dictionary<Type, IDataStoreDatabase>();
        }
        
        public IDataStoreDatabase CreateDatabase<T>(T type) where T : Type
        {
            if (_databases.ContainsKey(type))
            {
                throw new Exception($"Datastore database {type} is already initialized");
            }
            else
            {
                var instance = (IDataStoreDatabase)Activator.CreateInstance(type);
                _databases.Add(type, instance);
                return instance;
            }
        }
        
        public void RegisterDatabase(IDataStoreDatabase database)
        {
            var type = database.GetType();

            if (!_databases.TryAdd(type, database))
            {
                throw new Exception($"Datastore database {type} is already initialized");
            }
        }
        
        public void Destroy(IDataStoreDatabase database)
        {
            foreach (var databaseRegistered in _databases)
            {
                if(ReferenceEquals(databaseRegistered.Value, database))
                {
                    _databases.Remove(databaseRegistered.Key);
                    return;
                }
            }
            throw new Exception($"Datastore database {database.GetType()} is not initialized");
        }
        
        
        
        public IDataStoreDatabase GetDatabase(Type type, [CallerMemberName] string memberName ="",[CallerFilePath] string sourceFilePath = "",[CallerLineNumber] int sourceLineNumber = 0)
        {
            if (_databases.TryGetValue(type, out var database))
            {
                return database;
            }
            throw new Exception($"Datastore database {type} is not initialized. Called from {memberName} in {sourceFilePath} at line {sourceLineNumber}");
        }
        
        public void Dispose()
        {
            if (Instance != null)
            {
                while (_instance._databases.Count > 0)
                {
                    using (var enumerator = _instance._databases.GetEnumerator())
                    {
                        enumerator.MoveNext();
                        enumerator.Current.Value.Dispose();
                    }
                }
            }
            _instance = null;
        }
        
    }
}