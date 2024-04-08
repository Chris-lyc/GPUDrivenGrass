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
        
        // use for debug
        public ComputeBuffer InstanceGPUBoundsBuffer; // use for bounding gpu buffer
        public ComputeBuffer InstanceGPUBoundsCount;
        public uint[] InstanceGPUBoundsCountArray = new uint[1] { 0 }; // use for store the data in cpu
        public GPUBounds[] InstanceGPUBounds;
        
        public int InstanceCount;
        public VegetationInstanceData[] InstanceDatas;
        
        public void Init()
        {
            InputInstancesBuffer = new ComputeBuffer(InstanceCount, Marshal.SizeOf(new VegetationInstanceData()));
            InputInstancesBuffer.SetData(InstanceDatas);
            OutputVisibleInstancesBuffer = new ComputeBuffer(InstanceCount,
                Marshal.SizeOf(new VegetationInstanceData()), ComputeBufferType.Append);
            
            InstanceGPUBoundsCount = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.IndirectArguments);
            InstanceGPUBoundsCount.SetData(InstanceGPUBoundsCountArray);
            InstanceGPUBoundsBuffer =
                new ComputeBuffer(InstanceCount, sizeof(float) * 3 * 2, ComputeBufferType.Append);
            InstanceGPUBounds = new GPUBounds[InstanceCount];
        }

        public void Clear()
        {
            InputInstancesBuffer?.Release();
            OutputVisibleInstancesBuffer?.Release();
            
            InstanceGPUBoundsBuffer?.Release();
            InstanceGPUBoundsCount?.Release();
        }
    }
}