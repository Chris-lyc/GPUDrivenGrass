using System;
using System.Collections;
using System.Collections.Generic;
using GPUDrivenGrassDemo.Runtime;
using UnityEngine;
using UnityEngine.Rendering;

namespace GPUDrivenGrassDemo.Runtime
{
    public class HzbDepthTexMaker : MonoBehaviour
    {
        public RenderTexture hzbDepth;
        public Shader hzbShader;
        private Material hzbMat;

        public static int hzbDepthTextureSize = 1024;

        public bool stopMode;
        
        // Use this for initialization
        public void Start()
        {
            hzbMat = new Material(hzbShader);
            Camera.main.depthTextureMode |= DepthTextureMode.Depth;

            hzbDepth = new RenderTexture(hzbDepthTextureSize, hzbDepthTextureSize, 0, RenderTextureFormat.RHalf);
            hzbDepth.autoGenerateMips = false;

            hzbDepth.useMipMap = true;
            hzbDepth.filterMode = FilterMode.Point;
            hzbDepth.Create();
            GPUDrivenGrass.depthRT = hzbDepth;
        }

        void OnDestroy()
        {
            hzbDepth.Release();
            Destroy(hzbDepth);
        }

        int ID_DepthTexture;
        int ID_InvSize;
        
        private void OnBeginCameraRendering(ScriptableRenderContext arg1, Camera arg2)
        {
            if(arg2.name!="Main Camera")
                return;
            GenerateDepthMip();
        }
        
        // #if UNITY_EDITOR
        // private void Update()
        // {
        //     GenerateDepthMip();
        // }
        // #endif


        private void GenerateDepthMip()
        {
            if (stopMode)
            {
                return;
            }

            int w = hzbDepth.width;
            int h = hzbDepth.height;
            int level = 0;

            RenderTexture lastRt = null;
            if (ID_DepthTexture == 0)
            {
                ID_DepthTexture = Shader.PropertyToID("_DepthTexture");
                ID_InvSize = Shader.PropertyToID("_InvSize");
            }

            RenderTexture tempRT;
            while (h > 8)
            {
                hzbMat.SetVector(ID_InvSize, new Vector4(1.0f / w, 1.0f / h, 0, 0));

                tempRT = RenderTexture.GetTemporary(w, h, 0, hzbDepth.format);
                tempRT.filterMode = FilterMode.Point;
                if (lastRt == null)
                {
                    Graphics.Blit(Shader.GetGlobalTexture("_CameraDepthTexture"), tempRT);
                }
                else
                {
                    hzbMat.SetTexture(ID_DepthTexture, lastRt);
                    Graphics.Blit(null, tempRT, hzbMat);
                    RenderTexture.ReleaseTemporary(lastRt);
                }

                Graphics.CopyTexture(tempRT, 0, 0, hzbDepth, 0, level);
                lastRt = tempRT;

                w /= 2;
                h /= 2;
                level++;
            }

            RenderTexture.ReleaseTemporary(lastRt);
        }
        
        private void OnEnable()
        {
            Debug.Log(nameof(OnEnable));
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        }

        private void OnDisable()
        {
            Debug.Log(nameof(OnDisable));
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        }
    }
}
