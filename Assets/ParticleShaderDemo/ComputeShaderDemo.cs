using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public struct ParticleData
{
    public Vector3 pos;//equal to float3 in shader
    public Color color;//equal to float4 in shader
}

public class ComputeShaderDemo : MonoBehaviour
{

    public Camera MainCamera;
    public GameObject Prefab;

    public ComputeShader computeShader;
    public Material material;

    public int particleCounts;


    public int InstanceCounts = 1000000;
    public Vector3Int InstanceExtents = new Vector3Int(500, 500, 500);
    public float RandomMaxScaleValue = 5;

    public ComputeBuffer particleDataBuffer;

    private Matrix4x4[] instances;

    private int kernelId;

    // Start is called before the first frame update
    void Start()
    {
        instances = RandomGenerateInstances(InstanceCounts, InstanceExtents, RandomMaxScaleValue);

        RenderTexture mRenderTexture = new RenderTexture(256, 256, 16);
        mRenderTexture.enableRandomWrite = true;
        mRenderTexture.Create();

        int kernelIndex = computeShader.FindKernel("CSMain");
        material.mainTexture = mRenderTexture;
        computeShader.SetTexture(kernelIndex, "Result", mRenderTexture);

        computeShader.Dispatch(kernelIndex, 256 / 8, 256 / 8, 1);

        particleDataBuffer = new ComputeBuffer(particleCounts, 28);
        ParticleData[] particleDatas = new ParticleData[particleCounts];
        particleDataBuffer.SetData(particleDatas);
        kernelId = computeShader.FindKernel("UpdateParticle");


    }

    private Matrix4x4[] RandomGenerateInstances(int instanceCount, Vector3Int instanceExtents, float maxScale)
    {
        var instances = new Matrix4x4[instanceCount];
        var cameraPos = MainCamera.transform.position;

        for(var i = 0; i < instanceCount; i++)
        {
            var pos = new Vector3(
                cameraPos.x + Random.Range(-instanceExtents.x, instanceExtents.x),
                cameraPos.y + Random.Range(-instanceExtents.y, instanceExtents.y),
                cameraPos.z+ Random.Range(-instanceExtents.z, instanceExtents.z)
                );
            var rot = Quaternion.Euler(Random.Range(0,180), Random.Range(0, 180), Random.Range(0, 180));
            var scl = new Vector3(Random.Range(0.1f, maxScale), Random.Range(0.1f, maxScale), Random.Range(0.1f, maxScale));

            instances[i] = Matrix4x4.TRS(pos, rot, scl);
        }
        return instances;
    }
    

    // Update is called once per frame
    void Update()
    {
        computeShader.SetBuffer(kernelId, "ParticleBuffer", particleDataBuffer);
        computeShader.SetFloat("Time", Time.time);
        computeShader.Dispatch(kernelId, particleCounts / 1000, 1, 1);
        // here,computeShader store the result from gpu to particleDataBuffer in cpu, then pass to vf shader
        material.SetBuffer("_particleDataBuffer", particleDataBuffer);
    }

    void OnRenderObject()
    {
        material.SetPass(0);
        Graphics.DrawProceduralNow(MeshTopology.Points, particleCounts);
    }

    void OnDestroy()
    {
        particleDataBuffer.Release();
        particleDataBuffer = null;
    }
}
