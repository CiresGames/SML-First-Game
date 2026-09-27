using UnityEngine;

namespace MantaFlight
{
    // Transient, generated per-volume values. Authored settings live in the distribution recipe.
    [DefaultExecutionOrder(-101)]
    public sealed class MantaCloudLayerVolume : MonoBehaviour
    {
        public float density=1;
        public float windSpeed=1;
        public bool rainCloud;
        public Vector4[] lobeCenters=new Vector4[5];
        public Vector4[] lobeRadii=new Vector4[5];
        public float edgeFade=1;
        MantaCloudscape clouds;
        MantaCloudDistribution distribution;
        public void Initialize(int seed,MantaCloudDistribution owner)
        {
            distribution=owner; clouds=owner.GetComponent<MantaCloudscape>();
            var random=new System.Random(seed);
            lobeCenters[0]=new Vector4(0,-.1f,0,0);
            lobeRadii[0]=new Vector4(.34f,.25f,.32f,0);
            for(int i=1;i<5;i++)
            {
                float angle=(float)random.NextDouble()*Mathf.PI*2;
                float reach=.1f+(float)random.NextDouble()*.1f;
                lobeCenters[i]=new Vector4(Mathf.Cos(angle)*reach,-.06f+(float)random.NextDouble()*.19f,Mathf.Sin(angle)*reach,0);
                lobeRadii[i]=new Vector4(.19f+(float)random.NextDouble()*.09f,.18f+(float)random.NextDouble()*.13f,.18f+(float)random.NextDouble()*.1f,0);
            }
        }
        void Update() { if(Application.isPlaying) AdvanceMotion(Time.deltaTime); }
        public void AdvanceMotion(float seconds)
        {
            if(!clouds || !distribution || seconds<=0) return;
            var velocity=distribution.transform.InverseTransformVector(new Vector3(clouds.wind.x,0,clouds.wind.z))*windSpeed;
            var p=transform.localPosition+velocity*seconds;
            var center=distribution.regionCenter;
            float x=distribution.regionSize.x*.5f+transform.localScale.x;
            float z=distribution.regionSize.y*.5f+transform.localScale.z;
            p.x=center.x+Mathf.Repeat(p.x-center.x+x,2*x)-x;
            p.z=center.z+Mathf.Repeat(p.z-center.z+z,2*z)-z;
            transform.localPosition=p;
            edgeFade=Mathf.SmoothStep(0,1,Mathf.Clamp01(Mathf.Min(x-Mathf.Abs(p.x-center.x),z-Mathf.Abs(p.z-center.z))/500));
        }
    }
}
