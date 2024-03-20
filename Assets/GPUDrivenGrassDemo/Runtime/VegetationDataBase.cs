using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace GPUDrivenGrassDemo.Runtime
{
    [CreateAssetMenu(fileName = "VegetationDataAsset", menuName = "GPUDrivenGrass/VegetationDataAsset", order = 1)]
    public class VegetationDataBase: ScriptableObject
    {
        public int terrainCount;
        public int instanceCount;
        public List<VegetationInstanceData> vegetationInstanceDataList;
        public List<ModelPrototype> modelPrototypeList;

        public GameObject GetPrefabByID(int prefabID)
        {
            foreach (var modelPrototype in modelPrototypeList)
            {
                if (modelPrototype.prefabID == prefabID)
                {
                    return modelPrototype.prefabGameObject;
                }
            }

            return null;
        }
    } 
}
