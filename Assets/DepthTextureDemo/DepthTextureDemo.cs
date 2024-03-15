using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DepthTextureDemo : MonoBehaviour
{

    public RenderTexture m_depthTexture;//depth texture with mip map
    public RenderTexture DepthTexture => m_depthTexture;

    private int m_depthTextureSize = 0;
    public int DepthTextureSize
    {
        get
        {
            if (m_depthTextureSize == 0)
                m_depthTextureSize = Mathf.NextPowerOfTwo(Mathf.Max(Screen.width, Screen.height));
            return m_depthTextureSize;
        }
    }


    const RenderTextureFormat m_depthTextureFormat = RenderTextureFormat.RHalf;//depth value domain: 0-1,single channel




    public Shader DepthTextureShader;//the shader to generate mipmap
    private Material DepthTextureMaterial;
    private int CameraDepthTextureShaderID;
    // Start is called before the first frame update
    void Start()
    {
        Camera.main.depthTextureMode |= DepthTextureMode.Depth;
        DepthTextureMaterial = new Material(DepthTextureShader);
        CameraDepthTextureShaderID = Shader.PropertyToID("_CameraDepthTexture");

        InitDepthTexture();
    }

    private void InitDepthTexture()
    {
        if (m_depthTexture != null) return;
        m_depthTexture = new RenderTexture(DepthTextureSize, DepthTextureSize, 0, m_depthTextureFormat);
        m_depthTexture.autoGenerateMips = false;
        m_depthTexture.useMipMap = true;
        m_depthTexture.filterMode = FilterMode.Point;
        m_depthTexture.Create();
    }
#if UNITY_EDITOR
    void Update()
    {
#else
        void OnPostRender()
    {
#endif
        int w = m_depthTexture.width;
        int mipmapLevel = 0;

        RenderTexture currentRenderTexture = null;//cur mipmapLevel's mipmap
        RenderTexture preRenderTexture = null;//mipmapLevel-1's mipmap

        //if mipmap'width > 8, calculate the next level mipmap
        while (w > 8)
        {
            currentRenderTexture = RenderTexture.GetTemporary(w, w, 0, m_depthTextureFormat);
            currentRenderTexture.filterMode = FilterMode.Point;
            if (preRenderTexture == null)
            {
                //Mipmap[0],that is copy the original depth texture
                Graphics.Blit(Shader.GetGlobalTexture(CameraDepthTextureShaderID), currentRenderTexture);
            }
            else
            {
                //let Mipmap[i] Blit to Mipmap[i+1]
                Graphics.Blit(preRenderTexture, currentRenderTexture, DepthTextureMaterial);
                RenderTexture.ReleaseTemporary(preRenderTexture);
            }
            Graphics.CopyTexture(currentRenderTexture, 0, 0, m_depthTexture, 0, mipmapLevel);
            preRenderTexture = currentRenderTexture;

            w /= 2;
            mipmapLevel++;
        }
        RenderTexture.ReleaseTemporary(preRenderTexture);
    }

}
