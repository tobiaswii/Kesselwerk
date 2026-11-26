// Kesselwerk – Kreaturen-Shader (URP), moderner, weicher Stil.
// Farbe = Textur × Eckpunktfarbe (inkl. eingebackener Umgebungsverdeckung) × _BaseColor.
// Weiches Licht mit Lichtumgriff, Fellstruktur (triplanares Rauschen), Fellglanz, flauschiger Rand,
// leichtes Durchscheinen, feiner farbiger Umriss. Alpha der Eckpunktfarbe < 1 = leuchtende Stelle.
Shader "Kesselwerk/Kreatur"
{
    Properties
    {
        [MainTexture] _BaseMap ("Textur", 2D) = "white" {}
        [MainColor] _BaseColor ("Farbe", Color) = (1,1,1,1)
        _FurTex ("Fellrauschen", 2D) = "gray" {}
        _FurScale ("Fellfeinheit", Range(1,60)) = 22
        _FurStrength ("Fellstärke", Range(0,1)) = 0.35
        _Wrap ("Lichtumgriff", Range(0,1)) = 0.45
        _Toon ("Comic-Anteil", Range(0,1)) = 0.25
        _ShadeColor ("Schattenfarbe", Color) = (0.62,0.58,0.78,1)
        _SheenColor ("Fellglanz", Color) = (1,0.96,0.9,1)
        _Sheen ("Fellglanz Stärke", Range(0,1)) = 0.22
        _RimColor ("Randlicht", Color) = (1,0.97,0.9,1)
        _RimStrength ("Randlicht Menge", Range(0,2)) = 0.45
        _Translucency ("Durchscheinen", Range(0,1)) = 0.18
        _OutlineWidth ("Umrissbreite", Range(0,0.02)) = 0.0028
        _OutlineDarken ("Umriss Dunkelheit", Range(0,1)) = 0.55
        _GlowStrength ("Leuchten", Range(0,10)) = 2.2
        _Cutoff ("Cutoff", Range(0,1)) = 0.5
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST; float4 _FurTex_ST;
            half4 _BaseColor, _ShadeColor, _SheenColor, _RimColor;
            half _FurScale, _FurStrength, _Wrap, _Toon, _Sheen, _RimStrength, _Translucency, _OutlineWidth, _OutlineDarken, _GlowStrength, _Cutoff;
        CBUFFER_END
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_FurTex); SAMPLER(sampler_FurTex);

        half Fur(float3 p, float3 n)
        {
            float3 w = abs(n); w = w / (w.x + w.y + w.z + 1e-4);
            p *= _FurScale;
            half a = SAMPLE_TEXTURE2D(_FurTex, sampler_FurTex, p.yz).r;
            half b = SAMPLE_TEXTURE2D(_FurTex, sampler_FurTex, p.xz).r;
            half c = SAMPLE_TEXTURE2D(_FurTex, sampler_FurTex, p.xy).r;
            return a * w.x + b * w.y + c * w.z;
        }
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
            struct V2F { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; float3 wp:TEXCOORD1; float3 n:TEXCOORD2; float3 op:TEXCOORD3; float3 on:TEXCOORD4; half4 col:COLOR; half fog:TEXCOORD5; };

            V2F vert(A i)
            {
                V2F o;
                VertexPositionInputs p = GetVertexPositionInputs(i.pos.xyz);
                o.pos = p.positionCS; o.wp = p.positionWS;
                o.n = TransformObjectToWorldNormal(i.n);
                o.op = i.pos.xyz; o.on = i.n;
                o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
                o.col = i.col;
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 frag(V2F i) : SV_Target
            {
                half glow = saturate((1.0 - i.col.a) * 2.0);
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb * i.col.rgb * _BaseColor.rgb;
                half fur = Fur(i.op, normalize(i.on));
                albedo *= lerp(1.0, 0.78 + fur * 0.44, _FurStrength * (1 - glow));

                float3 n = normalize(i.n);
                float3 v = normalize(GetWorldSpaceViewDir(i.wp));
                Light L = GetMainLight(TransformWorldToShadowCoord(i.wp));
                half ndl = dot(n, L.direction);
                half sh = lerp(0.4, 1.0, L.shadowAttenuation);
                half soft = saturate((ndl + _Wrap) / (1.0 + _Wrap));
                soft = soft * soft * (3 - 2 * soft);
                half toon = smoothstep(-0.05, 0.12, ndl);
                half lit = lerp(soft, toon, _Toon) * sh;

                half lum = max(0.6, dot(L.color, half3(0.299, 0.587, 0.114)));
                half3 lightCol = min(lerp(half3(lum, lum, lum), L.color, 0.3), 1.3);
                half3 col = lerp(albedo * _ShadeColor.rgb, albedo, lit) * lightCol;
                half3 sh9 = SampleSH(n);
                col += albedo * dot(sh9, half3(0.299, 0.587, 0.114)) * 0.42;
                #if defined(_ADDITIONAL_LIGHTS)
                uint cnt = GetAdditionalLightsCount();
                for (uint li = 0; li < cnt; li++)
                {
                    Light al = GetAdditionalLight(li, i.wp);
                    half d = saturate(dot(n, al.direction) * 0.5 + 0.5);
                    col += albedo * al.color * al.distanceAttenuation * d * 0.6;
                }
                #endif
                half back = saturate(dot(-n, L.direction)) * pow(saturate(dot(v, -L.direction) * 0.5 + 0.5), 2);
                col += albedo * half3(1.0, 0.75, 0.6) * back * _Translucency;
                float3 h = normalize(L.direction + v);
                half sheen = pow(saturate(dot(n, h)), 16) * _Sheen * lit * (0.6 + fur * 0.8);
                col += _SheenColor.rgb * sheen;
                half fres = 1.0 - saturate(dot(n, v));
                half rim = pow(fres, 2.6) * (0.55 + fur * 0.9);
                col += _RimColor.rgb * rim * _RimStrength * (0.35 + 0.65 * saturate(ndl + 0.5));
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
                half3 c = lerp(i.col.rgb * _BaseColor.rgb * (1 - _OutlineDarken), i.col.rgb * _GlowStrength, glow);
                return half4(MixFog(c, i.fog), 1);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
    FallBack Off
}
