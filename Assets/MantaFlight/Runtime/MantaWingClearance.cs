using System.Collections.Generic;
using UnityEngine;

namespace MantaFlight
{
    // Constrains the visual pose using the imported mesh footprint, independently of the body collider.
    // Mesh skinning happens once at initialization; runtime queries use a small fixed set of probes.
    internal sealed class MantaWingClearance
    {
        readonly Transform root;
        readonly Transform[] joints;
        readonly Quaternion[] rest;
        readonly Vector3 restPosition;
        readonly Quaternion restRotation;
        readonly Vector3[] probes;
        readonly float radius;
        float blend = 1;
        public float Blend => blend;

        public MantaWingClearance(Transform visualRoot, Transform[] animatedJoints)
        {
            root = visualRoot; joints = animatedJoints;
            restPosition = root.localPosition; restRotation = root.localRotation;
            rest = new Quaternion[joints.Length];
            for (int i=0;i<joints.Length;i++) rest[i] = joints[i] ? joints[i].localRotation : Quaternion.identity;
            var points = new List<Vector3>();
            foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var mesh = new Mesh(); renderer.BakeMesh(mesh);
                foreach(var vertex in mesh.vertices) points.Add(root.InverseTransformPoint(renderer.transform.TransformPoint(vertex)));
                if (Application.isPlaying) Object.Destroy(mesh); else Object.DestroyImmediate(mesh);
            }
            if (points.Count == 0)
                foreach (var joint in joints) if (joint) points.Add(root.InverseTransformPoint(joint.position));
            var bounds = new Bounds(Vector3.zero, Vector3.zero);
            foreach(var point in points) { bounds.Encapsulate(point); radius = Mathf.Max(radius, point.magnitude); }
            // Keep the lowest vertex in each footprint cell, including the outer tips and leading/trailing edges.
            var cells = new Dictionary<int,Vector3>();
            foreach(var point in points)
            {
                int x = Mathf.Clamp(Mathf.FloorToInt(Mathf.InverseLerp(bounds.min.x,bounds.max.x,point.x)*8),0,7);
                int z = Mathf.Clamp(Mathf.FloorToInt(Mathf.InverseLerp(bounds.min.z,bounds.max.z,point.z)*8),0,7);
                int key=x+z*8;
                if (!cells.TryGetValue(key,out var old) || point.y<old.y) cells[key]=point;
            }
            probes = new Vector3[cells.Count]; cells.Values.CopyTo(probes,0);
        }
        public void Reset() { blend = 1; }
        public void Apply(float dt, int mask, float margin)
        {
            if (!root || probes.Length == 0 || mask == 0) return;
            margin = Mathf.Max(.05f, margin);
            Vector3 basePosition = root.parent ? root.parent.TransformPoint(restPosition) : restPosition;
            Quaternion baseRotation = root.parent ? root.parent.rotation * restRotation : restRotation;
            Vector3 scale = root.lossyScale;
            float reach = Mathf.Max(1, radius * Mathf.Max(Mathf.Abs(scale.x),Mathf.Abs(scale.y),Mathf.Abs(scale.z)));
            // A broad probe avoids footprint queries altogether when there is ample altitude.
            bool near = Physics.SphereCast(basePosition + Vector3.up*(reach+margin), reach,
                Vector3.down, out _, reach*2+margin, mask, QueryTriggerInteraction.Ignore);
            float target = 1;
            Quaternion safeRotation = baseRotation;
            if (near)
            {
                if (Physics.Raycast(basePosition+Vector3.up*reach,Vector3.down,out var ground,reach*3,mask,QueryTriggerInteraction.Ignore)
                    && Vector3.Dot(ground.normal,Vector3.up)>.4f)
                {
                    Vector3 forward = Vector3.ProjectOnPlane(baseRotation*Vector3.forward,ground.normal);
                    if(forward.sqrMagnitude>.001f) safeRotation = Quaternion.LookRotation(forward,ground.normal);
                }
                var frame = Matrix4x4.TRS(basePosition,safeRotation,scale);
                float available = float.PositiveInfinity;
                foreach(var probe in probes)
                {
                    Vector3 point=frame.MultiplyPoint3x4(probe);
                    if(Physics.Raycast(point+Vector3.up*reach,Vector3.down,out var hit,reach*3,mask,QueryTriggerInteraction.Ignore)
                        && Vector3.Dot(hit.normal,Vector3.up)>.4f)
                        available=Mathf.Min(available,Vector3.Dot(point-hit.point,hit.normal));
                }
                // Arc length bounds the displacement of every point under the combined rotations.
                // Summing both wings is deliberately conservative and also covers inherited parent rotations.
                float angle=Quaternion.Angle(safeRotation,root.rotation);
                for(int i=0;i<joints.Length;i++) if(joints[i]) angle+=Quaternion.Angle(rest[i],joints[i].localRotation);
                float excursion=reach*angle*Mathf.Deg2Rad+Vector3.Distance(basePosition,root.position);
                target=Mathf.Clamp01((available-margin)/Mathf.Max(.001f,excursion));
            }
            // Respond immediately to rising ground; restore the stroke gently when leaving it.
            blend = target < blend ? target : Mathf.Lerp(blend,target,MantaFlightSettings.Damp(3,dt));
            root.rotation=Quaternion.Slerp(safeRotation,root.rotation,blend);
            root.localPosition=Vector3.Lerp(restPosition,root.localPosition,blend);
            for(int i=0;i<joints.Length;i++) if(joints[i]) joints[i].localRotation=Quaternion.Slerp(rest[i],joints[i].localRotation,blend);
        }
    }
}
