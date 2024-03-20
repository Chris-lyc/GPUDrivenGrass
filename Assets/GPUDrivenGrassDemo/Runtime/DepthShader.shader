Shader "HZB/HZBBuild" {

    Properties {
        [HideInInspector] _DepthTexture("Depth Texture", 2D) = "black" {}
        [HideInInspector] _InvSize("Inverse Mipmap Size", Vector) = (0, 0, 0, 0) //x,y = (1/MipMapSize.x, 1/MipMapSize.y), zw = (0, 0)
    }

    SubShader {
    	Tags {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="Transparent"
            "Queue"="Transparent"
    	}
    	
    	Cull Off 
        ZWrite Off 
        ZTest Always
            
		Pass {
			Name "HZBBuild"

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #pragma target 3.0
            #pragma vertex HZBVert
            #pragma fragment HZBBuildFrag
            #pragma enable_d3d11_debug_symbols

            TEXTURE2D(_DepthTexture);
            SAMPLER(sampler_DepthTexture);
            TEXTURE2D(_CameraDepthTexture);
            SAMPLER(sampler_CameraDepthTexture);
            
 			float4 _InvSize;

            struct HZBAttributes
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct HZBVaryings
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            inline float HZBReduce(Texture2D mainTex,SamplerState sampler_mainTex, float2 inUV, float2 invSize)
            {
                float4 depth;
                float2 uv0 = inUV + float2(-0.25f, -0.25f) * invSize;
                float2 uv1 = inUV + float2(0.25f, -0.25f) * invSize;
                float2 uv2 = inUV + float2(-0.25f, 0.25f) * invSize;
                float2 uv3 = inUV + float2(0.25f, 0.25f) * invSize;

            	depth.x = SAMPLE_TEXTURE2D(mainTex, sampler_mainTex, uv0);
            	depth.y = SAMPLE_TEXTURE2D(mainTex, sampler_mainTex, uv1);
            	depth.z = SAMPLE_TEXTURE2D(mainTex, sampler_mainTex, uv2);
            	depth.w = SAMPLE_TEXTURE2D(mainTex, sampler_mainTex, uv3);

            	#if defined(UNITY_REVERSED_Z)
					return min(min(depth.x, depth.y), min(depth.z, depth.w));
				#else
					return max(max(depth.x, depth.y), max(depth.z, depth.w));
				#endif
            }

            HZBVaryings HZBVert(HZBAttributes v)
            {
                HZBVaryings o;
                o.vertex = TransformWorldToHClip(v.vertex.xyz);
                o.uv = v.uv;

                return o;
            }

			float4 HZBBuildFrag(HZBVaryings input) : SV_Target
			{	   
				float2 invSize = _InvSize.xy;
				float2 inUV = input.uv;

				float depth = HZBReduce(_DepthTexture, sampler_DepthTexture, inUV, invSize);

				return float4(depth, 0, 0, 1.0f);
			}
            
			ENDHLSL
		}
    }
	FallBack "Hidden/Shader Graph/FallbackError"
}