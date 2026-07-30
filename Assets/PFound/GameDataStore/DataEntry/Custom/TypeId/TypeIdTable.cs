using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PFound.GameDataStore.Entries
{
    public record DataIdentifierRegistry
    {
        public DataDefinitionList DataDefinitions { get; private set; }

        private DataDefinitionMap<EntityType> AllEntities;

        private bool _initialized;

        public DataIdentifierRegistry(DataDefinitionList dataDefinitions)
        {
            DataDefinitions = dataDefinitions;
            _initialized = true;
        }

        public void PrepareMaps()
        {
            Debug.Assert(_initialized,"TypeIds not initialized!");
            
            PrepareMap(DataDefinitions.AllEntities, ref AllEntities);
            
            var textToNumericMap = new Dictionary<string, EntityType>();
            var numericToTextMap = new Dictionary<EntityType, string>();
            AddInvalid(textToNumericMap, numericToTextMap);
            
            FillMapping(DataDefinitions.AllEntities, textToNumericMap, numericToTextMap);
        }

        private void PrepareMap<T>(IReadOnlyList<DataDefinition<T>> dataDefinitions, ref DataDefinitionMap<T> map) where T : struct, IDataIdentifier
        {
            var textToNumericMap = new Dictionary<string, T>();
            var numericToTextMap = new Dictionary<T, string>();
            foreach (var dataDefinition in dataDefinitions)
            {
                textToNumericMap.Add(dataDefinition.Text, dataDefinition.Type);
                numericToTextMap.Add(dataDefinition.Type, dataDefinition.Text);
            }
            AddInvalid(textToNumericMap, numericToTextMap);
            map = new DataDefinitionMap<T>(numericToTextMap, textToNumericMap);
        }

        private void PrepareMap<T>(IReadOnlyList<DataDefinition<T>> dataDefinitions,
            IReadOnlyList<DataDefinition<T>> dataDefinitions2,
            ref DataDefinitionMap<T> map) where T : struct, IDataIdentifier
        {
            var textToNumericMap = new Dictionary<string, T>();
            var numericToTextMap = new Dictionary<T, string>();
            foreach (var dataDefinition in dataDefinitions)
            {
                textToNumericMap.Add(dataDefinition.Text, dataDefinition.Type);
                numericToTextMap.Add(dataDefinition.Type, dataDefinition.Text);
            }
            foreach (var dataDefinition in dataDefinitions2)
            {
                textToNumericMap.Add(dataDefinition.Text, dataDefinition.Type);
                numericToTextMap.Add(dataDefinition.Type, dataDefinition.Text);
            }
            AddInvalid(textToNumericMap, numericToTextMap);
            map = new DataDefinitionMap<T>(numericToTextMap, textToNumericMap);
        }

        private void AddInvalid<T>(Dictionary<string, T> textToNumericMap, Dictionary<T, string> numericToTextMap) where T : struct, IDataIdentifier
        {
            textToNumericMap.Add(string.Empty, default);
            numericToTextMap.Add(default, string.Empty);
        }

        private void FillMapping<T>(IReadOnlyList<DataDefinition<T>> dataDefinitions,Dictionary<string, T> textToNumericMap, Dictionary<T, string> numericToTextMap) where T : struct, IDataIdentifier
        {
            foreach (var dataDefinition in dataDefinitions)
            {
                textToNumericMap.Add(dataDefinition.Text, dataDefinition.Type);
                numericToTextMap.Add(dataDefinition.Type, dataDefinition.Text);
            }
        }
        
        public bool TryGetTextIdsOfType(string typeName, out IEnumerable<string> textIds)
        {
            string[] textIdsArray;
            switch (typeName)
            {
                //@formatter:off
                case nameof(EntityType): textIds = AllEntities.TextToNumericMap.Keys; return true;
                case nameof(WeekDay): textIdsArray = Enum.GetNames(typeof(WeekDay)); textIds = textIdsArray.Skip(1).ToArray(); return true;
                //@formatter:on
                
                default: textIds = null; return false;
            }
        }

        public IEnumerable<(string text, uint numericId)> GetEnumerable
        {
            get
            {
                //@formatter:off
                foreach (var dd in AllEntities.TextToNumericMap) { yield return (dd.Key, dd.Value.NumericId); }
                //@formatter:on
            }
        }
        
        public void GetAllEntityTypes(ref List<(string text, uint numericId)> entityTypeDataDefinitions)
        {
            //@formatter:off
            foreach (var dd in AllEntities.TextToNumericMap) { entityTypeDataDefinitions.Add((dd.Key, dd.Value.NumericId)); }
            //@formatter:on
        }
        
        public void GetAllTypes<TEntityType>(ref List<TEntityType> entityTypeDataDefinitions) where TEntityType : IDataIdentifier
        {
            //@formatter:off
            foreach (var dd in DataDefinitions.GetTypeList<TEntityType>()) { entityTypeDataDefinitions.Add(dd.Type); }
            //@formatter:on
        }

        #region Data Getters

        public string GetTextId(EntityType entityType) { return AllEntities.NumericToTextMap[entityType]; }
        
        public bool TryGetEntityType(string textId, out EntityType entityType) { return AllEntities.TextToNumericMap.TryGetValue(textId, out entityType); }
        
        public EntityType GetEntityType(string textId) { return AllEntities.TextToNumericMap[textId]; }
        
        public EntityType GetEntityTypeOrError(ushort numericId) { if(AllEntities.NumericToTextMap.ContainsKey(new(numericId))) return new(numericId); Debug.LogError($" EntityType with numeric id {numericId} not found"); return default; }

        public bool IsEntityType(ushort numericId) { return AllEntities.NumericToTextMap.ContainsKey(new(numericId)); }
        
        #endregion
        
        
    }
}