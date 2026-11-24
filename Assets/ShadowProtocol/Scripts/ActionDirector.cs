using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace ShadowProtocol
{
    /// <summary>
    /// 액션 연출(어쌔신 크리드 참고, 2026-11-23 사용자 지시):
    /// - 들킨 순간: 짧은 슬로모션 + 카메라가 그 경비 쪽으로 돌아감 + 화면 흔들림 + 붉은 비네트·색수차 펄스
    /// - 암살(등 뒤 제압): 슬로모션 + 낮은 측면 클로즈업 + 채도 낮춘 색감 + 타격 흔들림
    /// 씬에 따로 두지 않아도 게임이 시작되면 스스로 생긴다. 일시정지·결과 화면에서는 시간 배율을 건드리지 않는다.
    /// </summary>
    public class ActionDirector : MonoBehaviour
    {
        static ActionDirector inst;
        public static ActionDirector Instance
        {
            get
            {
                if (inst == null) inst = new GameObject("ActionDirector").AddComponent<ActionDirector>();
                return inst;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            Ensure();
            // 재시도 등으로 씬을 다시 불러와도 다시 생기게
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m) => Ensure();

        static void Ensure()
        {
            if (inst == null && Object.FindAnyObjectByType<GameManager>() != null) { var d = Instance; }
        }

        [Header("들킨 순간")]
        public float detectSlowScale = 0.3f;
        public float detectSlowSeconds = 0.45f;  // 실제 시간
        public float detectFocusSeconds = 0.8f;
        public float detectCooldown = 5f;

        [Header("암살")]
        public float takedownSlowScale = 0.35f;
        public float takedownSlowSeconds = 0.7f;

        float baseFixedDt;
        float slowScale = 1f, slowUntil;
        float lastDetect = -99f;
        Volume alertVol, takedownVol;
        float alertPulse, takedownHold;
        ThirdPersonCamera cam;

        void Awake()
        {
            if (inst != null && inst != this) { Destroy(gameObject); return; }
            inst = this;
            baseFixedDt = Time.fixedDeltaTime;
            alertVol = MakeVolume("ActionFX_Alert", p =>
            {
                var v = p.Add<Vignette>(true);
                v.color.value = new Color(0.45f, 0.02f, 0.02f);
                v.intensity.value = 0.48f;
                v.smoothness.value = 0.7f;
                var ca = p.Add<ChromaticAberration>(true);
                ca.intensity.value = 0.45f;
                var adj = p.Add<ColorAdjustments>(true);
                adj.contrast.value = 14f;
                adj.saturation.value = -15f;
            });
            takedownVol = MakeVolume("ActionFX_Takedown", p =>
            {
                var adj = p.Add<ColorAdjustments>(true);
                adj.saturation.value = -50f;
                adj.contrast.value = 12f;
                var v = p.Add<Vignette>(true);
                v.color.value = Color.black;
                v.intensity.value = 0.45f;
                v.smoothness.value = 0.55f;
            });
        }

        void OnEnable() { GuardAI.StateChanged += OnGuardState; }
        void OnDisable()
        {
            GuardAI.StateChanged -= OnGuardState;
            if (inst == this && GameManager.Instance != null && GameManager.Instance.Flow == GameManager.FlowState.Playing) ResetTime();
        }

        Volume MakeVolume(string name, System.Action<VolumeProfile> fill)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var v = go.AddComponent<Volume>();
            v.isGlobal = true;
            v.priority = 20f;
            v.weight = 0f;
            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            fill(p);
            v.profile = p;
            return v;
        }

        ThirdPersonCamera Cam
        {
            get
            {
                if (cam == null && Camera.main != null) cam = Camera.main.GetComponent<ThirdPersonCamera>();
                return cam;
            }
        }

        static bool Playing => GameManager.Instance != null && GameManager.Instance.Flow == GameManager.FlowState.Playing;

        // ---------------------------------------------------------------- 공개 API

        public void SlowMo(float scale, float realSeconds)
        {
            if (!Playing) return;
            slowScale = Mathf.Min(scale, Time.unscaledTime < slowUntil ? slowScale : 1f);
            slowUntil = Mathf.Max(slowUntil, Time.unscaledTime + realSeconds);
        }

        /// <summary>등 뒤 제압 연출: 슬로모션·측면 클로즈업·색감. 실제 처리(기절)는 PlayerTools가 한다.</summary>
        public void Takedown(PlayerController p, GuardAI g)
        {
            SlowMo(takedownSlowScale, takedownSlowSeconds);
            takedownHold = takedownSlowSeconds + 0.25f;
            var c = Cam;
            if (c == null || p == null || g == null) return;
            Vector3 mid = (p.transform.position + g.transform.position) * 0.5f + Vector3.up * 1.0f;
            Vector3 toG = g.transform.position - p.transform.position; toG.y = 0f;
            toG = toG.sqrMagnitude > 0.001f ? toG.normalized : p.transform.forward;
            Vector3 side = Vector3.Cross(Vector3.up, toG);
            // 1) 플레이어 어깨 뒤 낮고 가깝게(방금 지나온 쪽이라 거의 비어 있음) 2) 반대 어깨 3) 측면
            var cands = new[]
            {
                mid - toG * 2.0f + side * 0.85f + Vector3.up * 0.3f,
                mid - toG * 2.0f - side * 0.85f + Vector3.up * 0.3f,
                mid + side * 2.0f - toG * 0.6f,
                mid - side * 2.0f - toG * 0.6f,
            };
            foreach (var pos in cands)
            {
                if (Blocked(mid, pos - mid)) continue;
                c.CinematicShot(pos, mid + Vector3.down * 0.15f + toG * 0.3f, takedownSlowSeconds + 0.15f);
                return;
            }
        }

        public void Impact(float amount) { if (Cam != null) Cam.Shake(amount); }

        static bool Blocked(Vector3 from, Vector3 delta)
        {
            float len = delta.magnitude;
            var hits = Physics.SphereCastAll(from, 0.2f, delta / len, len, ~0, QueryTriggerInteraction.Ignore);
            foreach (var h in hits)
            {
                if (h.collider.GetComponentInParent<PlayerController>() != null || h.collider.GetComponentInParent<GuardAI>() != null) continue;
                return true;
            }
            // 충돌체 없는 장식(팔레트·상자 등)도 화면을 가리므로 렌더러 경계로 한 번 더 본다
            var ray = new Ray(from, delta / len);
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude))
            {
                if (!r.enabled || r.shadowCastingMode == ShadowCastingMode.ShadowsOnly) continue;
                var b = r.bounds;
                if (b.size.y < 0.4f || b.size.magnitude > 12f) continue; // 바닥·큰 벽 합본은 위 물리 검사로 충분
                if ((b.center - from).sqrMagnitude > (len + 3f) * (len + 3f)) continue;
                if (r.GetComponentInParent<PlayerController>() != null || r.GetComponentInParent<GuardAI>() != null) continue;
                if (b.IntersectRay(ray, out float dist) && dist < len) return true;
            }
            return false;
        }

        // ---------------------------------------------------------------- 들킨 순간

        void OnGuardState(GuardAI g, GuardState prev, GuardState next)
        {
            if (next != GuardState.Alert || prev == GuardState.Alert || g == null || !g.CanSeePlayer) return;
            if (!Playing || Time.unscaledTime - lastDetect < detectCooldown) return;
            lastDetect = Time.unscaledTime;
            SlowMo(detectSlowScale, detectSlowSeconds);
            alertPulse = 1f;
            var c = Cam;
            if (c != null)
            {
                c.Focus(g.transform, detectFocusSeconds);
                c.Shake(0.9f);
            }
        }

        // ---------------------------------------------------------------- 매 프레임

        void Update()
        {
            float udt = Time.unscaledDeltaTime;
            if (Playing)
            {
                float target = Time.unscaledTime < slowUntil ? slowScale : 1f;
                // 들어갈 때는 빠르게, 나올 때는 부드럽게
                float rate = target < Time.timeScale ? 14f : 3.5f;
                Time.timeScale = Mathf.MoveTowards(Time.timeScale, target, rate * udt);
                Time.fixedDeltaTime = baseFixedDt * Time.timeScale;
                if (Time.unscaledTime >= slowUntil) slowScale = 1f;
            }
            else
            {
                // 일시정지·결과 화면은 GameManager가 시간을 관리한다
                slowUntil = 0f; slowScale = 1f;
                if (Time.fixedDeltaTime != baseFixedDt && Time.timeScale >= 0.999f) Time.fixedDeltaTime = baseFixedDt;
            }

            // 화면 효과 가중치
            alertPulse = Mathf.MoveTowards(alertPulse, 0f, udt * 0.8f);
            if (alertVol != null) alertVol.weight = Mathf.SmoothStep(0f, 1f, alertPulse);
            takedownHold -= udt;
            if (takedownVol != null)
            {
                float want = takedownHold > 0f ? 1f : 0f;
                takedownVol.weight = Mathf.MoveTowards(takedownVol.weight, want, udt * (want > takedownVol.weight ? 8f : 2.5f));
            }
        }

        void ResetTime()
        {
            Time.timeScale = 1f;
            Time.fixedDeltaTime = baseFixedDt;
        }
    }
}
