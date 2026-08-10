Shader "WeatherDrivenSolarPanel/DustOverlay"
{
    Properties
    {
        _DustColor ("Dust Color", Color) = (0.74, 0.59, 0.40, 1)
        _DustAmount ("Dust Amount", Range(0, 1)) = 0
        _NoiseScale ("Noise Scale", Float) = 0.55
        _Seed ("Seed", Float) = 0
        _MaxOpacity ("Maximum Opacity", Range(0, 1)) = 0.88
        _LightScale ("Light Scale", Range(0, 2)) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+15"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
        }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Back
            ZWrite Off
            ZTest LEqual
            Offset -1, -1

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            float4 _DustColor;
            float _DustAmount;
            float _NoiseScale;
            float _Seed;
            float _MaxOpacity;
            float _LightScale;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 objectPos : TEXCOORD2;
                float3 worldPos : TEXCOORD3;
            };

            v2f vert(appdata input)
            {
                v2f output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                output.worldNormal = UnityObjectToWorldNormal(input.normal);
                output.objectPos = input.vertex.xyz;
                output.worldPos = mul(unity_ObjectToWorld, input.vertex).xyz;
                return output;
            }

            float hash21(float2 value)
            {
                value = frac(value * float2(127.1, 311.7) + _Seed * 0.017);
                value += dot(value, value.yx + 19.19);
                return frac(value.x * value.y * 43758.5453);
            }

            float valueNoise(float2 value)
            {
                float2 cell = floor(value);
                float2 local = frac(value);
                local = local * local * local * (local * (local * 6.0 - 15.0) + 10.0);

                float bottom = lerp(hash21(cell), hash21(cell + float2(1, 0)), local.x);
                float top = lerp(hash21(cell + float2(0, 1)), hash21(cell + 1), local.x);
                return lerp(bottom, top, local.y);
            }

            float fbm(float2 value)
            {
                float noise = 0.0;
                float amplitude = 0.52;
                float2 offset = float2(_Seed * 0.13, _Seed * 0.29);
                for (int i = 0; i < 5; i++)
                {
                    noise += valueNoise(value + offset) * amplitude;
                    value = mul(float2x2(1.6, 1.1, -1.2, 1.5), value) * 2.03;
                    offset += float2(13.1, 8.7);
                    amplitude *= 0.5;
                }
                return saturate(noise);
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float dust = saturate(_DustAmount);

                // Object-space sampling keeps the pattern attached through vessel motion and
                // floating-origin shifts; the per-renderer seed still separates cloned meshes.
                float scale = max(_NoiseScale, 0.05);
                float3 objectPosition = input.objectPos;
                float2 sampleUv = float2(
                    objectPosition.x * 0.37 + objectPosition.z * 0.29,
                    objectPosition.y * 0.41 + objectPosition.x * 0.19) * scale;
                sampleUv += float2(_Seed * 1.7, _Seed * -1.1);
                // Tiny UV jitter only for micro breakup, not the main shape.
                sampleUv += (input.uv - 0.5) * 0.35;

                float2 warp = float2(
                    fbm(sampleUv * 0.45 + 2.6),
                    fbm(sampleUv * 0.45 + 19.4));
                float2 warped = sampleUv + (warp - 0.5) * 2.4;

                float blot = fbm(warped);
                float speck = fbm(warped * 3.4 + 11.2);
                // No anisotropic streak term — that created the repeating diagonal bands.
                float noise = blot * 0.7 + speck * 0.3;

                // Irregular patch mask: random blotches at low/mid dust.
                float threshold = lerp(0.86, -0.08, pow(dust, 0.8));
                float edge = lerp(0.18, 0.07, dust);
                float patches = smoothstep(threshold - edge, threshold + edge * 0.6, noise);

                // Growing base film so 100% reads as nearly full coverage, not sparse streaks.
                float baseFilm = smoothstep(0.0, 0.4, dust);
                baseFilm = pow(baseFilm, 0.85);
                float coverage = saturate(lerp(patches, 1.0, baseFilm * dust));
                coverage = max(coverage, patches * dust);
                coverage *= smoothstep(0.001, 0.04, dust);

                // Density varies inside the film so full cover still looks dusty, not flat paint.
                float density = lerp(0.62, 1.0, noise);

                float3 normal = normalize(input.worldNormal);
                float3 lightDirection = normalize(UnityWorldSpaceLightDir(input.worldPos));
                float nDotL = saturate(dot(normal, lightDirection));
                // Soft wrap: keep a little day/night cue, but avoid sun-facing dust washing to pale beige.
                float wrap = saturate(nDotL * 0.45 + 0.55);
                float lightColor = max(max(_LightColor0.r, _LightColor0.g), _LightColor0.b);
                float shade = saturate(wrap * saturate(lightColor) * saturate(_LightScale));

                // Narrow luminance range so specular glare under the overlay doesn't erase the dust read.
                float3 litDust = _DustColor.rgb * lerp(0.55, 0.92, shade);
                float alpha = saturate(coverage * dust * _MaxOpacity * density);
                // Extra opacity when facing the sun — counters bright panel specular showing through.
                alpha = saturate(alpha * lerp(1.0, 1.18, nDotL));
                float3 colour = lerp(litDust * 0.78, litDust, saturate(coverage + 0.2));
                return fixed4(colour, alpha);
            }
            ENDCG
        }
    }

    Fallback Off
}
