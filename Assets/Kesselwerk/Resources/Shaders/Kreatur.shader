// Kesselwerk – Anime-/Cel-Shader für Kreaturen (URP).
// Farbe = Textur × Eckpunktfarbe × _BaseColor. Alpha der Eckpunktfarbe < 1 = leuchtende Stelle.
// Zwei weiche Lichtstufen, Randlicht, leichter Glanz und ein Umriss (zweiter Durchgang).
Shader "Kesselwerk/Kreatur"
{
    Properties
    {
        [MainTexture] _BaseMap ("Textur", 2D) = "white" {}
        [MainColor] _BaseColor ("Farbe", Color) = (1,1,1,1)
        _ShadeColor ("Schattenfarbe", Color) = (0.58,0.55,0.72,1)
        _ShadeStep ("Schattengrenze", Range(-1,1)) = 0.0
        _ShadeSoft ("Weichheit", Range(0.001,0.5)) = 0.07
        _RimColor ("Randlicht", Color) = (1,0.96,0.88,1)
        _RimPower ("Randlicht Schärfe", Range(0.5,8)) = 3.2
        _RimStrength ("Randlicht Menge", Range(0,2)) = 0.4
        _SpecStrength ("Glanz", Range(0,1)) = 0.22
        _OutlineColor ("Umriss", Color) = (0.13,0.09,0.13,1)
        _OutlineWidth ("Umrissbreite", Range(0,0.03)) = 0.006
        _GlowStrength ("Leuchten", Range(0,10)) = 2.2
        _Cutoff ("Cutoff", Range(0,1)) = 0.5
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor, _ShadeColor, _RimColor, _OutlineColor;
            half _ShadeStep, _ShadeSoft, _RimPower, _RimStrength, _SpecStrength, _OutlineWidth, _GlowStrength, _Cutoff;
        CBUFFER_END
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct A { float4 pos:POSITION; float3 n:NORMAL; float2 uv:TEXCOORD0; half4 col:COLOR; };
            struct V2F { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; float3 wp:TEXCOORD1; float3 n:TEXCOORD2; half4 col:COLOR; half fog:TEXCOORD3; };

            V2F vert(A i)
            {
                V2F o;
                VertexPositionInputs p = GetVertexPositionInputs(i.pos.xyz);
                o.pos = p.positionCS; o.wp = p.positionWS;
                o.n = TransformObjectToWorldNormal(i.n);
                o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
                o.col = i.col;
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 frag(V2F i) : SV_Target
            {
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb * i.col.rgb * _BaseColor.rgb;
                float3 n = normalize(i.n);
                float3 v = normalize(GetWorldSpaceViewDir(i.wp));
                Light L = GetMainLight(TransformWorldToShadowCoord(i.wp));
                half ndl = dot(n, L.direction);
                half sh = L.shadowAttenuation;
                half lit = smoothstep(_ShadeStep - _ShadeSoft, _ShadeStep + _ShadeSoft, ndl) * lerp(0.35, 1.0, sh);
                half3 shade = albedo * _ShadeColor.rgb;
                // Eigenfarben bleiben lesbar: farbiges Licht färbt nur leicht
                half lum = max(0.6, dot(L.color, half3(0.299, 0.587, 0.114)));
                half3 lightCol = lerp(half3(lum, lum, lum), L.color, 0.3);
                half3 col = lerp(shade, albedo, lit) * min(lightCol, 1.3);
                // Umgebungslicht (weich, nur Helligkeit, damit Schatten nie schwarz werden)
                half3 sh9 = SampleSH(n);
                col += albedo * dot(sh9, half3(0.299, 0.587, 0.114)) * 0.4;
                // weitere Lichter (Effekte, Fackeln) weich addieren
                #if defined(_ADDITIONAL_LIGHTS)
                uint cnt = GetAdditionalLightsCount();
                for (uint li = 0; li < cnt; li++)
                {
                    Light al = GetAdditionalLight(li, i.wp);
                    half d = saturate(dot(n, al.direction) * 0.5 + 0.5);
                    col += albedo * al.color * al.distanceAttenuation * d * 0.6;
                }
                #endif
                // Glanzpunkt (stilisiert)
                float3 h = normalize(L.direction + v);
                half spec = smoothstep(0.92, 0.97, dot(n, h)) * _SpecStrength * lit;
                col += spec * L.color;
                // Randlicht auf der beleuchteten Seite
                half rim = pow(1.0 - saturate(dot(n, v)), _RimPower);
                col += _RimColor.rgb * rim * _RimStrength * (0.35 + 0.65 * saturate(ndl + 0.4));
                // Leuchten über Alpha der Eckpunktfarbe
                half glow = saturate((1.0 - i.col.a) * 2.0);
                col = lerp(col, albedo * _GlowStrength, glow);
                col = MixFog(col, i.fog);
                return half4(col, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Outline"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            struct A { float4 pos:POSITION; float3 n:NORMAL; half4 col:COLOR; };
            struct V2F { float4 pos:SV_POSITION; half4 col:COLOR; half fog:TEXCOORD0; };
            V2F vert(A i)
            {
                V2F o;
                float4 cs = TransformObjectToHClip(i.pos.xyz);
                float3 nv = mul((float3x3)UNITY_MATRIX_VP, TransformObjectToWorldNormal(i.n));
                float2 off = normalize(nv.xy) * _OutlineWidth * cs.w;
                off.x *= _ScreenParams.y / _ScreenParams.x;
                cs.xy += off * 2.0;
                o.pos = cs; o.col = i.col;
                o.fog = ComputeFogFactor(cs.z);
                return o;
            }
            half4 frag(V2F i) : SV_Target
            {
                half glow = saturate((1.0 - i.col.a) * 2.0);
                half3 c = lerp(_OutlineColor.rgb * lerp(1, i.col.rgb, 0.35), i.col.rgb, glow);
                return half4(MixFog(c, i.fog), 1);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
    FallBack Off
}
