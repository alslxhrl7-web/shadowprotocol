using UnityEngine;

namespace ShadowProtocol
{
    /// <summary>
    /// 뛰어넘기·슬라이딩(2026-11-23 사용자 지시: 액션 요소). Space 하나로 상황에 맞게:
    /// - 앞 1.1m 안에 높이 0.45~1.25m, 깊이 1.8m 이하의 장애물이 있고 넘어간 자리가 비어 있으면 → 뛰어넘기(한 손 짚고 옆으로 넘기)
    /// - 장애물이 없고 달리는 중이면 → 슬라이딩(0.8초, 웅크린 키로 낮은 틈 통과, 속도가 점점 줄어듦)
    /// 전용 애니메이션 클립이 없어 웅크림 다리 + 절차적 상체(CharacterProceduralPose.vaultWeight/slideWeight)로 표현한다.
    /// 클립이 생기면 PlayVault/PlaySlide에서 Animator 트리거로 바꾸면 된다.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class ParkourController : MonoBehaviour
    {
        [Header("뛰어넘기")]
        public float vaultCheckDistance = 1.1f;
        public float minVaultHeight = 0.45f;
        public float maxVaultHeight = 1.25f;
        public float maxVaultDepth = 1.8f;
        public float vaultTimeRun = 0.5f;
        public float vaultTimeWalk = 0.65f;
        public float vaultNoise = 5f;
        [Tooltip("달리는 중이면 Space 없이 자동으로 뛰어넘기(AC2·AC3 프리런)")]
        public bool autoVaultWhenRunning = true;
        /// <summary>시험용: 가상 입력(봇·측정)에서도 자동 뛰어넘기. 봇 경로가 바뀌지 않게 기본은 끔.</summary>
        public static bool AutoVaultForVirtualInput;

        [Header("슬라이딩")]
        public float slideTime = 0.8f;
        public float slideMinSpeed = 4.5f;
        public float slideEndSpeed = 1.3f;
        public float slideNoise = 6f;
        public float slideCooldown = 0.4f;

        [Header("Mixamo 클립(있으면 클립으로, 없으면 절차적 자세로)")]
        [Tooltip("ShadowProtocolMixamoBake가 만든 이동 곡선(클립 골반의 전진 거리·높이)")]
        public ParkourMotionData motion;

        /// <summary>장애물 반대편 끝에서 착지점까지(m)</summary>
        const float LandBeyond = 0.8f;

        public bool Busy { get; private set; }
        public string Prompt { get; private set; }

        PlayerController player;
        CharacterController cc;
        CharacterProceduralPose pose;
        PlayerTools tools;
        Animator anim;
        bool hasVaultClip, hasSlideClip;
        float cooldown;
        static readonly int HVault = Animator.StringToHash("Vault");
        static readonly int HSlide = Animator.StringToHash("Slide");
        static readonly int HParkourSpeed = Animator.StringToHash("ParkourSpeed");

        void Awake()
        {
            player = GetComponent<PlayerController>();
            cc = GetComponent<CharacterController>();
            tools = GetComponent<PlayerTools>();
        }

        void Start()
        {
            pose = GetComponentInChildren<CharacterProceduralPose>();
            var drv = GetComponentInChildren<CharacterAnimDriver>();
            anim = drv != null ? drv.Animator : GetComponentInChildren<Animator>();
            if (anim != null && anim.runtimeAnimatorController != null && motion != null)
                foreach (var p in anim.parameters)
                {
                    if (p.nameHash == HVault) hasVaultClip = motion.vaultLength > 0f;
                    if (p.nameHash == HSlide) hasSlideClip = motion.slideLength > 0f;
                }
        }

        void Update()
        {
            cooldown -= Time.deltaTime;
            Prompt = null;
            if (Busy || !player.InputEnabled || !GameManager.GameplayActive) return;
            if (tools != null && (tools.InTakedown || tools.Dragging != null || tools.Aiming)) return;

            bool moving = player.PlanarVelocity.sqrMagnitude > 0.5f;
            Vector3 top = default, land = default;
            bool canVault = moving && FindVault(out top, out land, out _);
            if (canVault) Prompt = player.IsRunning && autoVaultWhenRunning ? null : "[Space] 뛰어넘기";
            // AC2·AC3식 프리런: 달리는 중(Shift)이면 낮은 장애물을 버튼 없이 넘는다
            if (canVault && autoVaultWhenRunning && player.IsRunning && (!player.UseVirtualInput || AutoVaultForVirtualInput) && cooldown <= 0f)
            {
                StartCoroutine(Vault(top, land));
                return;
            }

            if (player.UseVirtualInput || !Input.GetKeyDown(KeyCode.Space)) return;
            TryAction();
        }

        /// <summary>Space와 같은 동작(봇·시험용). 실행했으면 true.</summary>
        public bool TryAction()
        {
            if (Busy || cooldown > 0f || !player.InputEnabled) return false;
            if (player.PlanarVelocity.sqrMagnitude > 0.5f && FindVault(out Vector3 top, out Vector3 land, out _)) { StartCoroutine(Vault(top, land)); return true; }
            if (player.IsRunning) { StartCoroutine(Slide()); return true; }
            return false;
        }

        // ---------------------------------------------------------------- 판정

        static bool IsCharacter(Collider c) => c.GetComponentInParent<GuardAI>() != null || c.GetComponentInParent<PlayerController>() != null;

        public bool FindVault(out Vector3 top, out Vector3 land, out float height)
        {
            top = land = Vector3.zero; height = 0f;
            Vector3 pos = transform.position;
            Vector3 fwd = player.PlanarVelocity.sqrMagnitude > 0.25f ? player.PlanarVelocity.normalized : transform.forward;
            fwd.y = 0f; fwd.Normalize();
            Vector3 up = Vector3.up;

            if (!Physics.SphereCast(pos + up * 0.45f, 0.2f, fwd, out RaycastHit hit, vaultCheckDistance, ~0, QueryTriggerInteraction.Ignore)) return false;
            if (IsCharacter(hit.collider)) return false;
            // 장애물 윗면 높이
            Vector3 probe = hit.point + fwd * 0.12f; probe.y = pos.y + 2.0f;
            if (!Physics.Raycast(probe, Vector3.down, out RaycastHit topHit, 2.0f, ~0, QueryTriggerInteraction.Ignore)) return false;
            height = topHit.point.y - pos.y;
            if (height < minVaultHeight || height > maxVaultHeight) return false;
            // 넘어가는 길 위가 비어 있어야(상인방·선반 아래는 안 됨)
            if (Physics.CheckSphere(topHit.point + up * 0.7f, 0.3f, ~0, QueryTriggerInteraction.Ignore)) return false;
            // 반대편 바닥 찾기
            for (float d = 0.2f; d <= maxVaultDepth; d += 0.1f)
            {
                Vector3 q = hit.point + fwd * d; q.y = pos.y + 2.0f;
                if (!Physics.Raycast(q, Vector3.down, out RaycastHit g, 2.6f, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (g.point.y > pos.y + 0.3f) continue; // 아직 장애물 위
                land = g.point + fwd * LandBeyond; // 클립은 최고점 뒤로 약 1.5m 더 나아간다 → 넉넉히
                land.y = g.point.y;
                // 착지 자리·넘어가는 경로가 비어 있는지
                float r = cc != null ? cc.radius : 0.35f;
                if (Physics.CheckCapsule(land + up * (r + 0.1f), land + up * 1.5f, r * 0.9f, ~0, QueryTriggerInteraction.Ignore)) return false;
                Vector3 over = topHit.point + up * (0.45f + r);
                if (Physics.SphereCast(over - fwd * 0.6f, r * 0.8f, fwd, out _, d + 0.6f, ~0, QueryTriggerInteraction.Ignore)) return false;
                top = topHit.point;
                return true;
            }
            return false;
        }

        // ---------------------------------------------------------------- 동작

        /// <summary>
        /// 클립 뛰어넘기: Mixamo 클립의 전진 거리 곡선대로 몸을 옮긴다(발이 미끄러지지 않음). 거리 차이는 비율로 맞추고,
        /// 장애물이 클립 상자(약 1.0m)보다 높으면 골반 높이 곡선 모양으로 그만큼 더 들어 올린다.
        /// </summary>
        System.Collections.IEnumerator VaultClip(Vector3 top, Vector3 land)
        {
            Busy = true;
            player.MotionLocked = true;
            if (pose != null) pose.overlaySuppressed = true;
            bool ccWas = cc.enabled;
            cc.enabled = false;
            Vector3 start = transform.position;
            Vector3 dir = land - start; dir.y = 0f;
            float dist = dir.magnitude;
            dir = dist > 0.001f ? dir / dist : transform.forward;
            transform.rotation = Quaternion.LookRotation(dir, Vector3.up);

            float a = ShadowProtocolParkourTiming.VaultStartSec, b = ShadowProtocolParkourTiming.VaultEndSec;
            float clipDist = Mathf.Max(0.5f, motion.VaultDistance(a, b));
            // 클립 최고점(상자 위)이 장애물 가운데 위에 오게: 최고점 전·후 전진 비율을 따로
            Vector3 far = land - dir * LandBeyond;
            float dApex = Mathf.Clamp(Vector3.Dot((top + far) * 0.5f - start, dir), 0.3f, dist - 0.3f);
            float cApex = Mathf.Max(0.2f, motion.VaultDistance(a, motion.vaultApex));
            float kPre = dApex / cApex, kPost = (dist - dApex) / Mathf.Max(0.2f, clipDist - cApex);
            float rate = Mathf.Clamp(player.PlanarVelocity.magnitude / 3.0f, 0.9f, 1.25f); // 클립 도움닫기 약 3 m/s 기준
            float extraH = Mathf.Max(0f, (top.y - start.y) - ShadowProtocolParkourTiming.VaultClipObstacleHeight);
            float hip0 = motion.vaultHipHeight.Evaluate(a), hipTop = motion.vaultHipHeight.Evaluate(motion.vaultApex);
            float f0 = motion.vaultForward.Evaluate(a);

            anim.SetFloat(HParkourSpeed, rate);
            anim.SetTrigger(HVault);
            var am = AudioManager.Instance;
            float ct = a;
            while (ct < b)
            {
                ct = Mathf.Min(b, ct + Time.deltaTime * rate);
                float raw = motion.vaultForward.Evaluate(ct) - f0;
                float fwd = raw <= cApex ? raw * kPre : dApex + (raw - cApex) * kPost;
                float bell = Mathf.Clamp01((motion.vaultHipHeight.Evaluate(ct) - hip0) / Mathf.Max(0.05f, hipTop - hip0));
                if (ct > motion.vaultApex + 0.6f) bell = 0f;
                Vector3 p = start + dir * fwd + Vector3.up * (extraH * bell);
                p.y = Mathf.Lerp(start.y, land.y, Mathf.InverseLerp(a, b, ct)) + extraH * bell;
                transform.position = p;
                yield return null;
            }
            transform.position = new Vector3(transform.position.x, land.y, transform.position.z);
            cc.enabled = ccWas;
            NoiseSystem.Emit(transform.position, vaultNoise, gameObject);
            if (am != null) am.PlayAt(am.canLand, transform.position, 0.6f, 1f, 12f, 0.6f);
            ActionDirector.Instance.Impact(0.3f);
            if (pose != null) pose.overlaySuppressed = false;
            player.MotionLocked = false;
            cooldown = 0.2f;
            Busy = false;
        }

        /// <summary>클립 슬라이딩: Mixamo 클립의 전진 곡선(처음 약 5.2 m/s → 감속 → 일어나 다시 달림)대로 이동, 웅크린 키로 낮은 틈 통과</summary>
        System.Collections.IEnumerator SlideClip()
        {
            Busy = true;
            player.MotionLocked = true;
            player.ForceCrouch = true;
            if (pose != null) pose.overlaySuppressed = true;
            Vector3 dir = player.PlanarVelocity; dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) dir = transform.forward;
            dir.Normalize();
            transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            float a = ShadowProtocolParkourTiming.SlideStartSec, b = ShadowProtocolParkourTiming.SlideEndSec;
            float rate = Mathf.Clamp(player.PlanarVelocity.magnitude / 5.2f, 0.85f, 1.15f);
            NoiseSystem.Emit(transform.position, slideNoise, gameObject);
            var am = AudioManager.Instance;
            if (am != null) am.PlayAt(am.throwWhoosh, transform.position, 0.5f, 1f, 10f, 0.55f);
            ActionDirector.Instance.Impact(0.25f);
            anim.SetFloat(HParkourSpeed, rate);
            anim.SetTrigger(HSlide);

            float ct = a, prev = motion.slideForward.Evaluate(a), vy = -2f;
            bool blocked = false;
            while (ct < b)
            {
                float dt = Time.deltaTime;
                ct = Mathf.Min(b, ct + dt * rate);
                float f = motion.slideForward.Evaluate(ct);
                float step = f - prev; prev = f;
                vy = cc.isGrounded ? -2f : vy + player.gravity * dt;
                cc.Move(dir * step + Vector3.up * vy * dt);
                // 일어서는 구간 전까지 웅크린 키(낮은 틈 통과), 일어설 때 위가 막혀 있으면 PlayerController가 웅크린 채 둔다
                if (ct > b - 0.35f) player.ForceCrouch = false;
                Vector3 actual = cc.velocity; actual.y = 0f;
                if (dt > 0f && step / dt > 1f && actual.magnitude < step / dt * 0.3f) { blocked = true; break; }
                yield return null;
            }
            if (blocked) anim.CrossFade("Locomotion", 0.2f, 0);
            player.ForceCrouch = false;
            if (pose != null) pose.overlaySuppressed = false;
            player.MotionLocked = false;
            cooldown = slideCooldown;
            Busy = false;
        }

        System.Collections.IEnumerator Vault(Vector3 top, Vector3 land)
        {
            if (hasVaultClip) { yield return VaultClip(top, land); yield break; }
            Busy = true;
            player.MotionLocked = true;
            player.ForceCrouch = true;
            bool ccWas = cc.enabled;
            cc.enabled = false; // 장애물을 통과
            Vector3 start = transform.position;
            Vector3 dir = land - start; dir.y = 0f;
            Quaternion face = dir.sqrMagnitude > 0.001f ? Quaternion.LookRotation(dir.normalized, Vector3.up) : transform.rotation;
            float dur = player.PlanarVelocity.magnitude > player.walkSpeed * 1.2f ? vaultTimeRun : vaultTimeWalk;
            // 2차 베지어: 출발 → (장애물 위 0.55m) → 착지
            Vector3 ctrl = new Vector3((start.x + land.x) * 0.5f, top.y + 0.55f, (start.z + land.z) * 0.5f);
            var am = AudioManager.Instance;
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / dur);
                float u = Mathf.SmoothStep(0f, 1f, k * 0.85f + 0.15f * k * k);
                Vector3 a = Vector3.Lerp(start, ctrl, u), b = Vector3.Lerp(ctrl, land, u);
                transform.position = Vector3.Lerp(a, b, u);
                transform.rotation = Quaternion.Slerp(transform.rotation, face, 1f - Mathf.Exp(-14f * Time.deltaTime));
                if (pose != null) pose.vaultWeight = Mathf.Sin(k * Mathf.PI); // 넘을 때 가장 크게
                yield return null;
            }
            transform.position = land;
            if (pose != null) pose.vaultWeight = 0f;
            cc.enabled = ccWas;
            NoiseSystem.Emit(transform.position, vaultNoise, gameObject); // 착지 소리
            if (am != null) am.PlayAt(am.canLand, transform.position, 0.6f, 1f, 12f, 0.6f);
            ActionDirector.Instance.Impact(0.35f);
            player.ForceCrouch = false;
            player.MotionLocked = false;
            cooldown = 0.2f;
            Busy = false;
        }

