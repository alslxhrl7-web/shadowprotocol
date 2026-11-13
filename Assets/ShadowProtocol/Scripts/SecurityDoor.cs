using UnityEngine;

namespace ShadowProtocol
{
    /// <summary>
    /// 서버실 보안문. 잠겨 있으면 플레이어를 막는다(키패드로 연다). 경비는 출입증이 있어 가까이 오면 자동으로 열린다
    /// (경비는 NavMesh로 다니므로 문 콜라이더는 NavMesh에 굽지 않는다).
    /// </summary>
    public class SecurityDoor : MonoBehaviour
    {
        public Transform panel;
        public Vector3 openOffset = new Vector3(1.9f, 0f, 0f);
        public Collider blocker;
        public Renderer lamp;
        public bool locked = true;
        public float guardOpenRadius = 2.6f;

        /// <summary>지휘가 경비를 안쪽으로 보낼 때: 잠겨 있어도 경비는 지나간다</summary>
        public bool guardOverride;
        UnityEngine.AI.NavMeshObstacle obstacle;
        Vector3 closedPos;
        float open;
        MaterialPropertyBlock mpb;

        public bool IsOpen => open > 0.9f;

        void Start()
        {
            if (panel != null) closedPos = panel.localPosition;
            mpb = new MaterialPropertyBlock();
            // 잠긴 문은 경비의 경로에서도 막힌 것으로 취급(NavMesh 깎기). 플레이어가 열면 경비도 지나다닌다.
            if (panel != null)
            {
                obstacle = gameObject.AddComponent<UnityEngine.AI.NavMeshObstacle>();
                obstacle.shape = UnityEngine.AI.NavMeshObstacleShape.Box;
                obstacle.center = closedPos;
                obstacle.size = panel.localScale;
                obstacle.carving = true;
            }
            UpdateLamp();
        }

        public void Unlock()
        {
            locked = false;
            UpdateLamp();
        }

        void UpdateLamp()
        {
            if (lamp == null) return;
            Color c = locked ? new Color(1f, 0.2f, 0.15f) : new Color(0.3f, 1f, 0.45f);
            lamp.GetPropertyBlock(mpb);
            mpb.SetColor("_Color", c);
            mpb.SetColor("_EmissionColor", c * 2f);
            lamp.SetPropertyBlock(mpb);
        }

        void Update()
        {
            bool want = !locked;
            if (!want && guardOverride)
            {
                foreach (var g in GuardAI.All)
                {
                    if (g == null || g.IsKnockedOut) continue;
                    Vector3 d = g.transform.position - transform.position;
                    d.y = 0f;
                    if (d.magnitude < guardOpenRadius) { want = true; break; }
                }
            }
            open = Mathf.MoveTowards(open, want ? 1f : 0f, Time.deltaTime * 2.2f);
            if (panel != null) panel.localPosition = closedPos + openOffset * open;
            if (blocker != null) blocker.enabled = open < 0.85f;
            if (obstacle != null) obstacle.enabled = locked && !guardOverride;
        }
    }
}
