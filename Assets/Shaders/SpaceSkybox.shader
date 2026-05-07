Shader "Skybox/SpaceSkybox"
{
    Properties
    {
        _StarDensity      ("Star Density",     Range(0.0001, 0.05)) = 0.006
        _StarBrightness   ("Star Brightness",  Range(0, 6))         = 2.4
        _NebulaIntensity  ("Nebula Intensity", Range(0, 2))         = 0.6
        _NebulaScale      ("Nebula Scale",     Range(0.5, 5))       = 2.0
        _NebulaColorA     ("Nebula Color A",   Color)               = (0.45, 0.18, 0.65, 1)
        _NebulaColorB     ("Nebula Color B",   Color)               = (0.06, 0.22, 0.55, 1)
        _DeepColor        ("Deep Space Color", Color)               = (0.004, 0.008, 0.018, 1)
        _Exposure         ("Exposure",         Range(0, 4))         = 1.0
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings   { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };

            float  _StarDensity;
            float  _StarBrightness;
            float  _NebulaIntensity;
            float  _NebulaScale;
            float4 _NebulaColorA;
            float4 _NebulaColorB;
            float4 _DeepColor;
            float  _Exposure;

            float hash13(float3 p)
            {
                p  = frac(p * float3(0.1031, 0.1030, 0.0973));
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float noise3(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float n000 = hash13(i + float3(0,0,0));
                float n100 = hash13(i + float3(1,0,0));
                float n010 = hash13(i + float3(0,1,0));
                float n110 = hash13(i + float3(1,1,0));
                float n001 = hash13(i + float3(0,0,1));
                float n101 = hash13(i + float3(1,0,1));
                float n011 = hash13(i + float3(0,1,1));
                float n111 = hash13(i + float3(1,1,1));

                float a = lerp(n000, n100, f.x);
                float b = lerp(n010, n110, f.x);
                float c = lerp(n001, n101, f.x);
                float d = lerp(n011, n111, f.x);

                float ab = lerp(a, b, f.y);
                float cd = lerp(c, d, f.y);
                return lerp(ab, cd, f.z);
            }

            float fbm(float3 p)
            {
                float v = 0;
                float a = 0.5;
                [unroll]
                for (int i = 0; i < 5; i++)
                {
                    v += a * noise3(p);
                    p *= 2.05;
                    a *= 0.5;
                }
                return v;
            }

            float starLayer(float3 dir, float density, float scale)
            {
                float3 p     = dir * scale;
                float3 cell  = floor(p);
                float3 local = frac(p);

                float h1 = hash13(cell);
                float th = 1.0 - density;
                if (h1 < th) return 0;

                float strength = saturate((h1 - th) / max(1e-5, 1.0 - th));

                float h2 = hash13(cell + 1.31);
                float h3 = hash13(cell + 7.42);
                float h4 = hash13(cell + 13.53);
                float3 center = float3(h2, h3, h4);

                float d = length(local - center);
                float falloff = exp(-d * d * 70);
                return falloff * pow(strength, 1.5);
            }

            Varyings vert(Attributes IN)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                o.dir = IN.positionOS.xyz;
                return o;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 dir = normalize(IN.dir);

                float n  = fbm(dir * _NebulaScale);
                float n2 = fbm(dir * _NebulaScale * 2.3 + 12);

                float nebulaA = smoothstep(0.45, 0.75, n);
                float nebulaB = smoothstep(0.55, 0.85, n2);

                half3 col = _DeepColor.rgb;
                col += _NebulaColorB.rgb * nebulaA * _NebulaIntensity * 0.55;
                col += _NebulaColorA.rgb * nebulaB * _NebulaIntensity;

                float s1 = starLayer(dir,           _StarDensity,        200);
                float s2 = starLayer(dir + 7.7,     _StarDensity * 0.4,   90);
                float s3 = starLayer(dir + 13.13,   _StarDensity * 0.15,  35) * 1.4;
                float starsTotal = s1 + s2 + s3;

                float colorH = hash13(floor(dir * 220) + 31);
                half3 starCol = lerp(half3(0.7, 0.85, 1.0), half3(1.0, 0.92, 0.7), colorH);

                col += starCol * starsTotal * _StarBrightness;

                col *= _Exposure;
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
    Fallback "Skybox/Procedural"
}
