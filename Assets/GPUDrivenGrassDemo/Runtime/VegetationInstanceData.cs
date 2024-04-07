using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace GPUDrivenGrassDemo.Runtime
{
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct VegetationInstanceData
    {
        /// <summary>
        /// center of bounding box
        /// </summary>
        public Vector3 center;
        /// <summary>
        /// half of the bounding box side
        /// </summary>
        public Vector3 extents;
        /// <summary>
        /// transform
        /// </summary>
        public Matrix4x4 matrixData;
        /// <summary>
        /// instance id
        /// </summary>
        public int InstanceID;
        /// <summary>
        /// model id
        /// </summary>
        public int ModelPrototypeID;
    }    
}