        System.Collections.IEnumerator Slide()
        {
            if (hasSlideClip) { yield return SlideClip(); yield break; }
            Busy = true;
            player.MotionLocked = true;
            player.ForceCrouch = true;
            Vector3 dir = player.PlanarVelocity; dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) dir = transform.forward;
            dir.Normalize();
            float v0 = Mathf.Max(slideMinSpeed, player.PlanarVelocity.magnitude);
            NoiseSystem.Emit(transform.position, slideNoise, gameObject);
            var am = AudioManager.Instance;
            if (am != null) am.PlayAt(am.throwWhoosh, transform.position, 0.5f, 1f, 10f, 0.55f);
            ActionDirector.Instance.Impact(0.25f);
            float t = 0f, vy = -2f;
            while (t < slideTime)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / slideTime);
                float v = Mathf.Lerp(v0, slideEndSpeed, 1f - (1f - k) * (1f - k)); // 처음 빠르고 끝에 느려짐
                vy = cc.isGrounded ? -2f : vy + player.gravity * Time.deltaTime;
                cc.Move((dir * v + Vector3.up * vy) * Time.deltaTime);
                if (pose != null) pose.slideWeight = Mathf.Clamp01(Mathf.Min(k * 6f, (1f - k) * 4f));
                // 벽에 부딪히면 멈춤
                Vector3 actual = cc.velocity; actual.y = 0f;
                if (t > 0.1f && actual.magnitude < v * 0.3f) break;
                yield return null;
            }
            if (pose != null) pose.slideWeight = 0f;
            player.ForceCrouch = false; // 낮은 틈 아래면 PlayerController가 웅크린 채로 둔다
            player.MotionLocked = false;
            cooldown = slideCooldown;
            Busy = false;
        }
    }
}
