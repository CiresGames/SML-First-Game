using UnityEngine;
namespace MantaFlight.Rider
{
    public sealed class RiderVisuals : MonoBehaviour
    {
        public RiderController rider;
        public Transform model;
        public Animator animator;
        public AudioClip landingClip;
        public AnimationClip landingAnimation;
        public AnimationClip rollingAnimation;
        AudioSource wind;
        AudioClip windClip;
        Vector3 baseScale;
        Vector3 basePosition;
        Quaternion baseRotation;
        string currentAnimation;
        RiderState previous;
        void Start()
        {
            baseScale=model.localScale; basePosition=model.localPosition; baseRotation=model.localRotation;
            animator.applyRootMotion=false;
            wind=gameObject.AddComponent<AudioSource>();wind.playOnAwake=false;wind.loop=true;wind.spatialBlend=0;
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
            animator.SetFloat("Speed",ground?speed:0,.12f,dt);
            UpdateAnimation(speed);
            Quaternion pose=Quaternion.Euler(0,0,0);
            if(rider.GlidePose>0) pose=Quaternion.Slerp(Quaternion.identity,Quaternion.Euler(rider.Glide.Pitch,0,-rider.Glide.Bank),rider.GlidePose);
            if(rider.Glide.Stalled && rider.GlidePose>.5f)pose*=Quaternion.Euler(Mathf.Sin(Time.time*8)*9,0,Mathf.Sin(Time.time*6)*10);
            model.localRotation=baseRotation*pose;
            model.localPosition=basePosition;
            model.localScale=baseScale;
            float feel=rider.Airborne?Mathf.InverseLerp(4,rider.settings.glide.maximumSpeed,rider.Motor.Velocity.magnitude):0;
            wind.volume=Mathf.Lerp(wind.volume,feel*rider.settings.glide.windVolume,dt*4);
            wind.pitch=Mathf.Lerp(rider.settings.glide.windMinPitch,rider.settings.glide.windMaxPitch,feel);
            if(previous!=rider.State && (rider.State==RiderState.Landing || rider.State==RiderState.Rolling) && landingClip!=null)wind.PlayOneShot(landingClip,.25f);
            previous=rider.State;
        }
        void UpdateAnimation(float speed)
        {
            string next;
            float playback=1;
            switch(rider.State)
            {
                case RiderState.Mounted:
                case RiderState.Remounting: next="Mounted"; break;
                case RiderState.Glide:
                case RiderState.Deploying: next="Glide"; break;
                case RiderState.Rolling:
                    next="Roll";
                    if(rollingAnimation!=null) playback=rollingAnimation.length/Mathf.Max(.1f,rider.settings.ground.rollDuration);
                    break;
                case RiderState.Landing:
                    next="Land";
                    if(landingAnimation!=null) playback=landingAnimation.length/Mathf.Max(.1f,rider.LandingDuration);
                    break;
                case RiderState.Grounded:
                    next=rider.Motor.Crouched?"Crouch":"Locomotion";
                    if(rider.Motor.Crouched) playback=Mathf.Clamp(speed/Mathf.Max(.1f,rider.settings.ground.crouchSpeed),0,1.5f);
                    break;
                default: next=rider.Motor.Velocity.y>0?"Jump":"Fall"; break;
            }
            animator.SetFloat("ActionSpeed",playback);
            if(next==currentAnimation)return;
            // The movement state owns timing; root motion must never move the capsule.
            animator.CrossFadeInFixedTime(next,next=="Roll" || next=="Land"?.05f:.15f,0,0);
            currentAnimation=next;
        }
        void OnDestroy(){if(windClip!=null)Destroy(windClip);}
    }
}
