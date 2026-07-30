using System;
using System.Collections.Generic;

namespace PFound.GameDataStore.Entries
{
    public record DataDefinitionList
    {
        public DataDefinitionList(IReadOnlyList<DataDefinition<EntityType>> allEntities)
        {
            AllEntities = allEntities;
        }

        public IReadOnlyList<DataDefinition<EntityType>> AllEntities { get; private set; } = new List<DataDefinition<EntityType>>();
        
        public IReadOnlyList<DataDefinition<T>> GetTypeList<T>() where T : IDataIdentifier
        {
            if (typeof(T) == typeof(EntityType)) return AllEntities as IReadOnlyList<DataDefinition<T>>;
            
            throw new Exception($"{typeof(T)} is not a valid type for data definition list");
        }
    }
}