Shader "WeatherDrivenSolarPanel/DustOverlay"
{
    Properties
    {
        _DustColor ("Dust Color", Color) = (0.74, 0.59, 0.40, 1)
        _DustAmount ("Dust Amount", Range(0, 1)) = 0
        _DustStrength ("Dust Strength", Range(0, 1)) = 1
        _NoiseScale ("Noise Scale", Float) = 1
        _DustAxisU ("Dust Projection U", Vector) = (1, 0, 0, 0)
        _DustAxisV ("Dust Projection V", Vector) = (0, 1, 0, 0)
        _DustExtent ("Dust Projection Extent", Vector) = (4, 4, 0, 0)
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
            float _DustStrength;
            float _NoiseScale;
            float4 _DustAxisU;
            float4 _DustAxisV;
            float4 _DustExtent;
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

            float2 hash22(float2 value)
            {
                return float2(
                    hash21(value + float2(17.17, 43.71)),
                    hash21(value + float2(91.73, 12.37)));
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
                float amplitude = 0.5;
                float2 offset = float2(_Seed * 0.13, _Seed * 0.29);
                // Milder rotation than the old 1.6/1.1 matrix — that pulled soft blobs into diagonals.
                for (int i = 0; i < 5; i++)
                {
                    noise += valueNoise(value + offset) * amplitude;
                    value = mul(float2x2(1.2, 0.55, -0.55, 1.2), value) * 2.02;
                    offset += float2(13.1, 8.7);
                    amplitude *= 0.5;
                }
                return saturate(noise);
            }

            float clumpDistance(
                float2 delta,
                float2 cell,
                float2 salt,
                float sharpness,
                float edgeNoise)
            {
                float2 direction = normalize(hash22(cell + salt) - 0.5 + float2(0.001, 0.002));
                float2 perpendicular = float2(-direction.y, direction.x);
                float2 local = float2(dot(delta, direction), dot(delta, perpendicular));
                local.y *= lerp(
                    0.76,
                    1.28,
                    hash21(cell + salt + float2(37.1, 9.7)));

                float roundDistance = length(local);
                float squareDistance = max(abs(local.x), abs(local.y)) * 1.10;
                float diamondDistance = (abs(local.x) + abs(local.y)) * 0.78;
                float facetedDistance = lerp(
                    squareDistance,
                    diamondDistance,
                    hash21(cell + salt + float2(71.3, 51.9)));
                float distance = lerp(roundDistance, facetedDistance, sharpness);

                // Round clumps receive more organic edge wobble; sharper clumps retain
                // a few visibly angular shoulders without becoming hard polygons.
                distance += (edgeNoise - 0.5) * lerp(0.18, 0.07, sharpness);
                return distance;
            }

            float growingClumpLayer(
                float2 position,
                float progress,
                float2 salt,
                float occupancy,
                float birthStart,
                float birthSpan,
                float sharpness)
            {
                float2 warp = float2(
                    fbm(position * 0.24 + salt + 2.6),
                    fbm(position * 0.24 + salt + 19.4));
                position += (warp - 0.5) * lerp(0.34, 0.18, sharpness);

                float edgeNoise = fbm(position * 1.55 + salt * 0.17 + 41.7);
                float coverage = 0.0;
                float2 baseCell = floor(position);
                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 cell = baseCell + float2(x, y);
                        float enabled = 1.0 - step(
                            occupancy,
                            hash21(cell + salt + float2(13.7, 93.1)));
                        float2 center = cell + lerp(
                            0.04,
                            0.96,
                            hash22(cell + salt + float2(29.3, 7.1)));

                        float birthRandom = hash21(cell + salt + float2(61.7, 117.3));
                        float birth = birthStart + pow(birthRandom, 1.15) * birthSpan;
                        float deltaProgress = progress - birth;
                        // Grow across the full remaining lifetime so coverage never stalls mid-way.
                        float age = saturate(deltaProgress / max(1.0 - birth, 0.001));
                        // Ease-in-out: slower early / late growth, steadier through the middle.
                        float grown = age * age * (3.0 - 2.0 * age);
                        float active = smoothstep(0.0, 0.02, deltaProgress);
                        float maxRadius = lerp(
                            0.50,
                            0.80,
                            hash21(cell + salt + float2(101.9, 3.7)));
                        float radius = lerp(0.04, maxRadius, grown);
                        float distance = clumpDistance(
                            position - center,
                            cell,
                            salt,
                            sharpness,
                            edgeNoise);
                        float softness = lerp(0.15, 0.075, sharpness);
                        float blob = 1.0 - smoothstep(
                            radius - softness,
                            radius + softness,
                            distance);
                        coverage = max(coverage, blob * active * enabled);
                    }
                }
                return saturate(coverage);
            }

            float starterClump(
                float2 position,
                float2 center,
                float progress,
                float2 salt,
                float birth,
                float sharpness)
            {
                float deltaProgress = progress - birth;
                float age = saturate(deltaProgress / max(1.0 - birth, 0.001));
                float grown = age * age * (3.0 - 2.0 * age);
                float active = smoothstep(0.0, 0.02, deltaProgress);
                float radius = lerp(
                    0.05,
                    lerp(0.68, 0.86, hash21(salt + float2(17.9, 201.7))),
                    grown);
                float edgeNoise = fbm(position * 1.45 + salt + 67.3);
                float distance = clumpDistance(
                    position - center,
                    floor(center),
                    salt,
                    sharpness,
                    edgeNoise);
                float softness = lerp(0.16, 0.08, sharpness);
                return (1.0 - smoothstep(radius - softness, radius + softness, distance))
                    * active;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float dust = saturate(_DustAmount);

                // Visual timeline keyed to PAW dust %:
                //   <10%  → clean
                //   10%   → first clumps
                //   50%   → about a quarter of the panel
                //   100%  → fully covered
                float early = smoothstep(0.10, 0.50, dust);
                float late = smoothstep(0.50, 1.00, dust);
                float progress = early * 0.25 + late * 0.75;

                // C# chooses the two largest object-space bounds axes and normalizes their
                // spans. Long/narrow meshes get more clumps along their long axis without
                // collapsing the short axis into a streak.
                float scale = max(_NoiseScale, 0.05);
                float2 projectedUv = float2(
                    dot(float4(input.objectPos, 1.0), _DustAxisU),
                    dot(float4(input.objectPos, 1.0), _DustAxisV)) * scale;

                // Rotate and offset each renderer's field. This removes shared grid alignment
                // and any apparent symmetry between cloned or mirrored panel meshes.
                float randomAngle = hash21(float2(9.2, 17.4)) * 6.2831853;
                float sineAngle = sin(randomAngle);
                float cosineAngle = cos(randomAngle);
                float2x2 randomRotation = float2x2(
                    cosineAngle,
                    -sineAngle,
                    sineAngle,
                    cosineAngle);
                float2 randomOffset = (hash22(float2(53.7, 101.3)) - 0.5) * 37.0;
                float2 sampleUv = mul(randomRotation, projectedUv) + randomOffset;

                // Large rounded deposits form the main pattern.
                float2 roundUv = sampleUv * 0.82;
                float2 roundSalt = float2(11.7, 83.1);
                float2 roundLocalCenter =
                    (hash22(roundSalt + float2(151.3, 43.7)) - 0.5)
                        * _DustExtent.xy * scale * 0.74;
                float2 roundCenter =
                    (mul(randomRotation, roundLocalCenter) + randomOffset) * 0.82;
                float roundCoverage = growingClumpLayer(
                    roundUv,
                    progress,
                    roundSalt,
                    0.58,
                    0.00,
                    0.78,
                    0.10);
                roundCoverage = max(
                    roundCoverage,
                    starterClump(
                        roundUv,
                        roundCenter,
                        progress,
                        roundSalt,
                        0.00,
                        0.10));

                // A separate, rotated sparse layer adds some sharper-edged deposits.
                float sharpAngle = hash21(float2(181.3, 23.9)) * 6.2831853;
                float sharpSine = sin(sharpAngle);
                float sharpCosine = cos(sharpAngle);
                float2x2 sharpRotation = float2x2(
                    sharpCosine,
                    -sharpSine,
                    sharpSine,
                    sharpCosine);
                float2 sharpOffset = (hash22(float2(211.7, 61.3)) - 0.5) * 19.0;
                float2 sharpUv = mul(sharpRotation, sampleUv * 1.12) + sharpOffset;
                float2 sharpSalt = float2(97.3, 157.1);
                float2 sharpLocalCenter =
                    (hash22(sharpSalt + float2(151.3, 43.7)) - 0.5)
                        * _DustExtent.xy * scale * 0.74;
                float2 sharpCenter = mul(
                    sharpRotation,
                    (mul(randomRotation, sharpLocalCenter) + randomOffset) * 1.12)
                    + sharpOffset;
                float sharpCoverage = growingClumpLayer(
                    sharpUv,
                    progress,
                    sharpSalt,
                    0.40,
                    0.04,
                    0.80,
                    0.52);
                sharpCoverage = max(
                    sharpCoverage,
                    starterClump(
                        sharpUv,
                        sharpCenter,
                        progress,
                        sharpSalt,
                        0.03,
                        0.52));

                float coverage = max(roundCoverage, sharpCoverage * 0.92);
                // Seal leftover gaps only in the last stretch so mid stays ~half covered.
                float finalFill = pow(smoothstep(0.86, 1.0, progress), 1.8);
                coverage = saturate(lerp(coverage, 1.0, finalFill));
                coverage *= smoothstep(0.08, 0.12, dust);

                // Fine noise affects thickness only, never the rounded silhouette.
                float speck = fbm(sampleUv * 2.1 + 11.2);
                float density = lerp(0.68, 1.0, speck);

                float3 normal = normalize(input.worldNormal);
                float3 lightDirection = normalize(UnityWorldSpaceLightDir(input.worldPos));
                float nDotL = saturate(dot(normal, lightDirection));
                float wrap = saturate(nDotL * 0.45 + 0.55);
                float lightColor = max(max(_LightColor0.r, _LightColor0.g), _LightColor0.b);
                float shade = saturate(wrap * saturate(lightColor) * saturate(_LightScale));

                float3 litDust = _DustColor.rgb * lerp(0.55, 0.92, shade);
                float clumpOpacity = lerp(0.64, 1.0, sqrt(dust));
                float alpha = saturate(
                    coverage * clumpOpacity * _MaxOpacity * density * saturate(_DustStrength));
                alpha = saturate(alpha * lerp(1.0, 1.18, nDotL));
                float3 colour = lerp(litDust * 0.78, litDust, saturate(coverage + 0.2));
                return fixed4(colour, alpha);
            }
            ENDCG
        }
    }

    Fallback Off
}
