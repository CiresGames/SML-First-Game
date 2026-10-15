using UnityEngine;
using UnityEngine.Rendering;

namespace MantaFlight
{
    /// <summary>Compact, camera-culled GPU instances of the shared Blender tree meshes.</summary>
    [ExecuteAlways]
    public sealed class MantaIslandGrove : MonoBehaviour
    {
        public Mesh trunkMesh,leafMesh;
        public Material trunkMaterial,leafMaterial;
        public Matrix4x4 trunkSource=Matrix4x4.identity,leafSource=Matrix4x4.identity;
        public Vector3[] positions=new Vector3[0],scales=new Vector3[0];
        public float[] yaws=new float[0];
        public Bounds localBounds;
        readonly Matrix4x4[] trunks=new Matrix4x4[1023],leaves=new Matrix4x4[1023];
        readonly Plane[] planes=new Plane[6];
        void OnEnable()=>RenderPipelineManager.beginCameraRendering+=Draw;
        void OnDisable()=>RenderPipelineManager.beginCameraRendering-=Draw;
        void Draw(ScriptableRenderContext context,Camera camera)
        {
            if(!trunkMesh || !leafMesh || !trunkMaterial || !leafMaterial || positions==null || positions.Length==0)return;
            if(camera.cameraType!=CameraType.Game && camera.cameraType!=CameraType.SceneView)return;
            if((camera.cullingMask & (1<<gameObject.layer))==0)return;
            GeometryUtility.CalculateFrustumPlanes(camera,planes);
            var m=transform.localToWorldMatrix;var bounds=new Bounds(m.MultiplyPoint3x4(localBounds.center),Vector3.Scale(localBounds.size,transform.lossyScale));
            if(!GeometryUtility.TestPlanesAABB(planes,bounds))return;
            int count=Mathf.Min(positions.Length,Mathf.Min(scales.Length,yaws.Length));
            for(int start=0;start<count;start+=1023){
                int n=Mathf.Min(1023,count-start);
                for(int j=0;j<n;j++){
                    int i=start+j;var tree=m*Matrix4x4.TRS(positions[i],Quaternion.Euler(0,yaws[i],0),scales[i]);
                    trunks[j]=tree*trunkSource;leaves[j]=tree*leafSource;
                }
                Graphics.DrawMeshInstanced(trunkMesh,0,trunkMaterial,trunks,n,null,ShadowCastingMode.On,true,gameObject.layer,camera);
                Graphics.DrawMeshInstanced(leafMesh,0,leafMaterial,leaves,n,null,ShadowCastingMode.Off,true,gameObject.layer,camera);
            }
        }
    }
}
