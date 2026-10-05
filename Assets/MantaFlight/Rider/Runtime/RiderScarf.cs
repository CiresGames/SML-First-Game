using UnityEngine;

namespace MantaFlight.Rider
{
    /// <summary>A pinned cloth ribbon, simulated in world space using fixed-step Verlet constraints.</summary>
    [DefaultExecutionOrder(220), DisallowMultipleComponent]
    public sealed class RiderScarf : MonoBehaviour
    {
        public RiderVisuals visuals;
        public MeshFilter fabric, collar;
        [Header("Fabric")]
        [Min(.2f)] public float length = 1.15f;
        [Min(.04f)] public float width = .18f;
        [Range(.002f,.03f)] public float thickness = .012f;
        [Range(0,.05f)] public float bodyClearance = .015f;
        [Range(0,2)] public float windResponse = 1;
        [Range(0,1)] public float flutter = .65f;
        [Min(.1f)] public float teleportDistance = 4;
        public Vector3 RelativeWind { get; private set; }
        public Vector3 Tip => positions == null ? transform.position : (positions[Index(Rows,0)]+positions[Index(Rows,Columns)])*.5f;
        public Vector3 Anchor { get; private set; }
        // A small ribbon cage bends as broad sections rather than a wrinkling sheet.
        public const int Rows=8, Columns=1;
        public const int RenderRows=32;
        const float Step=1f/90;
        const int Stride=Columns+1;
        struct Link { public int a,b; public float distance,stiffness; }
        Link[] links;
        Vector3[] positions, previous, smoothPositions, surface, vertices, collarVertices, collarRest;
        Vector3 renderAnchor;
        Mesh fabricMesh, collarMesh, originalFabric, originalCollar;
        Transform neck, head, chest, hips, leftShoulder, rightShoulder;
        Vector3 up, right, forward, lastRoot;
        Vector3 filteredRelativeWind;
        Vector3 filteredWorldWind, filteredMotion, filteredAcceleration, lastVelocity, lastAnchorOffset;
        bool windInitialized;
        float remainder, phase;
        bool reset=true;

        static int Index(int row,int column)=>row*Stride+column;
        void Start()=>Initialize();
        void OnEnable()=>reset=true;
        public void Initialize()
        {
            if(positions!=null)return;
            if(visuals==null || fabric==null || collar==null || !visuals.animator.isHuman) {enabled=false;return;}
            var a=visuals.animator;a.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            neck=a.GetBoneTransform(HumanBodyBones.Neck);head=a.GetBoneTransform(HumanBodyBones.Head);
            chest=a.GetBoneTransform(HumanBodyBones.UpperChest) ?? a.GetBoneTransform(HumanBodyBones.Chest);
            hips=a.GetBoneTransform(HumanBodyBones.Hips);
            leftShoulder=a.GetBoneTransform(HumanBodyBones.LeftUpperArm);rightShoulder=a.GetBoneTransform(HumanBodyBones.RightUpperArm);
            if(neck==null || head==null || chest==null || hips==null || leftShoulder==null || rightShoulder==null) {enabled=false;return;}
            originalFabric=fabric.sharedMesh;originalCollar=collar.sharedMesh;
            fabricMesh=CreateFabricMesh(length,width,thickness);fabricMesh.name="Rider scarf simulated cloth";fabricMesh.MarkDynamic();fabric.sharedMesh=fabricMesh;
            collarMesh=Instantiate(originalCollar);collarMesh.name="Rider scarf neck wrap";collarMesh.MarkDynamic();collar.sharedMesh=collarMesh;
            positions=new Vector3[(Rows+1)*Stride];previous=new Vector3[positions.Length];
            smoothPositions=new Vector3[positions.Length];surface=new Vector3[(RenderRows+1)*Stride];vertices=new Vector3[surface.Length*2];
            collarRest=originalCollar.vertices;collarVertices=new Vector3[collarRest.Length];
            var constraints=new System.Collections.Generic.List<Link>();
            void Add(int row,int col,int otherRow,int otherCol)
            {
                constraints.Add(new Link {a=Index(row,col),b=Index(otherRow,otherCol),distance=new Vector2((row-otherRow)*length/Rows,(col-otherCol)*width/Columns).magnitude,stiffness=Mathf.Abs(row-otherRow)==2?.45f:1});
            }
            for(int row=0;row<=Rows;row++)for(int col=0;col<=Columns;col++)
            {
                if(row<Rows)Add(row,col,row+1,col);
                if(col<Columns)Add(row,col,row,col+1);
                if(row<Rows && col<Columns){Add(row,col,row+1,col+1);Add(row,col+1,row+1,col);}
                if(row<Rows-1)Add(row,col,row+2,col);
            }
            links=constraints.ToArray();UpdateFrame();ResetCloth();Upload();
        }

