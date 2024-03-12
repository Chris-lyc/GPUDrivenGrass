using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public struct GPUBounds
{
    public Vector3 min;
    public Vector3 max;
};

public class RenderVegetation : MonoBehaviour
{
    // camera
    public Camera MainCamera;
    private Vector4[] CameraFrustumPlanes;
    private Vector3 LastCameraPos = Vector3.zero;
    private Quaternion LastCameraRot = Quaternion.identity;

    // prefab
    public GameObject Prefab;
    private Mesh PrefabMesh;
    private Material PrefabMaterial;
    private Bounds PrefabMeshBounds; //use for the prefab's mesh, as the boundCenter and boundExtents to be transfered  

    //compute shader
    public ComputeShader FrustumCullComputeShader;
    private ComputeBuffer InputInstancesBuffer;
    private ComputeBuffer OutputVisibleInstancesBuffer;
    private int FrustumCullingKernelID;

    //drawindirectinstance
    private ComputeBuffer DrawIndirectInstanceArgsBuffer;
    private uint[] DrawIndirectInstanceArgs = new uint[5] { 0, 0, 0, 0, 0 };
    private Bounds DrawIndirectInstanceBounds = new Bounds();
    private MaterialPropertyBlock DrawIndirectInstanceMPB;


    //use for generate instances
    public int InstanceCounts = 1000000;
    public Vector3Int InstanceExtents = new Vector3Int(500, 500, 500);
    public float RandomMaxScaleValue = 5;
    private Matrix4x4[] InstancesMatrix;


    //use for debug
    private ComputeBuffer InstanceGPUBoundsBuffer; // use for bounding gpu buffer
    private ComputeBuffer InstanceGPUBoundsCount;

    private uint[] InstanceGPUBoundsCountArray = new uint[1] { 0 }; // use for store the data in cpu
    private GPUBounds[] InstanceGPUBounds;

    [Header("Debug : show Instance GPUBounds by GetData")]
    public bool showInstanceGPUBounds_GetData;
    [Header("Debug : show Instance GPUBounds by Async")]
    public bool showInstanceGPUBounds_Async;
    
    // Start is called before the first frame update
    void Start()
    {
        //init instances
        InstancesMatrix = RandomGenerateInstances(InstanceCounts, InstanceExtents, RandomMaxScaleValue);
        
        //init prefab
        PrefabMesh = Prefab.GetComponent<MeshFilter>().sharedMesh;
        var mr = Prefab.GetComponent<MeshRenderer>();
        PrefabMaterial = mr.sharedMaterial;
        PrefabMeshBounds = mr.bounds;

        //malloc buffer
        InputInstancesBuffer = new ComputeBuffer(InstanceCounts, sizeof(float) * 4 * 4);
        InputInstancesBuffer.SetData(InstancesMatrix);
        OutputVisibleInstancesBuffer = new ComputeBuffer(InstanceCounts, sizeof(float) * 4 * 4, ComputeBufferType.Append);

        //init computeshader
        FrustumCullingKernelID = FrustumCullComputeShader.FindKernel("FrustumCulling");
        FrustumCullComputeShader.SetBuffer(FrustumCullingKernelID, "instancesBuffer", InputInstancesBuffer);
        FrustumCullComputeShader.SetInt("instancesCount", InstanceCounts);
        FrustumCullComputeShader.SetVector("boxCenter", PrefabMeshBounds.center);
        FrustumCullComputeShader.SetVector("boxExtents", PrefabMeshBounds.extents);
        FrustumCullComputeShader.SetBuffer(FrustumCullingKernelID, "visibleBuffer", OutputVisibleInstancesBuffer);

        //draw indirect instances
        DrawIndirectInstanceBounds.size = Vector3.one * 100000;

        DrawIndirectInstanceArgs[0] = PrefabMesh.GetIndexCount(0); // get the index array size of submesh 0
        DrawIndirectInstanceArgs[1] = 0; // instance count, in our culling case, this arg should be updated after culling
        DrawIndirectInstanceArgs[2] = PrefabMesh.GetIndexStart(0); // get the start of index array of submesh 0
        DrawIndirectInstanceArgs[3] = PrefabMesh.GetBaseVertex(0); // get the base vertex index of submesh 0
        DrawIndirectInstanceArgs[4] = 0; // the start index of instance

        DrawIndirectInstanceArgsBuffer = new ComputeBuffer(5, sizeof(uint) * 5, ComputeBufferType.IndirectArguments);
        DrawIndirectInstanceArgsBuffer.SetData(DrawIndirectInstanceArgs);

        DrawIndirectInstanceMPB = new MaterialPropertyBlock();
        DrawIndirectInstanceMPB.SetBuffer("IndirectShaderDataBuffer", OutputVisibleInstancesBuffer);

        // use for debug
        InstanceGPUBoundsBuffer = new ComputeBuffer(InstanceCounts, sizeof(float)* 3 * 2, ComputeBufferType.Append);
        InstanceGPUBoundsCount = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.IndirectArguments);
        InstanceGPUBoundsCount.SetData(InstanceGPUBoundsCountArray);
        FrustumCullComputeShader.SetBuffer(FrustumCullingKernelID, "GPUBoundsBuffer", InstanceGPUBoundsBuffer);

