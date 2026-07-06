using System;

namespace PFound.GameDataStore.Storage
{
    public enum TypeBindingSetup
    {
        ExplicitlySpecifyBindableMembers = 1 << 1,
    }
    [AttributeUsage(AttributeTargets.Class|AttributeTargets.Struct)]
    public class DataStoreSetupAttribute : Attribute
    {
        public readonly TypeBindingSetup Setup;
        public DataStoreSetupAttribute(TypeBindingSetup setup)
        {
            Setup = setup;
        }
    }
    
    [AttributeUsage(AttributeTargets.Field|AttributeTargets.Property|AttributeTargets.Method)]
    public class DataStoreElementAttribute : Attribute
    {
        public DataStoreElementAttribute() { }
    }
    
    [AttributeUsage(AttributeTargets.Field|AttributeTargets.Property|AttributeTargets.Method)]
    public class NotDataStoreElementAttribute : Attribute
    {
        public NotDataStoreElementAttribute() { }
    }
    
    [AttributeUsage(AttributeTargets.Field|AttributeTargets.Property)]
    public class RequestParameterAttribute : Attribute
    {
        public readonly int MessagePackKeyIndex;
        public RequestParameterAttribute(int messagePackKeyIndex)
        {
            MessagePackKeyIndex = messagePackKeyIndex;
        }
    }
    
    [AttributeUsage(AttributeTargets.Method)]
    public class ExternallyExecutableAttribute : Attribute
    {
        public ExternallyExecutableAttribute() { }
    }

    public enum DataStoreDatabaseInitializationMethod : byte
    {
        Default = 0,
        InitializeAtStart = 1,
        ManualRegistration = 2,
    }
    
    [AttributeUsage(AttributeTargets.Class|AttributeTargets.Struct)]
    public class DataStoreConfigAttribute : Attribute
    {
        public string Alias;
        public DataStoreDatabaseInitializationMethod Initialization= DataStoreDatabaseInitializationMethod.Default;
    }
}