        void LateUpdate()
        {
            if(positions==null || visuals.rider.Paused)return;
            Simulate(Time.deltaTime,WindManager.GetWindAt(neck.position),visuals.rider.Motor.Velocity);
        }

        void UpdateFrame()
        {
            up=(head.position-neck.position).normalized;
            if(up.sqrMagnitude<.5f)up=visuals.model.up;
            right=Vector3.ProjectOnPlane(rightShoulder.position-leftShoulder.position,up).normalized;
            if(right.sqrMagnitude<.5f)right=visuals.model.right;
            forward=Vector3.Cross(right,up).normalized;
            Anchor=neck.position+up*.015f-forward*.13f+right*.04f;
        }

        public void ResetCloth()
        {
            if(positions==null)return;
            UpdateFrame();
            for(int row=0;row<=Rows;row++)for(int col=0;col<=Columns;col++)
            {
                int i=Index(row,col);
                positions[i]=Anchor+right*((float)col/Columns-.5f)*width-forward*(length*row/Rows);
                previous[i]=positions[i];
                smoothPositions[i]=positions[i];
            }
            renderAnchor=Anchor;
            lastRoot=visuals.rider.transform.position;remainder=0;phase=0;reset=false;
            lastAnchorOffset=Anchor-lastRoot;filteredAcceleration=Vector3.zero;
            windInitialized=false;
        }

        public void Simulate(float deltaTime,Vector3 worldWind,Vector3 riderVelocity)
        {
            if(positions==null || deltaTime<=0)return;
            UpdateFrame();
            var root=visuals.rider.transform.position;
            if(reset || (root-lastRoot).sqrMagnitude>teleportDistance*teleportDistance)ResetCloth();
            var rootTranslation=root-lastRoot;
            var anchorOffset=Anchor-root;
            // Neck movement includes banking, loops, rolls and the glide pose. Cap animation
            // transitions so they cannot kick the ribbon violently on mounting/deployment.
            var neckVelocity=Vector3.ClampMagnitude((anchorOffset-lastAnchorOffset)/deltaTime,12);
            lastAnchorOffset=anchorOffset;
            for(int i=0;i<positions.Length;i++)
            {
                positions[i]+=rootTranslation;previous[i]+=rootTranslation;
            }
            lastRoot=root;
            // Carry the cage with the root before stepping. Its remaining velocity is relative
            // to the Rider, so apparent wind drives drag without frame-rate-dependent pin impulses.
            if(!windInitialized)
            {
                filteredWorldWind=worldWind;filteredMotion=riderVelocity;lastVelocity=riderVelocity;
                neckVelocity=Vector3.zero;windInitialized=true;
            }
            var acceleration=Vector3.ClampMagnitude((riderVelocity-lastVelocity)/deltaTime,35);
            lastVelocity=riderVelocity;
            RelativeWind=Vector3.ClampMagnitude(worldWind-riderVelocity-neckVelocity,80);
            remainder+=Mathf.Min(deltaTime,.1f);
            while(remainder>=Step)
            {
                // Smooth gusts in simulation time, independent of the render frame rate.
                float blend=1-Mathf.Exp(-Step/.65f);
                filteredWorldWind=Vector3.Lerp(filteredWorldWind,worldWind,blend);
                // Keep gusts slow, but respond promptly to flight input and stunt motion.
                float motionBlend=1-Mathf.Exp(-Step/.14f);
                filteredMotion=Vector3.Lerp(filteredMotion,riderVelocity+neckVelocity,motionBlend);
                filteredAcceleration=Vector3.Lerp(filteredAcceleration,acceleration,motionBlend);
                filteredRelativeWind=Vector3.ClampMagnitude(filteredWorldWind-filteredMotion,80);
                Integrate(filteredRelativeWind);remainder-=Step;
            }
            Pin();
            // Follow translation immediately, but filter changes in shape in the neck's moving frame.
            // This removes fixed-step chatter without letting the scarf trail metres behind at speed.
            var translation=Anchor-renderAnchor;
            float shapeBlend=1-Mathf.Exp(-deltaTime/.18f);
            for(int i=0;i<positions.Length;i++)
                smoothPositions[i]=i<Stride?positions[i]:Vector3.Lerp(smoothPositions[i]+translation,positions[i],shapeBlend);
            renderAnchor=Anchor;
            Upload();
        }

