Shader "Custom/RenderVegetationURP"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
            #include "UnityInstancing.cginc"
            
            #pragma multi_compile_instancing
            #include "Assets\Shaders\GPUInstanceIndirect.cginc"
            #pragma instancing_options procedural:setup

            struct Attributes
            {
                float3 positionOS:POSITION;
                float2 baseUV:TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS:SV_POSITION;
                float2 baseUV:VAR_BASE_UV;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            fixed4 _Color;

            Varyings vert (Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v,o);
                o.positionCS = UnityObjectToClipPos(v.positionOS);
                o.baseUV=v.baseUV;
                return o;
            }

            fixed4 frag (Varyings i) : SV_Target
            {
                return _Color;
            }
            ENDHLSL
        }
    }
    FallBack "Diffuse"
}