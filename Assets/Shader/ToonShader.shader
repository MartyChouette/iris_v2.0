Shader "Unlit/ToonShader"
{
    Properties
    {
        [HDR]_Color("Albedo", Color) = (1,1,1,1)
        _MainTex ("Texture", 2D) = "white" {}
       
        [Header(Stencil)]
        _Stencil ("Stencil ID [0;255]", Float) = 0
        _ReadMask ("ReadMask [0;255]", Int) = 255
        _WriteMask ("WriteMask [0;255]", Int) = 255
        [Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp ("Stencil Comparison", Int) = 0
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilOp ("Stencil Operation", Int) = 0
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilFail ("Stencil Fail", Int) = 0
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilZFail ("Stencil ZFail", Int) = 0
       
        [Header(Rendering)]
        _Offset("Offset", float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Culling ("Cull Mode", Int) = 2
        [Enum(Off,0,On,1)] _ZWrite("ZWrite", Int) = 1
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Int) = 4
        [Enum(None,0,Alpha,1,Red,8,Green,4,Blue,2,RGB,14,RGBA,15)] _ColorMask("Color Mask", Int) = 15
    }
   
    SubShader
    {
        // 1. Tell Unity this is a URP shader
        Tags { 
            "RenderType"="Opaque" 
            "Queue" = "Geometry" 
            "RenderPipeline" = "UniversalPipeline" 
        }

        // Apply Stencil settings
        Stencil
        {
            Ref [_Stencil]
            ReadMask [_ReadMask]
            WriteMask [_WriteMask]
            Comp [_StencilComp]
            Pass [_StencilOp]
            Fail [_StencilFail]
            ZFail [_StencilZFail]
        }

        // --- Pass 1: Main Rendering ---
        Pass
        {
            Name "Unlit"
            // URP uses "UniversalForward" for the main opaque pass
            Tags { "LightMode" = "UniversalForward" }

            LOD 100
            Cull [_Culling]
            Offset [_Offset], [_Offset]
            ZWrite [_ZWrite]
            ZTest [_ZTest]
            ColorMask [_ColorMask]
           
            HLSLPROGRAM
            // URP prefers HLSL over CG
            #pragma target 2.0
            #pragma vertex vert
            #pragma fragment frag
            
            // Core URP Library
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // Structs
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float2 uv : TEXCOORD0;
                float4 positionCS : SV_POSITION;
            };

            // Textures and Samplers must be declared outside the CBuffer
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            // SRP Batcher Compatibility
            // All float/vector properties must be inside this block
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float4 _MainTex_ST;
            CBUFFER_END

            Varyings vert (Attributes input)
            {
                Varyings output;
                // Convert Object Space to Homogenous Clip Space (URP equivalent of UnityObjectToClipPos)
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                
                // Transform UVs
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }
           
            half4 frag (Varyings input) : SV_Target
            {
                // Sample texture
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                return texColor * _Color;
            }
            ENDHLSL
        }
       
        // --- Pass 2: Shadow Caster ---
        // Necessary if you want this unlit object to cast shadows on other objects
Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            LOD 80
            Cull [_Culling]
            Offset [_Offset], [_Offset]
            ZWrite [_ZWrite]
            ZTest [_ZTest]
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            
            // Core URP libraries
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };
            
            // This is required to access the shadow bias variables safely
            float3 _LightDirection;
            float3 _LightPosition;

            Varyings vert(Attributes input)
            {
                Varyings output;
                
                // 1. Get World Position and Normal
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                // 2. Apply Shadow Bias manually (works in older and newer URP)
                // _LightDirection is provided by URP during the shadow pass
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));

                // 3. Fix Z-fighting for shadows (Shadow pancaking)
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif

                output.positionCS = positionCS;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}