using UnityEngine;

namespace ShadowProtocol
{
    /// <summary>
    /// 서버실 데이터 코어 앞 레이저 격자: 빔 4줄이 정해진 순서(판마다 무작위, 예 1-3-2-4)로 한 줄씩 꺼진다(한 박자 1초, 박자음).
    /// 꺼진 줄만 지날 수 있고, 켜진 빔에 닿으면 경보(근처 경비 수색 + 발각 1회).
    /// </summary>
    public class LaserGrid : MonoBehaviour
    {
        [System.Serializable]
        public class Beam
        {
            public Vector3 a, b;
            public Renderer[] visuals;
            public Light glow;
        }

        public Beam[] beams;
        public float stepSeconds = 1.1f;
        public float hitRadius = 0.32f;
        public float alarmRadius = 30f;
        public PlayerController player;
        public int[] order = { 0, 2, 1, 3 };

        public int Step { get; private set; }
        public int Trips { get; private set; }
        float timer, cooldown;
        int lastStep = -1;

        void Start()
        {
            if (player == null) player = FindAnyObjectByType<PlayerController>();
            // 판마다 꺼지는 순서를 섞는다
            var rng = new System.Random(PuzzleState.Seed ^ 0x1A5E);
            for (int i = order.Length - 1; i > 0; i--) { int j = rng.Next(0, i + 1); int t = order[i]; order[i] = order[j]; order[j] = t; }
            Apply();
        }

        /// <summary>지금 꺼져 있는 빔</summary>
        public int OffBeam => order[Step % order.Length];
        public bool IsOn(int beam) => beam != OffBeam;
        /// <summary>t초 뒤에 꺼져 있을 빔(봇의 타이밍 계산용)</summary>
        public int OffBeamAfter(float t) => order[(Step + Mathf.FloorToInt((timer + t) / stepSeconds)) % order.Length];
        public float TimeToNextStep => stepSeconds - timer;
        public string OrderText
        {
            get { var s = ""; for (int i = 0; i < order.Length; i++) s += (i > 0 ? "-" : "") + (order[i] + 1); return s; }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (!GameManager.GameplayActive) return;
            timer += dt;
            if (timer >= stepSeconds)
            {
                timer -= stepSeconds;
                Step++;
            }
            if (Step != lastStep) Apply();

            cooldown -= dt;
            if (player == null || cooldown > 0f) return;
            Vector3 p = player.transform.position;
            for (int i = 0; i < beams.Length; i++)
            {
                if (!IsOn(i)) continue;
                if (DistanceXZ(p, beams[i].a, beams[i].b) > hitRadius) continue;
                Trip(i, p);
                break;
            }
        }

        void Apply()
        {
            lastStep = Step;
            for (int i = 0; i < beams.Length; i++)
            {
                bool on = IsOn(i);
                if (beams[i].visuals != null) foreach (var r in beams[i].visuals) if (r != null) r.enabled = on;
                if (beams[i].glow != null) beams[i].glow.enabled = on;
            }
            var am = AudioManager.Instance;
            if (am != null && player != null && Vector3.Distance(player.transform.position, transform.position) < 14f)
                am.PlayAt(am.uiClick, transform.position, 0.5f, 1f, 9f, 0.55f + 0.15f * OffBeam);
        }

        static float DistanceXZ(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector2 P = new Vector2(p.x, p.z), A = new Vector2(a.x, a.z), B = new Vector2(b.x, b.z);
            Vector2 ab = B - A;
            float t = Mathf.Clamp01(Vector2.Dot(P - A, ab) / ab.sqrMagnitude);
            return Vector2.Distance(P, A + ab * t);
        }

        /// <summary>점에서 빔까지 거리(봇용)</summary>
        public float Distance(int beam, Vector3 p) => DistanceXZ(p, beams[beam].a, beams[beam].b);

        void Trip(int beam, Vector3 p)
        {
            cooldown = 4f;
            Trips++;
            var am = AudioManager.Instance;
            if (am != null) { am.PlayAt(am.guardAlert, p + Vector3.up, 1f, 2f, 40f, 1.2f); }
            if (GameManager.Instance != null) GameManager.Instance.RegisterDetection("레이저 경보");
            int n = 0;
            foreach (var g in GuardAI.All)
            {
                if (g == null || g.IsKnockedOut) continue;
                if (Vector3.Distance(g.transform.position, p) > alarmRadius) continue;
                g.ReceiveAlarm(p);
                n++;
            }
            if (GuardDirector.Instance != null) GuardDirector.Instance.RaiseAlarm(p, $"[무전] 보안 시스템: 서버실 레이저 감지! 경비 {n}명 출동");
            Debug.Log($"[ShadowProtocol] 레이저 {beam + 1}번에 닿음 → 경보 ({n}명 수색)");
        }
    }
}
