using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;

namespace GPUDrivenGrassDemo.Runtime
{
    public class GPUDrivenGrass : MonoBehaviour
    {
        public VegetationDataBase database;

        // camera
        public Camera MainCamera;
        private Vector4[] CameraFrustumPlanes;
        private Vector3 LastCameraPos = Vector3.zero;
        private Quaternion LastCameraRot = Quaternion.identity;
        
        // rendering data
        private RenderingData[] renderingDatas;

        //compute shader
        private ComputeShader GPUDrivenCullingComputeShader;
        private int GPUDrivenCullingKernelID;

        //drawindirectinstance
        private ComputeBuffer DrawIndirectInstanceArgsBuffer;
        private uint[] DrawIndirectInstanceArgs = new uint[5] { 0, 0, 0, 0, 0 };
        private Bounds DrawIndirectInstanceBounds = new Bounds();
        
        // use for hiz
        public static RenderTexture depthRT;

        //use for debug
        [Header("Debug : show Instance GPUBounds by GetData")]
        public bool showInstanceGPUBounds_GetData;

        [Header("Debug : show Instance GPUBounds by Async")]
        public bool showInstanceGPUBounds_Async;

        public void Start()
        {
            initFromDataBase();
            
            //init computeshader
            GPUDrivenCullingComputeShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(
                "Assets/GPUDrivenGrassDemo/Runtime/GPUDrivenCulling.compute");
            GPUDrivenCullingKernelID = GPUDrivenCullingComputeShader.FindKernel("GPUDrivenCulling");

            GPUDrivenCullingComputeShader.SetInt("depthTextureSize", HzbDepthTexMaker.hzbDepthTextureSize);

            //draw indirect instances
            DrawIndirectInstanceBounds.size = Vector3.one * 100000;

            DrawIndirectInstanceArgsBuffer =
                new ComputeBuffer(5, sizeof(uint) * 5, ComputeBufferType.IndirectArguments);
        }
        
        public void Update()
        {
            // frustum culling
            GPUDrivenCullingComputeShader.SetVectorArray("cameraPlanes",
                GetFrustumPlanes(MainCamera, CameraFrustumPlanes));
            GPUDrivenCullingComputeShader.SetBool("showInstanceBounds",
                showInstanceGPUBounds_Async || showInstanceGPUBounds_GetData);
                
            // hiz
            if (depthRT != null)
            {
                GPUDrivenCullingComputeShader.SetTexture(GPUDrivenCullingKernelID, "HZB_Depth", depthRT);

                // // debug  save depth texture
                // if (IsRenderCameraChange())
                // {
                //     Debug.Log("set HZB_Depth");
                //     Tool.SaveRTToEXR(depthRT, "Assets/GPUDrivenGrassDemo/1.exr");
                // }
            }

            GPUDrivenCullingComputeShader.SetVector("cmrDir",MainCamera.transform.forward);
            Matrix4x4 vp = GL.GetGPUProjectionMatrix(MainCamera.projectionMatrix, false) * MainCamera.worldToCameraMatrix;
            GPUDrivenCullingComputeShader.SetMatrix("matrix_VP", vp);
            
            foreach (var renderingData in renderingDatas)
            {
                GPUDrivenCullingComputeShader.SetBuffer(GPUDrivenCullingKernelID, "instancesBuffer",
                    renderingData.InputInstancesBuffer);
                GPUDrivenCullingComputeShader.SetInt("instancesCount", renderingData.InstanceCount);
                GPUDrivenCullingComputeShader.SetBuffer(GPUDrivenCullingKernelID, "visibleBuffer",
                    renderingData.OutputVisibleInstancesBuffer);
                
                DrawIndirectInstanceArgs[0] = renderingData.PrefabMesh.GetIndexCount(0); // get the index array size of submesh 0
                DrawIndirectInstanceArgs[1] = 0; // instance count, in our culling case, this arg should be updated after culling
                DrawIndirectInstanceArgs[2] = renderingData.PrefabMesh.GetIndexStart(0); // get the start of index array of submesh 0
                DrawIndirectInstanceArgs[3] = renderingData.PrefabMesh.GetBaseVertex(0); // get the base vertex index of submesh 0
                DrawIndirectInstanceArgs[4] = 0; // the start index of instance
                DrawIndirectInstanceArgsBuffer.SetData(DrawIndirectInstanceArgs);
                
                renderingData.DrawIndirectInstanceMPB.SetBuffer("IndirectShaderDataBuffer", renderingData.OutputVisibleInstancesBuffer);
                
                // use for debug
                GPUDrivenCullingComputeShader.SetBuffer(GPUDrivenCullingKernelID, "GPUBoundsBuffer",
                    renderingData.InstanceGPUBoundsBuffer);
                
                
                // if (IsRenderCameraChange())
                // {
                //clear
                renderingData.InstanceGPUBoundsBuffer.SetCounterValue(0);
                renderingData.OutputVisibleInstancesBuffer.SetCounterValue(0);
                
                //note that the FrustumCullingKernelID's numthreads is(64,1,1)
                //here we declare 1D number Dispatch, the group's number should be [InstancesCount /64] + 1
                GPUDrivenCullingComputeShader.Dispatch(GPUDrivenCullingKernelID, (renderingData.InstanceCount / 64) + 1, 1, 1);
                
                ComputeBuffer.CopyCount(renderingData.OutputVisibleInstancesBuffer, DrawIndirectInstanceArgsBuffer, sizeof(uint));
                
                if (showInstanceGPUBounds_GetData)
                {
                    //// first copy count to a buffer
                    ComputeBuffer.CopyCount(renderingData.InstanceGPUBoundsBuffer, renderingData.InstanceGPUBoundsCount, 0);
                    //// than use the getdata method to get count , and store in cpu
                    renderingData.InstanceGPUBoundsCount.GetData(renderingData.InstanceGPUBoundsCountArray);
                    uint cnt = renderingData.InstanceGPUBoundsCountArray[0];
                    if (renderingData.InstanceGPUBounds == null || renderingData.InstanceGPUBounds.Length != cnt)
                    {
                        renderingData.InstanceGPUBounds = new GPUBounds[cnt];
                    }
                
                    //// get data method is synchronized, so it would be slow
                    renderingData.InstanceGPUBoundsBuffer.GetData(renderingData.InstanceGPUBounds);
                }

                Graphics.DrawMeshInstancedIndirect(
                    renderingData.PrefabMesh,
                    0,
                    renderingData.PrefabMaterial,
                    DrawIndirectInstanceBounds,
                    DrawIndirectInstanceArgsBuffer,
                    0,
                    renderingData.DrawIndirectInstanceMPB
                );
            }
        }

