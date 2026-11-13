using UnityEngine;

namespace ShadowProtocol
{
    /// <summary>
    /// 발소리(3D). 바닥 재질(콘크리트/금속 캣워크/아스팔트 야드)에 따라 다른 소리.
    /// 플레이어는 걷기·달리기·웅크리기에 따라 간격·음량이, 경비병은 속도에 따라 달라진다.
    /// </summary>
    public class FootstepAudio : MonoBehaviour
    {
        public PlayerController player;
        public GuardAI guard;

        Vector3 last;
        float timer, speed;

        void Start()
        {
            if (player == null) player = GetComponent<PlayerController>();
            if (guard == null) guard = GetComponent<GuardAI>();
            last = transform.position;
        }

        void Update()
        {
            var am = AudioManager.Instance;
            float dt = Time.deltaTime;
            if (am == null || dt <= 0f) return;

            Vector3 d = transform.position - last;
            last = transform.position;
            d.y = 0f;
            float inst = d.magnitude / dt;
            if (inst > 15f) inst = 0f; // 순간이동 무시
            speed = Mathf.Lerp(speed, inst, 1f - Mathf.Exp(-12f * dt));

            if (guard != null && guard.IsKnockedOut) return;
            if (speed < 0.4f) { timer = Mathf.Min(timer, 0.12f); return; }

            float interval, vol, maxD;
            if (player != null)
            {
                if (player.IsCrouching) { interval = 0.62f; vol = 0.12f; maxD = 6f; }
                else if (player.IsRunning) { interval = 0.3f; vol = 0.65f; maxD = 16f; }
                else { interval = 0.46f; vol = 0.3f; maxD = 10f; }
            }
            else
            {
                bool run = speed > 3.2f;
                interval = run ? 0.32f : 0.5f;
                vol = run ? 0.8f : 0.55f;
                maxD = run ? 22f : 16f;
            }

            timer -= dt;
            if (timer > 0f) return;
            timer = interval;
            am.PlayAt(AudioManager.Pick(Surface(am)), transform.position + Vector3.up * 0.1f, vol, 1f, maxD, Random.Range(0.92f, 1.08f));
        }

        AudioClip[] Surface(AudioManager am)
        {
            if (Physics.Raycast(transform.position + Vector3.up * 0.4f, Vector3.down, out RaycastHit hit, 1.2f, ~0, QueryTriggerInteraction.Ignore))
            {
                string n = hit.collider.name;
                if (n.StartsWith("Deck") || n.StartsWith("Ramp")) return am.stepMetal;
                if (n == "Floor_Yard") return am.stepAsphalt;
            }
            return am.stepConcrete;
        }
    }
}