        InstanceGPUBounds = new GPUBounds[InstanceCounts];
    }


    // Update is called once per frame
    void Update()
    {
        if (IsRenderCameraChange())
        {
            //clear
            InstanceGPUBoundsBuffer.SetCounterValue(0);
            OutputVisibleInstancesBuffer.SetCounterValue(0);
            FrustumCullComputeShader.SetVectorArray("cameraPlanes", GetFrustumPlanes(MainCamera, CameraFrustumPlanes));
            FrustumCullComputeShader.SetBool("showInstanceBounds", showInstanceGPUBounds_Async || showInstanceGPUBounds_GetData);
            //note that the FrustumCullingKernelID's numthreads is(64,1,1)
            //here we declare 1D number Dispatch, the group's number should be [InstancesCount /64] + 1
            FrustumCullComputeShader.Dispatch(FrustumCullingKernelID, (InstanceCounts / 64) + 1 , 1, 1);
            ComputeBuffer.CopyCount(OutputVisibleInstancesBuffer, DrawIndirectInstanceArgsBuffer, sizeof(uint));

            if (showInstanceGPUBounds_GetData)
            {

                //// first copy count to a buffer
                ComputeBuffer.CopyCount(InstanceGPUBoundsBuffer, InstanceGPUBoundsCount, 0);
                //// than use the getdata method to get count , and store in cpu
                InstanceGPUBoundsCount.GetData(InstanceGPUBoundsCountArray);
                uint cnt = InstanceGPUBoundsCountArray[0];
                if (InstanceGPUBounds == null || InstanceGPUBounds.Length != cnt)
                {
                    InstanceGPUBounds = new GPUBounds[cnt];
                }
                //// get data method is synchronized, so it would be slow
                InstanceGPUBoundsBuffer.GetData(InstanceGPUBounds);
            }
        }
        Graphics.DrawMeshInstancedIndirect(
            PrefabMesh,
            0,
            PrefabMaterial,
            DrawIndirectInstanceBounds,
            DrawIndirectInstanceArgsBuffer,
            0,
            DrawIndirectInstanceMPB
            );

    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        for(int i = 0; InstanceGPUBounds != null && i < InstanceGPUBoundsCountArray[0]; i++)
        {
            if (i >= InstanceGPUBounds.Length) break;
            var ggbox = InstanceGPUBounds[i];
            Gizmos.DrawWireCube((ggbox.max + ggbox.min) / 2f, ggbox.max - ggbox.min);
        }
    }


    private Matrix4x4[] RandomGenerateInstances(int instanceCount, Vector3Int instanceExtents, float maxScale)
    {
        var instances = new Matrix4x4[instanceCount];
        var cameraPos = MainCamera.transform.position;

        for (var i = 0; i < instanceCount; i++)
        {
            var pos = new Vector3(
                cameraPos.x + Random.Range(-instanceExtents.x, instanceExtents.x),
                cameraPos.y + Random.Range(-instanceExtents.y, instanceExtents.y),
                cameraPos.z + Random.Range(-instanceExtents.z, instanceExtents.z)
                );
            var rot = Quaternion.Euler(Random.Range(0, 180), Random.Range(0, 180), Random.Range(0, 180));
            var scl = new Vector3(Random.Range(0.1f, maxScale), Random.Range(0.1f, maxScale), Random.Range(0.1f, maxScale));

            instances[i] = Matrix4x4.TRS(pos, rot, scl);
        }
        return instances;
    }


    

    public bool IsRenderCameraChange()
    {
        if(LastCameraPos != MainCamera.transform.position ||
            LastCameraRot != MainCamera.transform.rotation)
        {
            LastCameraPos = MainCamera.transform.position;
            LastCameraRot = MainCamera.transform.rotation;
            return true;
        }
        return false;
    }

    // point is on the plane, so n dot p + d = 0, d = - n dot p
    private static Vector4 GetPlane(Vector3 normal, Vector3 point) => new Vector4(normal.x, normal.y, normal.z, -Vector3.Dot(normal, point));
    
    // a, b, c is three points on the plane
    // Note : Unity Cross use the left-hand principle
    private static Vector4 GetPlane(Vector3 a, Vector3 b, Vector3 c) => GetPlane(Vector3.Normalize(Vector3.Cross(b - a, c - a)), a);

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

        points[0] = farCenterPoint - up - right;//left-bottom
        points[1] = farCenterPoint - up + right;//right-bottom
        points[2] = farCenterPoint + up - right;//left-up
        points[3] = farCenterPoint + up + right;//right-up
        return points;
    }

    private static Vector4[] GetFrustumPlanes(Camera camera, Vector4[] planes = null)
    {
        if (planes == null) planes = new Vector4[6];
        Transform transform = camera.transform;
        Vector3 cameraPosition = transform.position;
        Vector3[] points = GetCameraFarClipPlanePoint(camera);
        //clock direction
        planes[0] = GetPlane(cameraPosition, points[0], points[2]);//left
        planes[1] = GetPlane(cameraPosition, points[3], points[1]);//right
        planes[2] = GetPlane(cameraPosition, points[1], points[0]);//bottom
        planes[3] = GetPlane(cameraPosition, points[2], points[3]);//up
        planes[4] = GetPlane(-transform.forward, transform.position + transform.forward * camera.nearClipPlane);//near
        planes[5] = GetPlane(transform.forward, transform.position + transform.forward * camera.farClipPlane);//far
        return planes;
    }

    private void OnDestroy()
    {
        InstanceGPUBoundsBuffer?.Release();
        InstanceGPUBoundsCount?.Release();
    }

}