        private void initFromDataBase()
        {
            renderingDatas = new RenderingData[database.modelPrototypeList.Count];

            List<List<VegetationInstanceData>> instanceDataList = new List<List<VegetationInstanceData>>();

            for (int i = 0; i < database.modelPrototypeList.Count; ++i)
            {
                instanceDataList.Add(new List<VegetationInstanceData>());
                renderingDatas[i] = new RenderingData();
                GameObject prefab = database.GetPrefabByID(database.modelPrototypeList[i].prefabID);
                renderingDatas[i].Prefab = prefab;
                renderingDatas[i].PrefabMesh = prefab.GetComponent<MeshFilter>().sharedMesh;
                var mr = prefab.GetComponent<MeshRenderer>();
                renderingDatas[i].PrefabMaterial = mr.sharedMaterial;
                renderingDatas[i].DrawIndirectInstanceMPB = new MaterialPropertyBlock();
            }
            
            foreach (var instanceData in database.vegetationInstanceDataList)
            {
                int index = database.modelDic[instanceData.ModelPrototypeID];
                instanceDataList[index].Add(instanceData);
            }

            for (int i = 0; i < renderingDatas.Length; ++i)
            {
                renderingDatas[i].InstanceCount = instanceDataList[i].Count;
                renderingDatas[i].InstanceDatas = instanceDataList[i].ToArray();
                renderingDatas[i].Init();
            }
            
            instanceDataList.Clear();
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.red;
            if (renderingDatas == null) return;
            foreach (var renderingData in renderingDatas)
            {
                for (int i = 0; renderingData.InstanceGPUBounds != null && i < renderingData.InstanceGPUBoundsCountArray[0]; i++)
                {
                    if (i >= renderingData.InstanceGPUBounds.Length) break;
                    var ggbox = renderingData.InstanceGPUBounds[i];
                    Gizmos.DrawWireCube((ggbox.max + ggbox.min) / 2f, ggbox.max - ggbox.min);
                }
            }
        }
        
        private bool IsRenderCameraChange()
        {
            if (LastCameraPos != MainCamera.transform.position ||
                LastCameraRot != MainCamera.transform.rotation)
            {
                LastCameraPos = MainCamera.transform.position;
                LastCameraRot = MainCamera.transform.rotation;
                return true;
            }

            return false;
        }

        // point is on the plane, so n dot p + d = 0, d = - n dot p
        private static Vector4 GetPlane(Vector3 normal, Vector3 point) =>
            new Vector4(normal.x, normal.y, normal.z, -Vector3.Dot(normal, point));

        // a, b, c is three points on the plane
        // Note : Unity Cross use the left-hand principle
        private static Vector4 GetPlane(Vector3 a, Vector3 b, Vector3 c) =>
            GetPlane(Vector3.Normalize(Vector3.Cross(b - a, c - a)), a);

        private static Vector3[] GetCameraFarClipPlanePoint(Camera camera)
        {
            Vector3[] points = new Vector3[4];
            Transform transform = camera.transform;
            float distance = camera.farClipPlane;
            float halfFovRad = Mathf.Deg2Rad * camera.fieldOfView * 0.5f;
            float upLen = distance * Mathf.Tan(halfFovRad);
            float rightLen = upLen * camera.aspect;

            Vector3 farCenterPoint = transform.position + distance * transform.forward;
            Vector3 up = upLen * transform.up;
            Vector3 right = rightLen * transform.right;

            points[0] = farCenterPoint - up - right; //left-bottom
            points[1] = farCenterPoint - up + right; //right-bottom
            points[2] = farCenterPoint + up - right; //left-up
            points[3] = farCenterPoint + up + right; //right-up
            return points;
        }

        private static Vector4[] GetFrustumPlanes(Camera camera, Vector4[] planes = null)
        {
            if (planes == null) planes = new Vector4[6];
            Transform transform = camera.transform;
            Vector3 cameraPosition = transform.position;
            Vector3[] points = GetCameraFarClipPlanePoint(camera);
            //clock direction
            planes[0] = GetPlane(cameraPosition, points[0], points[2]); //left
            planes[1] = GetPlane(cameraPosition, points[3], points[1]); //right
            planes[2] = GetPlane(cameraPosition, points[1], points[0]); //bottom
            planes[3] = GetPlane(cameraPosition, points[2], points[3]); //up
            planes[4] = GetPlane(-transform.forward, transform.position + transform.forward * camera.nearClipPlane); //near
            planes[5] = GetPlane(transform.forward, transform.position + transform.forward * camera.farClipPlane); //far
            return planes;
        }
        
        private void OnDestroy()
        {
            DrawIndirectInstanceArgsBuffer?.Release();
            foreach (var renderingData in renderingDatas)
            {
                renderingData.Clear();;
            }
        }
    }
}
