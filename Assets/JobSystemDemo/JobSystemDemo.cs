using System;
using Unity.Collections;
using UnityEngine;
using Unity.Jobs;
using Unity.Mathematics;

public class JobSyetemDemo : MonoBehaviour
{
    public Camera mainCamera;
    public GameObject prefab;
    public int outputLength;

    public int instanceCount = 100000;
    public Vector3Int instanceExtents = new Vector3Int(500, 500, 500);
    public float randomMaxScaleValue = 5;

    private Bounds meshBounds;
    private float4[] frustumPlanesArray = new float4[6];
    private NativeArray<float4> frustumPlanes;
    private NativeArray<float4x4> input;
    private NativeArray<int> outputCount;
    private NativeArray<float4x4> output;

    private Mesh mesh;
    private Material material;
    private Bounds drawBounds = new Bounds();
    private MaterialPropertyBlock mpb;
    private ComputeBuffer instanceOutputBuffer;
    
    // Start is called before the first frame update
    void Start()
    {
        RandomGeneratedInstances(instanceCount, instanceExtents, randomMaxScaleValue);

        mesh = prefab.GetComponent<MeshFilter>().sharedMesh;
        var meshRenderer = prefab.GetComponent<MeshRenderer>();
        material = meshRenderer.sharedMaterial;
        meshBounds = meshRenderer.bounds;

        frustumPlanes = new NativeArray<float4>(6, Allocator.Persistent);
        outputCount = new NativeArray<int>(1, Allocator.Persistent);
        output = new NativeArray<float4x4>(instanceCount, Allocator.Persistent);
        drawBounds.size = Vector3.one * 100000;

        instanceOutputBuffer = new ComputeBuffer(instanceCount, sizeof(float) * 16);
        mpb = new MaterialPropertyBlock();
        mpb.SetBuffer("IndirectShaderDataBuffer", instanceOutputBuffer);
    }
    
    private void RandomGeneratedInstances(int instanceCount, Vector3Int instanceExtents, float maxScale)
    {
        input = new NativeArray<float4x4>(instanceCount, Allocator.Persistent);
        var cameraPos = mainCamera.transform.position;
        for (var i = 0; i < instanceCount; ++i)
        {
            var position = new Vector3(
                cameraPos.x + UnityEngine.Random.Range(-instanceExtents.x, instanceExtents.x),
                cameraPos.y + UnityEngine.Random.Range(-instanceExtents.y, instanceExtents.y),
                cameraPos.z + UnityEngine.Random.Range(-instanceExtents.z, instanceExtents.z)
            );
            var rotation = Quaternion.Euler(
                UnityEngine.Random.Range(0, 180),
                UnityEngine.Random.Range(0, 180),
                UnityEngine.Random.Range(0, 180)
            );
            var scale = new Vector3(
                UnityEngine.Random.Range(0.1f, maxScale),
                UnityEngine.Random.Range(0.1f, maxScale),
                UnityEngine.Random.Range(0.1f, maxScale)
            );

            input[i] = Matrix4x4.TRS(position, rotation, scale);
        }
    }

    private Vector3 per_playerPos = Vector3.zero;
    private Quaternion per_playerRot = Quaternion.identity;

    public bool isCameraChange()
    {
        if (per_playerPos != mainCamera.transform.position || per_playerRot != mainCamera.transform.rotation)
        {
            per_playerPos = mainCamera.transform.position;
            per_playerRot = mainCamera.transform.rotation;
            return true;
        }

        return false;
    }

    private static Vector4 GetPlane(Vector3 normal, Vector3 point) =>
        new Vector4(normal.x, normal.y, normal.z, -Vector3.Dot(normal, point));

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
        points[0] = farCenterPoint - up - right; // left-bottom
        points[1] = farCenterPoint - up + right; // right-bottom
        points[2] = farCenterPoint + up - right; // left-up
        points[3] = farCenterPoint + up + right; // right-up
        return points;
    }

    private static float4[] GetFrustumPlanes(Camera camera, float4[] planes = null)
    {
        if (planes == null) planes = new float4[6];
        Transform transform = camera.transform;
        Vector3 cameraPosition = transform.position;
        Vector3[] points = GetCameraFarClipPlanePoint(camera);

        planes[0] = GetPlane(cameraPosition, points[0], points[2]); // left
        planes[1] = GetPlane(cameraPosition, points[3], points[1]); // right
        planes[2] = GetPlane(cameraPosition, points[1], points[0]); // bottom
        planes[3] = GetPlane(cameraPosition, points[2], points[3]); // up
        planes[4] = GetPlane(-transform.forward, transform.position + transform.forward * camera.nearClipPlane); // near
        planes[5] = GetPlane(transform.forward, transform.position + transform.forward * camera.farClipPlane); // far

        return planes;
    }

    // Update is called once per frame
    void Update()
    {
        if (isCameraChange())
        {
            drawBounds.center = mainCamera.transform.position;
            outputCount[0] = 0;
            frustumPlanes.CopyFrom(GetFrustumPlanes(mainCamera, frustumPlanesArray));
            var job = new FrustumCullingJob();
            job.input = input;
            job.outputCount = outputCount;
            job.output = output;
            job.boxCenter = meshBounds.center;
            job.boxExtents = meshBounds.extents;
            job.cameraPlanes = frustumPlanes;
            var jobHandle = job.Schedule(instanceCount, instanceCount);
            JobHandle.ScheduleBatchedJobs();
            jobHandle.Complete();
            outputLength = outputCount[0];
            instanceOutputBuffer.SetData(output, 0, 0, outputLength);
        }

        Graphics.DrawMeshInstancedProcedural(mesh, 0, material, drawBounds, outputLength, mpb);
    }

    private void OnDestroy()
    {
        if (input.IsCreated)
            input.Dispose();
        if (frustumPlanes.IsCreated)
            frustumPlanes.Dispose();
        if (outputCount.IsCreated)
            outputCount.Dispose();
        if (output.IsCreated)
            output.Dispose();
        instanceOutputBuffer?.Release();
    }
}
