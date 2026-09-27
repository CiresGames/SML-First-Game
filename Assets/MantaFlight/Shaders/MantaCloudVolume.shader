// URP volume adaptation of ProceduralClouds: baked Worley shape/detail,
// edge erosion, Beer-Lambert extinction and light marching.
Shader "Manta/Procedural Cloud Volume"
{
    Properties
    {
        _ShapeNoise ("Package shape noise", 3D) = "white" {}
        _DetailNoise ("Package detail noise", 3D) = "white" {}
        _SunTint ("Sunlit ivory", Color) = (1,.88,.69,1)
        _ShadowTint ("Cool interior", Color) = (.36,.48,.61,1)
        _Density ("Density", Float) = .8
        _Wind ("Wind", Vector) = (2.8,.12,.8,0)
        _Evolution ("Evolution", Float) = .35
        _Steps ("Ray steps", Float) = 64
        _Seed ("Variation", Float) = 0
        _ShapeParams ("Coverage / footprint / thickness / noise scale", Vector) = (.55,1,1,1)
        _DetailParams ("Erosion / lenticular / layers", Vector) = (.65,0,0,0)
        _FlowOffset ("Integrated wind", Vector) = (0,0,0,0)
        _DetailPhase ("Integrated evolution", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-50" "RenderType"="Transparent" }
        Pass
        {
            Name "Cloud Volume"
            Tags { "LightMode"="UniversalForward" }
            Blend One OneMinusSrcAlpha
            Cull Front ZWrite Off ZTest Always
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            TEXTURE3D(_ShapeNoise); SAMPLER(sampler_ShapeNoise);
            TEXTURE3D(_DetailNoise); SAMPLER(sampler_DetailNoise);
            CBUFFER_START(UnityPerMaterial)
            float4 _SunTint, _ShadowTint, _Wind;
            float4 _ShapeParams, _DetailParams, _FlowOffset, _CloudLod;
            float _Density, _Evolution, _Steps, _Seed, _DetailPhase;
            CBUFFER_END
            float4 _LobeCenters[5],_LobeRadii[5];
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                return o;
            }
            float DensityAt(float3 world)
            {
                float3 local = TransformWorldToObject(world);
                local.xz /= max(.2,_ShapeParams.y);
                local.y /= max(.06,_ShapeParams.z);
                if(max(abs(local.x),max(abs(local.y),abs(local.z)))>=.495 || local.y<=-.41) return 0;
                float3 uv = (world - _FlowOffset.xyz) * .0017 * max(.3,_ShapeParams.w) + _Seed;
                // Slow independent vertical/diagonal evolution, without new texture samples.
                uv += float3(.0011,.0023,-.0008)*_DetailPhase;
                float4 shape = SAMPLE_TEXTURE3D_LOD(_ShapeNoise,sampler_ShapeNoise,uv,0);
                float3 warped=local+(shape.gba-.5)*float3(.16,.09,.16)*(1-_DetailParams.y);
                float billows=0;
                [unroll] for(int l=0;l<5;l++)
                {
                    float3 q=(warped-_LobeCenters[l].xyz)/max(.01,_LobeRadii[l].xyz);
                    float v=saturate(1-dot(q,q));
                    // Smooth union joins towers into one formation, without separate draw calls.
                    billows=max(billows,v)+min(billows,v)*.22;
                }
                float lens=saturate((1-length(local*float3(2.04,2.15,2.04)))*3);
                float envelope=lerp(saturate(billows*2.2),lens,_DetailParams.y);
                float boundsFade=1-smoothstep(.42,.495,max(abs(local.x),max(abs(local.y),abs(local.z))));
                float height=smoothstep(-.41,-.28,local.y)*boundsFade;
                float baseShape = dot(shape,float4(.62,.23,.1,.05));
                baseShape = lerp(baseShape,.79+baseShape*.08,_DetailParams.y);
                float threshold = lerp(.5,.065,saturate(_ShapeParams.x));
                // Detail erosion can only remove density. Empty samples need no detail lookup.
                if(baseShape*envelope*height<=threshold) return 0;
                float3 duv = uv * 5.1 + float3(0,-1,.3)*_DetailPhase*.007;
                float detail = .5;
                if(_CloudLod.y<.999) detail=lerp(dot(SAMPLE_TEXTURE3D_LOD(_DetailNoise,sampler_DetailNoise,duv,0).rgb,float3(.6,.3,.1)),.5,_CloudLod.y);
                float erosion = (1-detail) * pow(saturate(1-baseShape),3) * _DetailParams.x;
                float layerMask = lerp(1,smoothstep(-.7,.4,cos(local.y*42)),_DetailParams.z);
                float fade = smoothstep(0,.1,_ShapeParams.x);
                return max(0,baseShape * envelope * height * layerMask - threshold - erosion) * _Density * .048 * fade;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float3 origin = GetCameraPositionWS();
                float3 direction = normalize(input.positionWS-origin);
                float3 ro = TransformWorldToObject(origin);
                float3 rd = mul((float3x3)unity_WorldToObject,direction);
                float3 invDir = rcp(rd + float3(1e-8,1e-8,1e-8));
                // Match ray bounds to the current profile so thin clouds retain sampling quality.
                float3 bounds = float3(.5*max(.2,_ShapeParams.y),.5*max(.06,_ShapeParams.z),.5*max(.2,_ShapeParams.y));
                float3 ta = (-bounds-ro)*invDir, tb = (bounds-ro)*invDir;
                float3 nearT = min(ta,tb), farT = max(ta,tb);
                float entry = max(0,max(nearT.x,max(nearT.y,nearT.z)));
                float exit = min(farT.x,min(farT.y,farT.z));
                float2 uv = GetNormalizedScreenSpaceUV(input.positionCS);
                float rawDepth = SampleSceneDepth(uv);
                float eyeDepth = LinearEyeDepth(rawDepth,_ZBufferParams);
                float depth = eyeDepth / max(.001,-TransformWorldToViewDir(direction).z);
                exit = min(exit,depth);
                if (exit <= entry) return 0;
                float sampleJitter=frac(52.9829189*frac(dot(input.positionCS.xy,float2(.06711056,.00583715))));
                int steps = (int)clamp(floor(_Steps+sampleJitter),8,96);
                float stride = (exit-entry)/steps;
                float jitter = frac(52.9829189 * frac(dot(input.positionCS.xy,float2(.06711056,.00583715))));
                // Distant low-step volumes use a near-midpoint sample to avoid visible stippling.
                float travel = entry + stride*lerp(jitter,.5,_CloudLod.x*.85);
                Light sun = GetMainLight();
                float3 sunDir = normalize(sun.direction);
                float silver = pow(saturate(dot(direction,sunDir)),12)*.65;
                float transmission = 1;
                float3 radiance = 0;
                [loop] for(int i=0;i<steps;i++)
                {
                    float3 p = origin + direction*travel;
                    float d = DensityAt(p);
                    if(d > .00001)
                    {
                        float optical = DensityAt(p+sunDir*22)*22;
                        float nearDensity=optical/22;
                        float midDensity=nearDensity,farDensity=nearDensity;
                        if(_CloudLod.x<.999) farDensity=lerp(DensityAt(p+sunDir*140),nearDensity,_CloudLod.x);
                        if(_CloudLod.y<.999) midDensity=lerp(DensityAt(p+sunDir*65),nearDensity,_CloudLod.y);
                        optical+=midDensity*43+farDensity*75;
                        float lighting = exp(-optical*1.35);
                        float3 color = lerp(_ShadowTint.rgb,_SunTint.rgb,lighting) + _SunTint.rgb*silver*lighting;
                        // Atmospheric perspective leaves distant silhouettes soft and legible.
                        color = lerp(color,unity_FogColor.rgb,1-exp(-travel*.00011));
                        float opacity = 1-exp(-d*stride);
                        radiance += transmission*opacity*color;
                        transmission *= 1-opacity;
                        if(transmission < .015) break;
                    }
                    travel += stride;
                }
                return half4(radiance,1-transmission);
            }
            ENDHLSL
        }
    }
}
