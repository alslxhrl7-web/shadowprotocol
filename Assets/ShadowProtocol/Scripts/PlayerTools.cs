using System.Collections.Generic;
using UnityEngine;

namespace ShadowProtocol
{
    /// <summary>
    /// 플레이어 도구: 던지기(병/캔, 개수 제한·줍기, 포물선 미리보기, 착지 소음으로 경비 유인),
    /// 등 뒤 제압(E), 기절체 끌기(F).
    /// 우클릭 누른 채 조준 → 좌클릭 던지기, G 빠른 던지기(화면 가운데 지점).
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerTools : MonoBehaviour
    {
        public PlayerController player;
        public ThirdPersonCamera cam;
        public CharacterAnimDriver anim;
        public CharacterProceduralPose pose;

        [Header("던지기")]
        public int maxThrowables = 3;
        public int startThrowables = 1;
        public float maxThrowDistance = 16f;
        public float minThrowDistance = 2f;
        public float bottleNoise = 11f;
        public float canNoise = 8f;
        public Material bottleMat, canMat;
        public LineRenderer arc;
        public LineRenderer landingRing;

        [Header("제압·끌기")]
        public float takedownNoise = 2.5f;
        public float dragSpeedMultiplier = 0.55f;
        public float dragDistance = 1.15f;

        public int Throwables
        {
            get => kinds.Count;
            set
            {
                kinds.Clear();
                for (int i = 0; i < Mathf.Clamp(value, 0, maxThrowables); i++) kinds.Add(ThrowKind.Bottle);
            }
        }
        public ThrowKind NextKind => kinds.Count > 0 ? kinds[kinds.Count - 1] : ThrowKind.Bottle;
        public bool Aiming { get; private set; }
        public Vector3 AimPoint { get; private set; }
        public bool AimValid { get; private set; }
        public GuardAI TakedownTarget { get; private set; }
        public GuardAI DragCandidate { get; private set; }
        public GuardAI Dragging { get; private set; }
        public string Prompt { get; private set; }
        public int ThrowsMade { get; private set; }

        readonly List<ThrowKind> kinds = new List<ThrowKind>();
        Transform hand;
        CharacterController cc;
        float pendingRelease = -1f;
        Vector3 pendingTarget;
        ThrowKind pendingKind;
        readonly Vector3[] arcPts = new Vector3[48];

        void Awake()
        {
            if (player == null) player = GetComponent<PlayerController>();
            cc = GetComponent<CharacterController>();
            Throwables = startThrowables;
        }

        void Start()
        {
            if (cam == null) cam = FindAnyObjectByType<ThirdPersonCamera>();
            if (anim == null) anim = GetComponentInChildren<CharacterAnimDriver>();
            if (pose == null) pose = GetComponentInChildren<CharacterProceduralPose>();
            var a = GetComponentInChildren<Animator>();
            if (a != null && a.isHuman) hand = a.GetBoneTransform(HumanBodyBones.RightHand);
            if (hand == null) foreach (var t in GetComponentsInChildren<Transform>()) if (t.name == "RightHand") { hand = t; break; } // Generic 플레이어
            ShowArc(false);
        }

        void Update()
        {
            Prompt = null;
            if (!GameManager.GameplayActive || !player.InputEnabled)
            {
                Aiming = false;
                ShowArc(false);
                player.FaceDirection = null;
                return;
            }

            UpdatePickups();
            UpdatePendingThrow();
            UpdateDrag();
            UpdateTakedownTarget();

            bool userInput = !player.UseVirtualInput;
            if (userInput)
            {
                Aiming = Input.GetMouseButton(1) && kinds.Count > 0 && Dragging == null;
                if (Aiming) ComputeAim();
                if (Aiming && Input.GetMouseButtonDown(0) && AimValid) ThrowAt(AimPoint);
                else if (Input.GetKeyDown(KeyCode.G) && kinds.Count > 0 && Dragging == null)
                {
                    ComputeAim();
                    if (AimValid) ThrowAt(AimPoint);
                }
                if (Input.GetKeyDown(KeyCode.E))
                {
                    if (!TryTakedown() && Dragging == null)
                    {
                        var pi = PuzzleInteractable.Nearest(transform.position);
                        if (pi != null) pi.Interact(this);
                    }
                }
                if (Input.GetKeyDown(KeyCode.F)) ToggleDrag();
            }

            if (Aiming)
            {
                Vector3 f = AimPoint - transform.position;
                f.y = 0f;
                player.FaceDirection = f.sqrMagnitude > 0.01f ? f : (Vector3?)null;
                DrawArc();
            }
            else
            {
                if (pendingRelease < 0f) player.FaceDirection = null;
                ShowArc(false);
            }

            // 안내 문구(우선순위: 내려놓기 > 제압 > 끌기)
            if (Dragging != null) Prompt = "[F] 내려놓기";
            else if (TakedownTarget != null) Prompt = "[E] 등 뒤에서 제압";
            else if (DragCandidate != null) Prompt = "[F] 기절한 경비 끌기";
            else
            {
                var pi = PuzzleInteractable.Nearest(transform.position);
                if (pi != null) Prompt = pi.PromptText;
            }
        }

        // ---------------- 던지기 ----------------

        void ComputeAim()
        {
            AimValid = false;
            Camera c = cam != null ? cam.GetComponent<Camera>() : Camera.main;
            if (c == null) return;
            float skip = cam != null ? cam.CurrentDistance + 0.6f : 1f;
            Ray ray = new Ray(c.transform.position + c.transform.forward * skip, c.transform.forward);
            Vector3 target;
            if (Physics.Raycast(ray, out RaycastHit hit, 40f, ~0, QueryTriggerInteraction.Ignore) && !hit.collider.transform.IsChildOf(transform))
                target = hit.point;
            else
                target = ray.GetPoint(maxThrowDistance);
            Vector3 flat = target - transform.position;
            flat.y = 0f;
            float d = flat.magnitude;
            if (d > maxThrowDistance) target = transform.position + flat / d * maxThrowDistance + Vector3.up * (target.y - transform.position.y);
            else if (d < minThrowDistance) target = transform.position + (d > 0.01f ? flat / d : transform.forward) * minThrowDistance;
            // 공중이면 바닥으로 내린다
            if (Physics.Raycast(target + Vector3.up * 0.5f, Vector3.down, out RaycastHit down, 12f, ~0, QueryTriggerInteraction.Ignore))
                target = down.point;
            AimPoint = target;
            AimValid = true;
        }

        Vector3 ReleasePoint()
        {
            if (hand != null) return hand.position;
            return transform.position + Vector3.up * 1.6f + transform.forward * 0.35f + transform.right * 0.2f;
        }

        static Vector3 LaunchVelocity(Vector3 from, Vector3 to, out float time)
        {
            Vector3 flat = to - from;
            flat.y = 0f;
            time = Mathf.Clamp(flat.magnitude / 10.5f, 0.4f, 1.3f);
            return (to - from) / time - 0.5f * Physics.gravity * time;
        }

        /// <summary>목표 지점으로 던진다(봇·테스트도 사용)</summary>
        public bool ThrowAt(Vector3 target)
        {
            if (kinds.Count == 0 || pendingRelease >= 0f || Dragging != null) return false;
            pendingKind = kinds[kinds.Count - 1];
            kinds.RemoveAt(kinds.Count - 1);
            pendingTarget = target;
            pendingRelease = anim != null && anim.Animator != null && anim.Animator.runtimeAnimatorController != null ? anim.ThrowReleaseDelay : 0.15f;
            Vector3 f = target - transform.position;
            f.y = 0f;
            if (f.sqrMagnitude > 0.01f) player.FaceDirection = f;
            if (anim != null) anim.PlayThrow();
            Aiming = false;
            ShowArc(false);
            return true;
        }

        void UpdatePendingThrow()
        {
            if (pendingRelease < 0f) return;
            pendingRelease -= Time.deltaTime;
            if (pendingRelease > 0f) return;
            pendingRelease = -1f;
            player.FaceDirection = null;

            Vector3 origin = ReleasePoint();
            Vector3 v = LaunchVelocity(origin, pendingTarget, out _);
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = pendingKind == ThrowKind.Bottle ? "Thrown_Bottle" : "Thrown_Can";
            go.transform.position = origin;
            go.transform.localScale = pendingKind == ThrowKind.Bottle ? new Vector3(0.09f, 0.13f, 0.09f) : new Vector3(0.08f, 0.06f, 0.08f);
            go.transform.rotation = Random.rotation;
            var r = go.GetComponent<Renderer>();
            if (pendingKind == ThrowKind.Bottle && bottleMat != null) r.sharedMaterial = bottleMat;
            if (pendingKind == ThrowKind.Can && canMat != null) r.sharedMaterial = canMat;
            var col = go.GetComponent<Collider>();
            if (cc != null) Physics.IgnoreCollision(col, cc);
            foreach (var pc in GetComponentsInChildren<Collider>()) if (pc != cc) Physics.IgnoreCollision(col, pc);
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.4f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.linearVelocity = v;
            rb.angularVelocity = Random.insideUnitSphere * 12f;
            var t = go.AddComponent<ThrownObject>();
            t.kind = pendingKind;
            t.noiseRadius = pendingKind == ThrowKind.Bottle ? bottleNoise : canNoise;
            ThrowsMade++;
            var am = AudioManager.Instance;
            if (am != null) am.PlayAt(am.throwWhoosh, origin, 0.6f, 1f, 8f);
        }

        void DrawArc()
        {
            if (arc == null || !AimValid) { ShowArc(false); return; }
            Vector3 origin = ReleasePoint();
            Vector3 v = LaunchVelocity(origin, AimPoint, out float T);
            int n = 1;
            arcPts[0] = origin;
            Vector3 landing = AimPoint;
            float step = T * 1.25f / (arcPts.Length - 1);
            for (int i = 1; i < arcPts.Length; i++)
            {
                float t = step * i;
                Vector3 p = origin + v * t + 0.5f * Physics.gravity * t * t;
                Vector3 prev = arcPts[n - 1];
                Vector3 seg = p - prev;
                if (Physics.Raycast(prev, seg.normalized, out RaycastHit hit, seg.magnitude, ~0, QueryTriggerInteraction.Ignore) && !hit.collider.transform.IsChildOf(transform))
                {
                    arcPts[n++] = hit.point;
                    landing = hit.point;
                    break;
                }
                arcPts[n++] = p;
                landing = p;
            }
            arc.enabled = true;
            arc.positionCount = n;
            arc.SetPositions(arcPts);
            if (landingRing != null)
            {
                landingRing.enabled = true;
                landingRing.transform.position = landing + Vector3.up * 0.05f;
                float r = (NextKind == ThrowKind.Bottle ? bottleNoise : canNoise) / 11f * 0.6f + 0.25f;
                landingRing.transform.localScale = Vector3.one * r;
            }
        }

        void ShowArc(bool on)
        {
            if (arc != null) arc.enabled = on;
            if (landingRing != null) landingRing.enabled = on;
        }

        void UpdatePickups()
        {
            if (kinds.Count >= maxThrowables) return;
            for (int i = ThrowablePickup.All.Count - 1; i >= 0; i--)
            {
                var p = ThrowablePickup.All[i];
                if (p == null) continue;
                Vector3 d = p.transform.position - transform.position;
                if (Mathf.Abs(d.y) > 1.2f) continue;
                d.y = 0f;
                if (d.magnitude > p.radius) continue;
                kinds.Add(p.kind);
                var am = AudioManager.Instance;
                if (am != null) am.PlayAt(am.pickup, p.transform.position, 0.8f, 1f, 6f);
                if (GameManager.Instance != null) GameManager.Instance.ShowBanner($"{ThrowablePickup.KindName(p.kind)} 획득 ({kinds.Count}/{maxThrowables})  우클릭 조준 · 좌클릭 던지기", 2.5f);
                Destroy(p.gameObject);
                if (kinds.Count >= maxThrowables) break;
            }
        }

        public void AddThrowable(ThrowKind k)
        {
            if (kinds.Count < maxThrowables) kinds.Add(k);
        }

        // ---------------- 제압 ----------------

        void UpdateTakedownTarget()
        {
            TakedownTarget = null;
            DragCandidate = null;
            if (Dragging != null) return;
            float best = float.MaxValue, bestBody = float.MaxValue;
            foreach (var g in GuardAI.All)
            {
                if (g == null) continue;
                Vector3 d = g.transform.position - transform.position;
                if (Mathf.Abs(d.y) > 1f) continue;
                d.y = 0f;
                float m = d.magnitude;
                if (g.IsKnockedOut)
                {
                    if (m < 1.8f && m < bestBody) { bestBody = m; DragCandidate = g; }
                }
                else if (m < best && g.CanBeTakenDownBy(player))
                {
                    best = m;
                    TakedownTarget = g;
                }
            }
        }

        public bool TryTakedown()
        {
            if (Dragging != null) return false;
            UpdateTakedownTarget();
            var g = TakedownTarget;
            if (g == null) return false;
            Vector3 f = g.transform.position - transform.position;
            f.y = 0f;
            if (f.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(f.normalized, Vector3.up);
            // 판정(기절)은 바로, 쓰러지는 모습은 돌진 후 내려치는 순간에 맞춘다
            g.FallDelay = TakedownDashTime + 0.06f;
            g.KnockOut();
            StartCoroutine(TakedownRoutine(g));
            NoiseSystem.Emit(g.transform.position, takedownNoise, g.gameObject);
            if (GameManager.Instance != null) GameManager.Instance.RegisterKnockout();
            Debug.Log($"[ShadowProtocol] 제압: {g.name} ({g.transform.position.x:0.0}, {g.transform.position.z:0.0})");
            TakedownTarget = null;
            return true;
        }

        const float TakedownDashTime = 0.2f;
        public bool InTakedown { get; private set; }

        /// <summary>
        /// 암살 연출(어쌔신 크리드 참고): 슬로모션·측면 클로즈업 속에서 경비 등 뒤로 짧게 돌진 →
        /// 내려치는 순간 타격음·화면 흔들림, 경비가 쓰러진다. 그동안 조작은 잠근다.
        /// </summary>
        System.Collections.IEnumerator TakedownRoutine(GuardAI g)
        {
            InTakedown = true;
            bool prevInput = player.InputEnabled;
            player.InputEnabled = false;
            ActionDirector.Instance.Takedown(player, g);
            // AC3식: 돌진하며 왼손으로 입·어깨를 덮어 잡고, 붙는 순간 오른손으로 목덜미를 찌른다(손 IK)
            if (pose != null) { pose.assassinReach = TakedownDashTime; pose.PlayAssassinate(g); }

            var cc = player.GetComponent<CharacterController>();
            Vector3 start = transform.position;
            Vector3 gb = g.transform.position - g.transform.forward * 0.42f; // 경비 등 바로 뒤(손이 입·목에 닿는 거리)
            gb.y = start.y;
            float t = 0f;
            while (t < TakedownDashTime && g != null)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / TakedownDashTime));
                Vector3 target = Vector3.Lerp(start, gb, k);
                Vector3 step = target - transform.position; step.y = 0f;
                if (cc != null && cc.enabled) cc.Move(step); else transform.position += step;
                Vector3 look = g.transform.position - transform.position; look.y = 0f;
                if (look.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
                yield return null;
            }

            var am = AudioManager.Instance;
            if (am != null && g != null) am.PlayAt(am.knockout, g.transform.position + Vector3.up, 1f, 1.5f, 14f);
            ActionDirector.Instance.Impact(1f);

            yield return new WaitForSeconds(0.22f);
            // 쓰러지는 몸에 겹치지 않게 반 걸음 물러난다
            Vector3 away = g != null ? transform.position - g.transform.position : -transform.forward;
            away.y = 0f;
            away = away.sqrMagnitude > 0.001f ? away.normalized : -transform.forward;
            float bt = 0f;
            while (bt < 0.25f)
            {
                bt += Time.deltaTime;
                Vector3 step = away * (1.6f * Time.deltaTime);
                if (cc != null && cc.enabled) cc.Move(step); else transform.position += step;
                yield return null;
            }
            player.ResetMotion();
            if (GameManager.GameplayActive) player.InputEnabled = prevInput; // 그사이 게임이 끝났으면 잠금 유지
            InTakedown = false;
        }

