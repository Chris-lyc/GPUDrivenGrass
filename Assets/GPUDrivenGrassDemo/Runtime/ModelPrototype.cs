using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace GPUDrivenGrassDemo.Runtime
{
    [Serializable]
    public class ModelPrototype
    {
        public int prefabID;
        public GameObject prefabGameObject;

        public ModelPrototype(int prefabID, GameObject prefabGameObject)
        {
            this.prefabID = prefabID;
            this.prefabGameObject = prefabGameObject;
        }
    }
}