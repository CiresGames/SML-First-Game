using System;
using UnityEngine;

namespace MantaFlight
{
    /// <summary>Camera-only angular offsets; input is in degrees, with mouse delta already scaled.</summary>
    [Serializable]
    public sealed class FlightFreeLook
    {
        [Range(45,180)] public float horizontalLimit=160;
        [Range(10,80)] public float verticalLimit=65;
        [Min(0)] public float recenterDelay=1.8f;
        [Min(0)] public float recenterSpeed=1.7f;
        public Vector2 Offset { get; private set; }
        float idle;
        public void Reset() {Offset=Vector2.zero;idle=0;}
        public void Tick(Vector2 delta,float dt,bool fullOrbit=false)
        {
            if(dt<=0)return;
            if(delta.sqrMagnitude>.000001f)
            {
                idle=0;
                float yaw=fullOrbit?Mathf.DeltaAngle(0,Offset.x+delta.x):Mathf.Clamp(Offset.x+delta.x,-horizontalLimit,horizontalLimit);
                Offset=new Vector2(yaw,Mathf.Clamp(Offset.y+delta.y,-verticalLimit,verticalLimit));
            }
            else
            {
                float before=idle;idle+=dt;
                float recenterTime=Mathf.Max(0,idle-Mathf.Max(before,recenterDelay));
                Offset*=Mathf.Exp(-recenterSpeed*recenterTime);
            }
        }
        public Quaternion Rotation=>Quaternion.Euler(-Offset.y,Offset.x,0);
    }
}