        // ---------------- 끌기 ----------------

        public bool ToggleDrag()
        {
            if (Dragging != null)
            {
                Dragging.BeingDragged = false;
                Debug.Log($"[ShadowProtocol] 기절체 내려놓음: {Dragging.name} ({Dragging.transform.position.x:0.0}, {Dragging.transform.position.z:0.0})");
                Dragging = null;
                player.SpeedMultiplier = 1f;
                player.BlockRun = false;
                return true;
            }
            UpdateTakedownTarget();
            if (DragCandidate == null) return false;
            Dragging = DragCandidate;
            Dragging.BeingDragged = true;
            player.SpeedMultiplier = dragSpeedMultiplier;
            player.BlockRun = true;
            return true;
        }

        void UpdateDrag()
        {
            if (Dragging == null) return;
            if (!Dragging.IsKnockedOut) { ToggleDrag(); return; }
            Vector3 target = transform.position - transform.forward * dragDistance;
            Vector3 cur = Dragging.transform.position;
            Vector3 next = Vector3.Lerp(cur, target, 1f - Mathf.Exp(-6f * Time.deltaTime));
            // 바닥 높이에 맞춘다
            if (Physics.Raycast(next + Vector3.up * 1f, Vector3.down, out RaycastHit hit, 3f, ~0, QueryTriggerInteraction.Ignore) && !hit.collider.transform.IsChildOf(transform))
                next.y = hit.point.y;
            Dragging.transform.position = next;
            Vector3 away = next - transform.position;
            away.y = 0f;
            if (away.sqrMagnitude > 0.01f)
                Dragging.transform.rotation = Quaternion.Slerp(Dragging.transform.rotation, Quaternion.LookRotation(-away.normalized, Vector3.up), 1f - Mathf.Exp(-4f * Time.deltaTime));
        }
    }
}
