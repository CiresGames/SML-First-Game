using UnityEngine;

namespace MantaFlight.Rider
{
    // A vector ring segment keeps the wheel crisp at any resolution without texture assets.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MantaOrderSector : UnityEngine.UI.MaskableGraphic
    {
        public float angle;
        public float sweep = 86, innerRadius = 108, outerRadius = 242;
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear();
            const int steps = 28;
            for (int i = 0; i <= steps; i++)
            {
                float a = (angle - sweep * .5f + sweep * i / steps) * Mathf.Deg2Rad;
                var direction = new Vector2(Mathf.Sin(a), Mathf.Cos(a));
                mesh.AddVert(direction * innerRadius, color, Vector2.zero);
                mesh.AddVert(direction * outerRadius, color, Vector2.one);
                if (i == 0) continue;
                int n = i * 2;
                mesh.AddTriangle(n - 2, n - 1, n); mesh.AddTriangle(n, n - 1, n + 1);
            }
        }
    }
}
