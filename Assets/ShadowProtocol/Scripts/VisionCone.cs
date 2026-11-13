using UnityEngine;

namespace ShadowProtocol
{
    /// <summary>
    /// 경비병 시야를 바닥에 반투명 부채꼴 메시로 그린다. 벽에 가리면 잘린다.
    /// 경비병의 자식(로컬 원점, 회전 없음)에 붙인다.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class VisionCone : MonoBehaviour
    {
        public GuardAI guard;
        [Range(8, 96)] public int rayCount = 40;
        public float castHeight = 1.0f;
        public float alpha = 0.3f;
        public LayerMask obstacleMask = ~0;

        Mesh mesh;
        Vector3[] vertices;
        Color[] colors;
        int[] triangles;
        readonly RaycastHit[] hits = new RaycastHit[16];
        // 콜라이더가 시야를 막는 장애물인지(캐릭터가 아닌지) 캐시
        readonly System.Collections.Generic.Dictionary<Collider, bool> blockerCache = new System.Collections.Generic.Dictionary<Collider, bool>();

        void Awake()
        {
            if (guard == null) guard = GetComponentInParent<GuardAI>();
            mesh = new Mesh { name = "VisionConeMesh" };
            mesh.MarkDynamic();
            GetComponent<MeshFilter>().sharedMesh = mesh;
            Allocate();
        }

        void Allocate()
        {
            vertices = new Vector3[rayCount + 2];
            colors = new Color[rayCount + 2];
            triangles = new int[rayCount * 3];
            for (int i = 0; i < rayCount; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = i + 2;
            }
        }

        void LateUpdate()
        {
            if (guard == null) return;
            if (guard.IsKnockedOut) { if (mesh.vertexCount > 0) mesh.Clear(); return; }
            if (vertices.Length != rayCount + 2) Allocate();

            float half = guard.viewAngle * 0.5f;
            float range = guard.EffectiveViewDistance;
            Transform g = guard.transform;
            Vector3 origin = g.position + Vector3.up * castHeight;
            Vector3 eyeOrigin = guard.EyePosition;
            Vector3 forward = Vector3.ProjectOnPlane(g.forward, Vector3.up).normalized;

            Color baseColor = GuardAI.ColorFor(guard.State);
            if (guard.State == GuardState.Patrol)
                baseColor = Color.Lerp(baseColor, GuardAI.ColorFor(GuardState.Suspicious), guard.Suspicion * 2f);

            Color center = baseColor; center.a = Mathf.Clamp01(alpha * 1.8f);
            Color edge = baseColor; edge.a = alpha * 0.5f;

            vertices[0] = Vector3.zero;
            colors[0] = center;

            for (int i = 0; i <= rayCount; i++)
            {
                float a = -half + guard.viewAngle * i / rayCount;
                Vector3 dir = Quaternion.AngleAxis(a, Vector3.up) * forward;
                // 낮은 높이(낮은 엄폐물)와 눈높이(환기구·덕트 상인방) 둘 다에 막히는 거리 중 짧은 쪽
                float d = Mathf.Min(CastDistance(origin, dir, range), CastDistance(eyeOrigin, dir, range));
                Vector3 world = g.position + dir * d;
                Vector3 local = transform.InverseTransformPoint(world);
                local.y = 0f;
                vertices[i + 1] = local;
                colors[i + 1] = Color.Lerp(center, edge, d / Mathf.Max(range, 0.01f));
            }

            mesh.Clear();
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
        }

        float CastDistance(Vector3 origin, Vector3 dir, float range)
        {
            int count = Physics.RaycastNonAlloc(origin, dir, hits, range, obstacleMask, QueryTriggerInteraction.Ignore);
            float best = range;
            for (int i = 0; i < count; i++)
            {
                var col = hits[i].collider;
                if (!blockerCache.TryGetValue(col, out bool blocks))
                {
                    blocks = col.GetComponentInParent<GuardAI>() == null && col.GetComponentInParent<PlayerController>() == null;
                    blockerCache[col] = blocks;
                }
                if (!blocks) continue;
                if (hits[i].distance < best) best = hits[i].distance;
            }
            return best;
        }
    }
}
