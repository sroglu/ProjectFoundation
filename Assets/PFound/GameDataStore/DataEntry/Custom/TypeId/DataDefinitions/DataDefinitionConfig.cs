using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace PFound.GameDataStore.Entries
{
    [CreateAssetMenu(fileName = "DataDefinitionConfig", menuName = "GameConfigs/DataDefinitionConfig", order = 1)]
    public class DataDefinitionConfig : ScriptableObject
    {
        public DataDefinitionList CreateDataDefinitionList()
        {
            List<DataDefinition<EntityType>> allEntities = new List<DataDefinition<EntityType>>();
            allEntities.AddRange(GetBuildingDataDefinitions());
            allEntities.AddRange(GetItemDataDefinitions());
            
            return new DataDefinitionList(allEntities);
        }
        
        [SerializeField]
        private List<DataIdentifierDefinition> _buildingDefinitions;
        [SerializeField]
        private List<DataIdentifierDefinition> _itemDefinitions;


        #region Data definition list constructor methods
        
        private List<DataDefinition<EntityType>> GetBuildingDataDefinitions()
        {
            List<DataDefinition<EntityType>> buildingDataDefinitions = new List<DataDefinition<EntityType>>();
            foreach (var entityDefinition in _buildingDefinitions)
            {
                var dd = new DataDefinition<EntityType>
                {
                    Type = new EntityType(entityDefinition.NumericId),
                    Text = entityDefinition.TextId
                };
                buildingDataDefinitions.Add(dd);
            }

            return buildingDataDefinitions;
        }
        
        private List<DataDefinition<EntityType>> GetItemDataDefinitions()
        {
            List<DataDefinition<EntityType>> itemDataDefinitions = new List<DataDefinition<EntityType>>();
            foreach (var entityDefinition in _itemDefinitions)
            {
                var dd = new DataDefinition<EntityType>
                {
                    Type = new EntityType(entityDefinition.NumericId),
                    Text = entityDefinition.TextId
                };
                itemDataDefinitions.Add(dd);
            }

            return itemDataDefinitions;
        }

  
        #endregion
    }
}