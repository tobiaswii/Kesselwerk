// Kesselwerk – Gras mit Wind (URP, GPU-Instancing). Farbe von unten (dunkel) nach oben (hell).
Shader "Kesselwerk/Gras"
{
    Properties
    {
        _Bottom ("Farbe unten", Color) = (0.12,0.28,0.1,1)
        _Top ("Farbe oben", Color) = (0.55,0.8,0.32,1)
        _Wind ("Wind", Range(0,1)) = 0.35
        _WindSpeed ("Windtempo", Range(0,5)) = 1.6
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Bottom, _Top; half _Wind, _WindSpeed;
            CBUFFER_END
            struct A { float4 pos:POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V2F { float4 pos:SV_POSITION; float h:TEXCOORD0; float3 wp:TEXCOORD1; half var:TEXCOORD2; half fog:TEXCOORD3; };
            V2F vert(A i)
            {
                UNITY_SETUP_INSTANCE_ID(i);
                V2F o;
                float3 wp = TransformObjectToWorld(i.pos.xyz);
                float3 root = TransformObjectToWorld(float3(0, 0, 0));
                float h = i.uv.y;
                float t = _Time.y * _WindSpeed;
                float gust = sin(root.x * 0.35 + t) * 0.6 + sin(root.z * 0.5 + t * 1.7) * 0.4;
                wp.x += gust * _Wind * h * h * 0.35;
                wp.z += sin(root.x * 0.8 + t * 1.3) * _Wind * h * h * 0.12;
                o.pos = TransformWorldToHClip(wp);
                o.h = h; o.wp = wp;
                o.var = frac(sin(dot(root.xz, float2(12.9898, 78.233))) * 43758.5453);
                o.fog = ComputeFogFactor(o.pos.z);
                return o;
            }
            half4 frag(V2F i) : SV_Target
            {
                half3 c = lerp(_Bottom.rgb, _Top.rgb * (0.85 + i.var * 0.3), saturate(i.h * 1.15));
                Light L = GetMainLight(TransformWorldToShadowCoord(i.wp));
                half sh = lerp(0.55, 1.0, L.shadowAttenuation);
                half3 lum = dot(L.color, half3(0.299, 0.587, 0.114));
                c *= (0.55 + 0.6 * sh) * lerp(lum, L.color, 0.4) ;
                c += c * SampleSH(half3(0, 1, 0)) * 0.35;
                return half4(MixFog(c, i.fog), 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