        void Pin()
        {
            for(int col=0;col<=Columns;col++)
            {
                positions[col]=Anchor+right*((float)col/Columns-.5f)*width;
                previous[col]=positions[col];
            }
        }
        void Integrate(Vector3 wind)
        {
            float intensity=Mathf.Clamp01(filteredRelativeWind.magnitude/25);
            phase+=Step*Mathf.Lerp(.7f,1.4f,intensity);
            var side=Vector3.Cross(filteredRelativeWind.sqrMagnitude>.01f?filteredRelativeWind.normalized:-forward,up).normalized;
            if(side.sqrMagnitude<.1f)side=right;
            for(int row=1;row<=Rows;row++)for(int col=0;col<=Columns;col++)
            {
                int i=Index(row,col);Vector3 velocity=(positions[i]-previous[i])/Step;
                Vector3 drag=Vector3.ClampMagnitude((wind-velocity)*5*windResponse,90);
                // One broad, slow wave across the width avoids competing twists and rapid flapping.
                float wavePhase=phase-2f*row/Rows;
                float wave=Mathf.Sin(wavePhase);
                Vector3 force=Physics.gravity+drag-filteredAcceleration*.65f
                    +(side*wave+up*(Mathf.Sin(wavePhase+.6f)*.15f))*(intensity*flutter*1.5f);
                // Dampen oscillation relative to the Rider, preserving translation in fast flight.
                Vector3 dampedVelocity=velocity*Mathf.Exp(-10*Step);
                var p=positions[i];positions[i]+=dampedVelocity*Step+force*(Step*Step);previous[i]=p;
            }
            for(int iteration=0;iteration<8;iteration++)
            {
                Pin();
                foreach(var link in links)
                {
                    Vector3 difference=positions[link.b]-positions[link.a];float distance=difference.magnitude;
                    if(distance<.000001f)continue;
                    var correction=difference*((distance-link.distance)/distance*link.stiffness);
                    bool pinnedA=link.a<Stride,pinnedB=link.b<Stride;
                    if(pinnedA && pinnedB)continue;
                    if(!pinnedA)positions[link.a]+=correction*(pinnedB?1:.5f);
                    if(!pinnedB)positions[link.b]-=correction*(pinnedA?1:.5f);
                }
                for(int i=Stride;i<positions.Length;i++)
                {
                    // Long-range tethers limit accumulated stretch without stiffening local folds.
                    var seam=Anchor+right*((float)(i%Stride)/Columns-.5f)*width;
                    positions[i]=seam+Vector3.ClampMagnitude(positions[i]-seam,length*(i/Stride)/Rows*1.025f);
                    // Solve body clearance last: the stretch tether must not push fabric back inside.
                    float padding=thickness*.5f+bodyClearance;
                    Collide(i,hips.position+up*.06f,chest.position,.19f+padding);
                    Collide(i,head.position+up*.075f,head.position+up*.10f,.125f+padding);
                }
            }
            Pin();
        }
        void Collide(int i,Vector3 a,Vector3 b,float radius)
        {
            var axis=b-a;float t=Mathf.Clamp01(Vector3.Dot(positions[i]-a,axis)/Mathf.Max(.00001f,axis.sqrMagnitude));
            var center=a+axis*t;var offset=positions[i]-center;
            if(offset.sqrMagnitude<radius*radius)
            {
                Vector3 correction=(offset.sqrMagnitude>.000001f?offset.normalized:-forward)*radius-offset;
                positions[i]+=correction;previous[i]+=correction*.8f;
            }
        }
        void Upload()
        {
            // Smooth the visible ribbon independently of the small simulation cage.
            for(int row=0;row<=RenderRows;row++)for(int col=0;col<=Columns;col++)
            {
                float sample=(float)row*Rows/RenderRows;
                int section=Mathf.Min(Rows-1,Mathf.FloorToInt(sample));float t=sample-section;
                var b=smoothPositions[Index(section,col)];var c=smoothPositions[Index(section+1,col)];
                var a=section>0?smoothPositions[Index(section-1,col)]:2*b-c;
                var d=section+2<=Rows?smoothPositions[Index(section+2,col)]:2*c-b;
                var p=.5f*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*(t*t)+(-a+3*b-3*c+d)*(t*t*t));
                if(row>0)
                {
                    float padding=thickness*.5f+bodyClearance;
                    p=ClearBody(p,hips.position+up*.06f,chest.position,.19f+padding);
                    p=ClearBody(p,head.position+up*.075f,head.position+up*.10f,.125f+padding);
                }
                surface[Index(row,col)]=p;
            }
            for(int row=0;row<=RenderRows;row++)for(int col=0;col<=Columns;col++)
            {
                int i=Index(row,col);
                var along=surface[Index(Mathf.Min(RenderRows,row+1),col)]-surface[Index(Mathf.Max(0,row-1),col)];
                var across=surface[Index(row,Mathf.Min(Columns,col+1))]-surface[Index(row,Mathf.Max(0,col-1))];
                var normal=Vector3.Cross(along,across).normalized;
                if(normal.sqrMagnitude<.5f)normal=up;
                var offset=normal*(thickness*.5f);
                vertices[i]=fabric.transform.InverseTransformPoint(surface[i]+offset);
                vertices[i+surface.Length]=fabric.transform.InverseTransformPoint(surface[i]-offset);
            }
            fabricMesh.vertices=vertices;fabricMesh.RecalculateNormals();fabricMesh.RecalculateBounds();
            for(int i=0;i<collarRest.Length;i++)
            {
                var p=collarRest[i];
                collarVertices[i]=collar.transform.InverseTransformPoint(neck.position+right*p.x+up*p.y+forward*p.z);
            }
            collarMesh.vertices=collarVertices;collarMesh.RecalculateNormals();collarMesh.RecalculateBounds();
        }
        Vector3 ClearBody(Vector3 point,Vector3 a,Vector3 b,float radius)
        {
            var axis=b-a;float t=Mathf.Clamp01(Vector3.Dot(point-a,axis)/Mathf.Max(.00001f,axis.sqrMagnitude));
            var center=a+axis*t;var offset=point-center;
            return offset.sqrMagnitude<radius*radius?center+(offset.sqrMagnitude>.000001f?offset.normalized:-forward)*radius:point;
        }
        public static Mesh CreateFabricMesh(float length,float width,float thickness)
        {
            int count=(RenderRows+1)*Stride;
            var v=new Vector3[count*2];var uv=new Vector2[v.Length];
            var face=new System.Collections.Generic.List<int>();var hem=new System.Collections.Generic.List<int>();
            for(int row=0;row<=RenderRows;row++)for(int col=0;col<=Columns;col++)
            {
                int i=Index(row,col);float u=(float)col/Columns,t=(float)row/RenderRows;
                v[i]=new Vector3((u-.5f)*width,-thickness*.5f,-t*length);
                v[i+count]=v[i]+Vector3.up*thickness;uv[i]=uv[i+count]=new Vector2(u,t);
                if(row==RenderRows || col==Columns)continue;
                var triangles=row>=RenderRows-RenderRows/Rows?hem:face;
                int a=i,b=i+Stride,c=i+1,d=i+Stride+1;
                triangles.AddRange(new[]{a,b,c,c,b,d,a+count,c+count,b+count,c+count,d+count,b+count});
            }
            var edge=new System.Collections.Generic.List<int>();
            for(int row=0;row<=RenderRows;row++)edge.Add(Index(row,0));
            for(int col=1;col<=Columns;col++)edge.Add(Index(RenderRows,col));
            for(int row=RenderRows-1;row>=0;row--)edge.Add(Index(row,Columns));
            for(int col=Columns-1;col>0;col--)edge.Add(Index(0,col));
            for(int i=0;i<edge.Count;i++)
            {
                int a=edge[i],b=edge[(i+1)%edge.Count];
                hem.AddRange(new[]{a,b+count,b,a,a+count,b+count});
            }
            var mesh=new Mesh{name="Scarf thick fabric"};mesh.vertices=v;mesh.uv=uv;mesh.subMeshCount=2;
            mesh.SetTriangles(face,0);mesh.SetTriangles(hem,1);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        void OnDestroy()
        {
            if(fabric!=null && originalFabric!=null)fabric.sharedMesh=originalFabric;
            if(collar!=null && originalCollar!=null)collar.sharedMesh=originalCollar;
            if(fabricMesh!=null){if(Application.isPlaying)Destroy(fabricMesh);else DestroyImmediate(fabricMesh);}
            if(collarMesh!=null){if(Application.isPlaying)Destroy(collarMesh);else DestroyImmediate(collarMesh);}
        }
    }
}
