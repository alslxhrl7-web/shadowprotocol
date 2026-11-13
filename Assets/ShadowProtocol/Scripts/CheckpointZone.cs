using UnityEngine;

namespace ShadowProtocol
{
    /// <summary>
    /// 구역 체크포인트(홀·사무동·서버실). 들어가서 주변이 안전할 때(경계 중인 경비 없음, 가장 가까운 경비 6m 밖) 자동 저장.
    /// 탈출 단계에서는 따로 한 번 더 저장된다. 실패 시 마지막 체크포인트에서 재시작(적응 AI 기록은 유지).
    /// </summary>
    public class CheckpointZone : MonoBehaviour
    {
        public int index = 1;
        public string label = "메인 홀";
        public Vector3 size = new Vector3(8f, 3f, 8f);
        public PlayerController player;
        public float safeDistance = 6f;

        float timer;

        void Start()
        {
            if (player == null) player = FindAnyObjectByType<PlayerController>();
        }

        void Update()
        {
            if (!GameManager.GameplayActive || player == null) return;
            timer -= Time.deltaTime;
            if (timer > 0f) return;
            timer = 0.25f;
            var gm = GameManager.Instance;
            if (gm == null) return;
            bool extract = gm.Phase == GameManager.MissionPhase.Extract;
            int key = index + (extract ? 100 : 0);
            if (CheckpointData.SavedKeys.Contains(key)) return;
            if (!new Bounds(transform.position, size).Contains(player.transform.position + Vector3.up * 0.5f)) return;
            foreach (var g in GuardAI.All)
            {
                if (g == null || g.IsKnockedOut) continue;
                if (g.State != GuardState.Patrol) return;
                if (Vector3.Distance(g.transform.position, player.transform.position) < safeDistance) return;
            }
            gm.SaveCheckpoint(key, label + (extract ? " (탈출 중)" : ""));
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.4f, 1f, 0.5f, 0.3f);
            Gizmos.DrawWireCube(transform.position, size);
        }
    }
}
