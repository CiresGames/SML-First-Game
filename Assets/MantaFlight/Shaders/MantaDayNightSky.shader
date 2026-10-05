Shader "Manta/Day Night Sky"
{
    Properties
    {
        _Zenith("Zenith",Color)=(.08,.28,.55,1)
        _Horizon("Horizon",Color)=(.6,.76,.86,1)
        _Ground("Ground",Color)=(.1,.15,.2,1)
        _SunDirection("Sun direction",Vector)=(0,1,0,0)
        _MoonDirection("Moon direction",Vector)=(0,-1,0,0)
        [HDR]_SunColor("Sun",Color)=(4,3,2,1)
        [HDR]_MoonColor("Moon",Color)=(.6,.8,1.5,1)
        _Daylight("Daylight",Range(0,1))=1
        _Stars("Star brightness",Float)=1.2
        _StarAtlas("Stars and Milky Way",2D)="black" {}
        _GalaxyBrightness("Milky Way brightness",Float)=1.4
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"
            float4 _Zenith,_Horizon,_Ground,_SunDirection,_MoonDirection,_SunColor,_MoonColor;
            float _Daylight,_Stars;
            float _Overcast;
            sampler2D _StarAtlas;
            float _GalaxyBrightness,_Twinkle;
            float4x4 _StarRotation;
            float4 _Aurora;
            float4 _AuroraShapes[4];
            float _AuroraBandCount;
            float4 _AuroraAnchor;
            float4 _AuroraWaves;
            struct Varyings { float4 position:SV_POSITION; float3 direction:TEXCOORD0; };
            Varyings Vert(float4 vertex:POSITION)
            { Varyings o; o.position=UnityObjectToClipPos(vertex); o.direction=vertex.xyz; return o; }
            float Hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float RibbonNoise(float x)
            {
                float p=floor(x),f=frac(x); f=f*f*(3-2*f);
                return lerp(Hash(float2(p,17)),Hash(float2(p+1,17)),f);
            }
            float3 Aurora(float3 d)
            {
                if(_Aurora.x<.001 || d.y<=0) return 0;
                float time=_Aurora.y*.025,seed=_Aurora.w;
                float3 light=0;
                float scale=max(.5,_AuroraAnchor.w);
                float3 eye=(_WorldSpaceCameraPos-_AuroraAnchor.xyz)/scale;
                [unroll] for(int curtain=0;curtain<4;curtain++)
                {
                    if(curtain>=_AuroraBandCount) break;
                    float phase=seed+curtain*2.7;
                    float4 shape=_AuroraShapes[curtain];
                    float heading=_Aurora.z+(curtain-(_AuroraBandCount-1)*.5)*.23;
                    float3 forward=float3(sin(heading),0,cos(heading));
                    float3 right=float3(forward.z,0,-forward.x);
                    float oz=dot(eye,forward),ox=dot(eye,right);
                    float dz=dot(d,forward),dx=dot(d,right);
                    if(dz<=.08) continue;
                    float distance=12500+curtain*8500;
                    float width=14000*max(.1,shape.x);
                    float drift=shape.z*11000;
                    float travel=(distance-oz)/dz;
                    // Reject rays outside the sheet's possible bounds before solving folds.
                    float reach=2950/dz;
                    float centerX=ox+dx*travel-drift;
                    if(abs(centerX)>width*1.75+abs(dx)*reach) continue;
                    float centerY=eye.y+d.y*travel;
                    float waveReach=900*_AuroraWaves.x;
                    float baseMin=4750+curtain*650+shape.w*6000-waveReach;
                    float baseMax=6050+curtain*650+shape.w*6000+waveReach;
                    if(centerY+abs(d.y)*reach<baseMin-180*shape.y ||
                       centerY-abs(d.y)*reach>baseMax+6900*shape.y) continue;
                    // Intersect the active curved, world-anchored emitting sheets.
                    // Three bounded iterations replace a volumetric ray march.
                    [unroll] for(int solve=0;solve<3;solve++)
                    {
                        float u=(ox+dx*travel-drift)/width;
                        float fold=sin(u*3.2+time+phase)*2300+sin(u*6.1-time*.6+phase)*650;
                        travel=(distance+fold-oz)/dz;
                    }
                    if(travel<=0 || travel>120000) continue;
                    float localAngle=(ox+dx*travel-drift)/width;
                    if(abs(localAngle)>1.75) continue;
                    float altitude=eye.y+d.y*travel;
                    float baseHeight=5400+curtain*650+shape.w*6000+sin(localAngle*2.1+phase+time*.4)*650;
                    // Traveling waves roll along the lower edge, with a smaller counter-wave.
                    float waveTime=_AuroraWaves.y*(1+curtain*.13);
                    float roll=sin(localAngle*8-waveTime+phase);
                    float ripple=sin(localAngle*17+waveTime*.73+phase*1.7);
                    baseHeight+=_AuroraWaves.x*(650*roll+250*ripple);
                    float h=(altitude-baseHeight)/(15000*max(.1,shape.y));
                    if(h<-.012 || h>.46) continue;
                    float height=.3+.16*RibbonNoise(localAngle*2.4+phase+time*.18);
                    // Increasing displacement with height gives the rays a soft trailing bend.
                    float bend=_AuroraWaves.x*smoothstep(0,.32,h)*
                        (.045*sin(localAngle*8-waveTime+phase-h*7)+.018*ripple);
                    float filaments=RibbonNoise((localAngle+bend)*105+sin(localAngle*5+time)*3+phase-time*.25);
                    filaments=.15+.85*pow(filaments,2);
                    float vertical=smoothstep(-.012,.025,h)*exp(-max(0,h)*8)*(1-smoothstep(height*.5,height,h));
                    float arc=1-smoothstep(.65,1.6,abs(localAngle+.14*sin(time+phase)));
                    float pulse=.75+.25*sin(time*.8+localAngle*2+phase);
                    float3 tint=lerp(float3(.08,1,.38),float3(.43,.15,.8),smoothstep(.06,.25,h));
                    tint=lerp(float3(.25,.55,.75),tint,smoothstep(-.01,.035,h));
                    float slope=(cos(localAngle*3.2+time+phase)*7360+cos(localAngle*6.1-time*.6+phase)*3965)/width;
                    float grazing=rsqrt(1+slope*slope)*abs(dz-slope*dx);
                    float foldLight=min(1.8,rsqrt(max(.2,grazing)));
                    float distant=exp(-travel*.000012);
                    tint=lerp(tint,float3(.19,.48,.55),saturate(travel/90000)*.35);
                    light+=tint*vertical*arc*filaments*pulse*foldLight*distant*(.65-curtain*.1);
                }
                return light*_Aurora.x*smoothstep(0,.08,d.y);
            }
            half4 Frag(Varyings i):SV_Target
            {
                float3 d=normalize(i.direction);
                float3 color=lerp(_Horizon.rgb,_Zenith.rgb,pow(saturate(d.y),.55));
                color=lerp(color,_Ground.rgb,1-smoothstep(-.3,-.02,d.y));
                float sun=saturate(dot(d,_SunDirection.xyz));
                color+=_SunColor.rgb*(smoothstep(.99965,.99986,sun)+pow(sun,180)*.16)*_Daylight;
                float moon=saturate(dot(d,_MoonDirection.xyz));
                float moonDisc=smoothstep(.99935,.9996,moon);
                color+=_MoonColor.rgb*(moonDisc*.8+pow(moon,220)*.035)*(1-_Daylight);
                float3 celestial=mul((float3x3)_StarRotation,d);
                float2 uv=float2(atan2(celestial.x,celestial.z)/6.283185+.5,asin(clamp(celestial.y,-1,1))/3.141593+.5);
                float4 atlas=tex2D(_StarAtlas,uv);
                float3 skyLight=atlas.rgb*atlas.rgb*4;
                float twinkle=1+_Twinkle*(.3+.7*(1-saturate(d.y)))*sin(_Time.y*1.7+dot(celestial,float3(173,257,319)))*atlas.a;
                float moonWash=(1-pow(moon,32)*.65)*(1-moonDisc);
                color+=skyLight*lerp(_GalaxyBrightness,_Stars,atlas.a)*twinkle*pow(1-_Daylight,4)*smoothstep(-.01,.2,d.y)*moonWash;
                color+=Aurora(d)*(1-moonDisc);
                // Cheap continuous ceiling behind the existing local volume formations.
                float ceiling=saturate(_Overcast)*smoothstep(-.12,.15,d.y);
                float mottling=RibbonNoise(d.x*9+d.z*5+_Time.y*.008)*.6+RibbonNoise(d.z*19-d.x*7-_Time.y*.005)*.4;
                float3 grey=lerp(float3(.018,.024,.035),float3(.21,.23,.26),_Daylight)*lerp(.72,1.18,mottling);
                color=lerp(color,grey,ceiling);
                return half4(color,1);
            }
            ENDCG
        }
    }
}
