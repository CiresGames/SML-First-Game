using UnityEngine;
namespace MantaFlight.Rider
{
    public sealed class RiderVisuals : MonoBehaviour
    {
        public RiderController rider;
        public Transform model;
        public Animator animator;
        public AudioClip landingClip;
        AudioSource wind;
        AudioClip windClip;
        Vector3 baseScale;
        RiderState previous;
        void Start()
        {
            baseScale=model.localScale;wind=gameObject.AddComponent<AudioSource>();wind.playOnAwake=false;wind.loop=true;wind.spatialBlend=0;
            float[] samples=new float[22050];uint seed=123456;
            float low=0;
            for(int i=0;i<samples.Length;i++){seed=1664525*seed+1013904223;float noise=((seed>>8)/16777215f*2-1);low=Mathf.Lerp(low,noise,.12f);samples[i]=low*Mathf.Sin(Mathf.PI*i/(samples.Length-1));}
            windClip=AudioClip.Create("Rider wind placeholder",samples.Length,1,22050,false);windClip.SetData(samples,0);wind.clip=windClip;wind.volume=0;wind.Play();
        }
        void LateUpdate()
        {
            if(rider.Paused) {wind.Pause();return;} if(!wind.isPlaying)wind.UnPause();
            float dt=Time.deltaTime;
            bool ground=rider.State==RiderState.Grounded || rider.State==RiderState.Landing || rider.State==RiderState.Rolling;
            float speed=Vector3.ProjectOnPlane(rider.Motor.Velocity,Vector3.up).magnitude;
            animator.SetFloat("Speed",ground?speed:0,.12f,dt);animator.SetFloat("MotionSpeed",1);
            animator.SetBool("Grounded",ground || rider.Mounted);animator.SetBool("Jump",rider.State==RiderState.Falling && rider.Motor.Velocity.y>0);animator.SetBool("FreeFall",rider.Airborne);
            Quaternion pose=Quaternion.Euler(0,0,0);
            if(rider.GlidePose>0) pose=Quaternion.Slerp(Quaternion.identity,Quaternion.Euler(90+rider.Glide.Pitch,0,-rider.Glide.Bank),rider.GlidePose);
            if(rider.Glide.Stalled && rider.GlidePose>.5f)pose*=Quaternion.Euler(Mathf.Sin(Time.time*8)*9,0,Mathf.Sin(Time.time*6)*10);
            if(rider.State==RiderState.Rolling)pose=Quaternion.Euler(360*rider.RollProgress,0,0);
            model.localRotation=pose;
            model.localPosition=Vector3.up*(rider.State==RiderState.Rolling?.45f:0);
            model.localScale=Vector3.Scale(baseScale,new Vector3(1,rider.Motor.Crouched?.65f:1,1));
            float feel=rider.Airborne?Mathf.InverseLerp(4,rider.settings.glide.maximumSpeed,rider.Motor.Velocity.magnitude):0;
            wind.volume=Mathf.Lerp(wind.volume,feel*rider.settings.glide.windVolume,dt*4);
            wind.pitch=Mathf.Lerp(rider.settings.glide.windMinPitch,rider.settings.glide.windMaxPitch,feel);
            if(previous!=rider.State && (rider.State==RiderState.Landing || rider.State==RiderState.Rolling) && landingClip!=null)wind.PlayOneShot(landingClip,.25f);
            previous=rider.State;
        }
        void OnDestroy(){if(windClip!=null)Destroy(windClip);}
    }
}
