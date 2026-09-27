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
            struct Varyings { float4 position:SV_POSITION; float3 direction:TEXCOORD0; };
            Varyings Vert(float4 vertex:POSITION)
            { Varyings o; o.position=UnityObjectToClipPos(vertex); o.direction=vertex.xyz; return o; }
            float Hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
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
                float2 uv=float2(atan2(d.x,d.z)/6.283185+.5,acos(clamp(d.y,-1,1))/3.141593)*float2(1500,750);
                float2 cell=floor(uv), p=frac(uv)-.5;
                float star=step(.997,Hash(cell))*exp(-dot(p,p)*34);
                float twinkle=.8+.2*sin(_Time.y*1.1+Hash(cell+3)*30);
                color+=star*twinkle*_Stars*pow(1-_Daylight,3)*smoothstep(0,.15,d.y)*float3(.8,.9,1);
                return half4(color,1);
            }
            ENDCG
        }
    }
}
