using UnityEngine;

namespace ShadowProtocol
{
    /// <summary>
    /// 목표 지점. 플레이어가 닿으면 성공. 빛나는 큐브를 돌리고 깜빡인다.
    /// </summary>
    public class GoalZone : MonoBehaviour
    {
        public Transform visual;
        public Light glow;
        public PlayerController player;
        public float spinSpeed = 60f;
        public float bobHeight = 0.15f;
        public float reachRadius = 1.1f;

        Vector3 visualBase;
        float glowBase;

        void Start()
        {
            if (player == null) player = FindAnyObjectByType<PlayerController>();
            if (visual != null) visualBase = visual.localPosition;
            if (glow != null) glowBase = glow.intensity;
        }

        void Update()
        {
            float t = Time.time;
            // 데이터를 확보하면 홀로그램과 발광을 끈다
            bool taken = GameManager.Instance != null && GameManager.Instance.HasData;
            if (visual != null && visual.gameObject.activeSelf == taken) visual.gameObject.SetActive(!taken);
            if (glow != null) glow.enabled = !taken;
            if (taken) return;
            if (visual != null)
            {
                visual.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
                visual.localPosition = visualBase + Vector3.up * Mathf.Sin(t * 2f) * bobHeight;
            }
            if (glow != null) glow.intensity = glowBase * (1f + 0.3f * Mathf.Sin(t * 3f));

            // 트리거 이벤트가 누락돼도 동작하도록 거리 판정도 함께 한다.
            if (player != null && GameManager.GameplayActive)
            {
                Vector3 d = player.transform.position - transform.position;
                d.y = 0f;
                if (d.magnitude <= reachRadius) Reach();
            }
        }

        void OnTriggerEnter(Collider other)
        {
            if (GameManager.GameplayActive && other.GetComponentInParent<PlayerController>() != null) Reach();
        }

        void Reach()
        {
            if (GameManager.Instance != null) GameManager.Instance.PlayerReachedGoal();
        }
    }
}
