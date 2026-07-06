using System;
using System.Collections.Generic;
using System.Reflection;

namespace PFound.GameDataStore.Storage
{
    public interface IDataStoreDatabase : IDisposable { }
    public interface IDataStoreDatabase<T> : IDataStoreDatabase where T : IDataStoreDatabase<T>, new()
    {
        protected static bool _initialized;
        
        protected static T _instance;
        
        public static T Instance
        {
            get
            {
                if (_initialized)
                {
                    return _instance;
                }
                #if UNITY_EDITOR
                // In editor mode, we just show database to see its content without initialization
                Initialize();
                if (!_initialized)
                {
                    throw new Exception($" Datastore database {typeof(T)} is not initialized");
                }
                return _instance;
#else
                throw new Exception($" Datastore database {typeof(T)} is not initialized");
#endif
            }
        }

        protected static void Initialize()
        {
            if (_initialized)
            {
                throw new Exception($" Datastore database {typeof(T)} is already initialized");
            }
            
            var type = typeof(T);
            var initialization = type.TryGetDataStoreDatabaseAttribute(out var attribute) ? attribute.Initialization : DataStoreDatabaseInitializationMethod.Default;
            
            if(initialization == DataStoreDatabaseInitializationMethod.ManualRegistration)
            {
                throw new Exception($" Datastore database {typeof(T)} is set to manual initialization");
            }
            _instance = (T)DataStoreManager.Instance.CreateDatabase(typeof(T));
            _initialized = true;
        }
        
        protected static void InitializeWithInstance(T instance)
        {
            if (_initialized)
            {
                throw new Exception($" Datastore database {typeof(T)} is already initialized");
            }
            
            var type = instance.GetType();
            var initialization = type.TryGetDataStoreDatabaseAttribute(out var attribute) ? attribute.Initialization : DataStoreDatabaseInitializationMethod.Default;
            if (initialization != DataStoreDatabaseInitializationMethod.ManualRegistration)
            {
                throw new Exception($" Datastore database {typeof(T)} is not set to manual initialization");
            }
            
            DataStoreManager.Instance.RegisterDatabase(instance);
            _instance = instance;
            _initialized = true;
        }
        
        protected static void Destroy()
        {
            if (!_initialized)
            {
                throw new Exception($" Datastore database {typeof(T)} is not initialized");
            }
            _initialized = false;
            _instance = default;
        }
    }
    [Serializable]
    public abstract class DataStoreClass<T> : IDataStoreDatabase<T> where T : IDataStoreDatabase<T>, new()
    {
        public static T Instance => IDataStoreDatabase<T>.Instance;
        public void Dispose() => IDataStoreDatabase<T>.Destroy(); 
        public static bool IsInitialized => IDataStoreDatabase<T>._initialized;
        
        public static void Initialize()=>IDataStoreDatabase<T>.Initialize();
        public static void InitializeWithInstance(T instance)=>IDataStoreDatabase<T>.InitializeWithInstance(instance);
    }
    [Serializable]
    public abstract record DataStoreRecord<T> : IDataStoreDatabase<T> where T : IDataStoreDatabase<T>, new()
    {
        public static T Instance => IDataStoreDatabase<T>.Instance;
        public void Dispose() => IDataStoreDatabase<T>.Destroy(); 
        public static bool IsInitialized => IDataStoreDatabase<T>._initialized;
        
        public static void Initialize()=>IDataStoreDatabase<T>.Initialize();
        public static void InitializeWithInstance(T instance)=>IDataStoreDatabase<T>.InitializeWithInstance(instance);
    }


    public static class DataStoreExtensions
    {
        public static readonly MethodInfo DictionaryGetMethodInfo;
        public static string DictionaryGetMethodName = nameof(Get);
        public static string DictionaryTryGetMethodParameterName = "key";
        
        static DataStoreExtensions()
        {
            DictionaryGetMethodInfo = typeof(DataStoreExtensions).GetMethod(DictionaryGetMethodName, BindingFlags.Static | BindingFlags.Public);
        }
        
        public static MethodInfo CreateConcreateMethodInfoForDictionaryGet(Type keyType, Type valueType)
        {
            return DictionaryGetMethodInfo.MakeGenericMethod(keyType, valueType);
        }
        
        
        public static TValue Get<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> dictionary, TKey key)
        {
            if (dictionary.TryGetValue(key, out var value))
            {
                return value;
            }
            throw new KeyNotFoundException($"Key {key} not found in dictionary");
        }

    }
    
    
    
    
    
    public static class DataStoreTools
    {
        public static bool IsDataStoreDatabase(this Type type)
        {
            var baseType = type.BaseType;
            if (baseType is { IsGenericType: true })
            {
                var genericType = baseType.GetGenericTypeDefinition();
                return genericType == typeof(DataStoreClass<>) || genericType == typeof(DataStoreRecord<>);
            }

            return false;
        }
        
        public static bool TryGetDataStoreDatabaseAttribute(this Type type, out DataStoreConfigAttribute attribute)
        {
            attribute = type.GetCustomAttribute<DataStoreConfigAttribute>();
            return attribute != null;
        }
        
    }
    
    
}