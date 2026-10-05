using UnityEngine;

namespace MantaFlight.Rider
{
    // Runs after RiderVisuals and the Animator: membrane edges follow the final skeletal pose.
    [DefaultExecutionOrder(200)]
    public sealed class RiderWingsuit : MonoBehaviour
    {
        public RiderVisuals visuals;
        public MeshFilter leftWing, rightWing, legWing;
        public float Deployment { get; private set; }
        sealed class Panel
        {
            public MeshFilter filter;
            public Mesh mesh;
            public Vector2[] uv;
            public Vector3[] vertices;
            public Renderer renderer;
        }
        Panel[] panels;
        Transform leftShoulder, rightShoulder, leftHand, rightHand, leftHip, rightHip, leftFoot, rightFoot, hips, chest;
        Quaternion chestOffset = Quaternion.identity;
        float phase;

        void Start()
        {
            var a = visuals.animator;
            // CPU fabric anchors must keep receiving the same pose as the skinned body offscreen.
            a.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            leftShoulder=a.GetBoneTransform(HumanBodyBones.LeftUpperArm); rightShoulder=a.GetBoneTransform(HumanBodyBones.RightUpperArm);
            leftHand=a.GetBoneTransform(HumanBodyBones.LeftHand); rightHand=a.GetBoneTransform(HumanBodyBones.RightHand);
            leftHip=a.GetBoneTransform(HumanBodyBones.LeftUpperLeg); rightHip=a.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            leftFoot=a.GetBoneTransform(HumanBodyBones.LeftFoot); rightFoot=a.GetBoneTransform(HumanBodyBones.RightFoot);
            hips=a.GetBoneTransform(HumanBodyBones.Hips); chest=a.GetBoneTransform(HumanBodyBones.Chest);
            panels=new Panel[3];
            var filters=new[]{leftWing,rightWing,legWing};
            for(int i=0;i<3;i++)
            {
                var mesh=Instantiate(filters[i].sharedMesh); mesh.name="Wingsuit animated panel"; mesh.MarkDynamic();
                filters[i].sharedMesh=mesh;
                panels[i]=new Panel {filter=filters[i],mesh=mesh,uv=mesh.uv,vertices=mesh.vertices,renderer=filters[i].GetComponent<Renderer>()};
            }
        }

        void Update()
        {
            // Remove our previous additive rotation before Animator evaluation, also when culled.
            if(chest!=null) chest.localRotation *= Quaternion.Inverse(chestOffset);
            chestOffset=Quaternion.identity;
        }

        void LateUpdate()
        {
            if(panels==null || visuals.rider.Paused) return;
            var rider=visuals.rider;
            float dt=Time.deltaTime;
            // Opening the fabric on take-off is cosmetic; lift still follows the player's glide command.
            float target=rider.Airborne?1:0;
            Deployment=Mathf.MoveTowards(Deployment,target,dt/Mathf.Max(.01f,target>0?rider.settings.glide.deployDuration:rider.settings.glide.retractDuration));
            float open=Mathf.SmoothStep(0,1,Deployment);
            float speed=Mathf.InverseLerp(5,rider.settings.glide.maximumSpeed,rider.Motor.Velocity.magnitude);
            phase+=dt*Mathf.Lerp(5,16,speed);
            float glide=rider.GlidePose;
            if(chest!=null)
            {
                float turn=rider.Glide.Bank/Mathf.Max(1,rider.settings.glide.bankLimit);
                chestOffset=Quaternion.Euler((-3*speed+Mathf.Sin(phase*.43f)*1.2f)*glide, turn*4*glide, Mathf.Sin(phase*.57f)*.8f*speed*glide);
                chest.localRotation*=chestOffset;
            }
            Deform(panels[0],leftShoulder.position,leftHand.position,leftHip.position,open,speed,0);
            Deform(panels[1],rightShoulder.position,rightHand.position,rightHip.position,open,speed,1.7f);
            Deform(panels[2],hips.position,leftFoot.position,rightFoot.position,open,speed,3.1f);
        }

        void Deform(Panel p,Vector3 a,Vector3 b,Vector3 c,float open,float speed,float offset)
        {
            p.renderer.enabled=open>.005f;
            // Collapsed fabric gathers against the body; deployment progressively spans the limbs.
            b=Vector3.Lerp(a,b,Mathf.Lerp(.035f,1,open));
            var normal=Vector3.Cross(b-a,c-a).normalized;
            if(Vector3.Dot(normal,visuals.model.forward)<0) normal=-normal;
            for(int i=0;i<p.vertices.Length;i++)
            {
                float u=p.uv[i].x,v=p.uv[i].y,w=Mathf.Max(0,1-u-v);
                // Barycentric envelope leaves all three sewn edges pinned to their anchors.
                float envelope=27*u*v*w;
                float inflation=(.055f+speed*.065f)*open;
                float ripple=Mathf.Sin(phase+u*17+v*11+offset)*.012f*speed*open;
                var world=a*w+b*u+c*v+normal*envelope*(inflation+ripple);
                p.vertices[i]=p.filter.transform.InverseTransformPoint(world);
            }
            p.mesh.vertices=p.vertices;p.mesh.RecalculateNormals();p.mesh.RecalculateBounds();
        }

        void OnDestroy()
        {
            if(chest!=null) chest.localRotation*=Quaternion.Inverse(chestOffset);
            if(panels!=null) foreach(var p in panels) if(p.mesh!=null) Destroy(p.mesh);
        }
    }
}
