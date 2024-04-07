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
        
        public Dictionary<int, int> modelDic; // <prefabID, index in modelPrototypeList>

        public void OnEnable()
        {
            modelDic = new Dictionary<int, int>();
            for (int i = 0; i < modelPrototypeList.Count; ++i)
            {
                modelDic.Add(modelPrototypeList[i].prefabID, i);
            }
        }

        public GameObject GetPrefabByID(int prefabID)
        {
            if (modelDic.ContainsKey(prefabID))
            {
                return modelPrototypeList[modelDic[prefabID]].prefabGameObject;
            }

            return null;
            
            // foreach (var modelPrototype in modelPrototypeList)
            // {
            //     if (modelPrototype.prefabID == prefabID)
            //     {
            //         return modelPrototype.prefabGameObject;
            //     }
            // }
            //
            // return null;
        }
    } 
}
