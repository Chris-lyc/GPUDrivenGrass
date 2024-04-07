using System.Runtime.InteropServices;
using UnityEngine;

namespace GPUDrivenGrassDemo.Runtime
{
    public class RenderingData
    {
        // prefab
        public GameObject Prefab;
        public Mesh PrefabMesh;
        public Material PrefabMaterial;
        // mpb
        public MaterialPropertyBlock DrawIndirectInstanceMPB;
        
        // compute buffer
        public ComputeBuffer InputInstancesBuffer;
        public ComputeBuffer OutputVisibleInstancesBuffer;
        
        public int InstanceCount;
        public VegetationInstanceData[] InstanceDatas;
        
        public void Init()
        {
            InputInstancesBuffer = new ComputeBuffer(InstanceCount, Marshal.SizeOf(new VegetationInstanceData()));
            InputInstancesBuffer.SetData(InstanceDatas);
            OutputVisibleInstancesBuffer = new ComputeBuffer(InstanceCount,
                Marshal.SizeOf(new VegetationInstanceData()), ComputeBufferType.Append);
        }

        public void Clear()
        {
            InputInstancesBuffer?.Release();
            OutputVisibleInstancesBuffer?.Release();
        }
    }
}