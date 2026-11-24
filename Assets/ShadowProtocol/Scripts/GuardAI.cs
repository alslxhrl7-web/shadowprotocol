using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace ShadowProtocol
{
    public enum GuardState { Patrol, Suspicious, Alert, Search }

    /// <summary>
    /// 경비병 AI. Patrol -> Suspicious -> Alert(Chase) -> Search -> Patrol.
    /// 시야(각도·거리·Raycast 가림)와 청각(소음 반경)으로 플레이어를 감지한다.
    /// 기절(등 뒤 제압), 기절한 동료 발견 시 경보, 적응형 지휘(GuardDirector)의 순찰 변경을 지원한다.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class GuardAI : MonoBehaviour
    {
        public static readonly List<GuardAI> All = new List<GuardAI>();
        /// <summary>(경비, 이전 상태, 새 상태)</summary>
        public static event Action<GuardAI, GuardState, GuardState> StateChanged;
        public static event Action<GuardAI> KnockedOutEvent;
        /// <summary>(발견한 경비, 기절한 경비)</summary>
        public static event Action<GuardAI, GuardAI> BodyFound;

        [Header("References")]
        public PlayerController player;
        [Tooltip("무전 자막에 쓰는 호출명")]
        public string callSign = "경비";

        [Header("Patrol")]
        public Transform[] patrolPoints;
        public float waitAtPoint = 1.5f;
        public float patrolSpeed = 1.5f;
        [Tooltip("순찰 지점별 대기 시간 배율(적응형 지휘가 자주 쓰는 구역 쪽을 늘린다)")]
        public float[] pointWaitScale;

        [Header("Post (고정 감시형)")]
        [Tooltip("켜면 첫 순찰 지점(초소)에 서서, 그 지점 회전 방향을 중심으로 좌우를 둘러본다")]
        public bool stationary;
        public float scanAngle = 55f;
        public float scanSpeed = 0.6f;

        [Header("Light")]
        [Tooltip("플레이어 위치 밝기 0→1일 때 시야 거리 배율")]
        public float darkRangeFactor = 0.6f;
        public float brightRangeFactor = 1.15f;

        [Header("Speeds")]
        public float suspiciousSpeed = 2f;
        public float chaseSpeed = 4.2f;
        public float searchSpeed = 2.4f;

        [Header("Vision")]
        public float viewDistance = 10f;
        [Range(10f, 180f)] public float viewAngle = 90f;
        public float eyeHeight = 1.6f;
        [Tooltip("이 거리 안이면 등 뒤라도 기척을 느낀다")]
        public float proximityRadius = 1.6f;
        public LayerMask sightMask = ~0;

        [Header("Suspicion")]
        public float suspicionRiseRate = 1f;
        public float suspicionDecayRate = 0.2f;
        [Range(0f, 1f)] public float suspiciousThreshold = 0.25f;

        [Header("Chase / Search")]
        public float loseSightTime = 2.5f;
        public float searchDuration = 10f;
        public float searchRadius = 6f;
        public float catchDistance = 1.2f;
        [Tooltip("경보(!) 진입 후 이 시간이 지나야 체포할 수 있다. 바로 옆에서 들켜도 도망칠 틈을 준다.")]
        public float alertReactionTime = 0.6f;
        [Tooltip("무전으로 마지막 목격 위치를 공유하는 반경")]
        public float alertShareRadius = 22f;

        [Header("Hearing")]
        public float hearingMultiplier = 1f;
        [Range(0f, 1f)] public float wallMuffle = 0.55f;

        public bool IsKnockedOut { get; private set; }
        public float KnockedOutTime { get; private set; }
        /// <summary>기절 판정 후 쓰러지는 모습을 늦추는 시간(암살 연출에서 타격 순간에 맞춤)</summary>
        [System.NonSerialized] public float FallDelay;
        /// <summary>쓰러지는 애니메이션을 보여 줄 때인가</summary>
        public bool ShowDown => IsKnockedOut && Time.time >= KnockedOutTime + FallDelay;
        /// <summary>기절한 위치(무전이 끊긴 곳). 동료는 여기로 확인하러 온다.</summary>
        public Vector3 KnockedOutPosition { get; private set; }
        /// <summary>다른 경비가 이 기절체를 발견했다</summary>
        public bool BodyDiscovered { get; set; }
        public bool BeingDragged { get; set; }
        public GuardState State { get; private set; } = GuardState.Patrol;
        public float Suspicion { get; private set; }
        public bool CanSeePlayer { get; private set; }
        public Vector3 LastKnownPosition { get; private set; }
        /// <summary>멈춰서 두리번거리는 중(수색·의심 조사)</summary>
        public bool IsLookingAround { get; private set; }
        public float EffectiveViewDistance => viewDistance * (State == GuardState.Alert ? 1.3f : 1f);
        public Vector3 EyePosition => transform.position + Vector3.up * eyeHeight;
        public int PatrolIndex => patrolIndex;
        public float WaitTimer => waitTimer;

        NavMeshAgent agent;
        NavMeshPath scratchPath;
        int patrolIndex;
        float waitTimer;
        float timeSinceSeen = 999f;
        float searchTimer;
        float lookTimer;
        float baseYaw;
        bool hasLookBase;
        bool investigating;
        float repathTimer;
        float alertTime;
        float homeYaw;
        float scanPhase;
        float bodyCheckTimer;
        bool frozen;
        Vector3? priorityPoint;

        public static Color ColorFor(GuardState s)
        {
            switch (s)
            {
                case GuardState.Suspicious: return new Color(1f, 0.85f, 0.2f);
                case GuardState.Search: return new Color(1f, 0.55f, 0.15f);
                case GuardState.Alert: return new Color(1f, 0.2f, 0.2f);
                default: return new Color(0.35f, 0.95f, 0.45f);
            }
        }

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            scratchPath = new NavMeshPath();
        }

        void OnEnable()
        {
            All.Add(this);
            NoiseSystem.OnNoise += HandleNoise;
        }

        void OnDisable()
        {
            All.Remove(this);
            NoiseSystem.OnNoise -= HandleNoise;
        }

        void Start()
        {
            if (player == null) player = FindAnyObjectByType<PlayerController>();
            // 에이전트는 꺼진 채 저장된다(NavBootstrap 설명 참고). 모든 OnEnable 이후인 여기서 NavMesh를 확인하고 켠다.
            if (!NavBootstrap.EnableAgent(agent)) Debug.LogWarning($"[ShadowProtocol] {name}: NavMesh에 올리지 못함");
            LastKnownPosition = transform.position;
            patrolIndex = 0;
            RefreshHome();
            scanPhase = Random.value * Mathf.PI * 2f;
            bodyCheckTimer = Random.value * 0.2f;
            EnterState(GuardState.Patrol);
        }

        void RefreshHome()
        {
            homeYaw = (patrolPoints != null && patrolPoints.Length > 0 && patrolPoints[0] != null) ? patrolPoints[0].eulerAngles.y : transform.eulerAngles.y;
        }

        void Update()
        {
            if (IsKnockedOut) return;
            if (agent == null || !agent.isOnNavMesh) return;
            if (GameManager.IsOver || !GameManager.GameplayActive)
            {
                agent.isStopped = true;
                frozen = true;
                CanSeePlayer = false;
                return;
            }
            if (frozen)
            {
                frozen = false;
                agent.isStopped = false;
            }

            float dt = Time.deltaTime;
            repathTimer -= dt;

            float sight = EvaluateSight();
            CanSeePlayer = sight > 0f;

            if (CanSeePlayer)
            {
                LastKnownPosition = player.transform.position;
                timeSinceSeen = 0f;
                float mult = State == GuardState.Search ? 1.75f : (State == GuardState.Suspicious ? 1.25f : 1f);
                Suspicion += suspicionRiseRate * sight * mult * dt;
            }
            else
            {
                timeSinceSeen += dt;
                if (ShouldDecay()) Suspicion -= suspicionDecayRate * dt;
            }
            Suspicion = Mathf.Clamp01(Suspicion);

            bodyCheckTimer -= dt;
            if (bodyCheckTimer <= 0f)
            {
                bodyCheckTimer = 0.2f;
                CheckForBodies();
            }

            IsLookingAround = false;
            switch (State)
            {
                case GuardState.Patrol: UpdatePatrol(dt); break;
                case GuardState.Suspicious: UpdateSuspicious(dt); break;
                case GuardState.Alert: UpdateAlert(dt); break;
                case GuardState.Search: UpdateSearch(dt); break;
            }
        }

        bool ShouldDecay()
        {
            switch (State)
            {
                case GuardState.Patrol: return true;
                case GuardState.Suspicious: return investigating;
                default: return false;
            }
        }

        // ---------------- States ----------------

        void EnterState(GuardState next)
        {
            var prev = State;
            State = next;
            waitTimer = 0f;
            hasLookBase = false;
            investigating = false;
            repathTimer = 0f;
            if (agent.isOnNavMesh) agent.isStopped = false;

            switch (next)
            {
                case GuardState.Patrol:
                    Suspicion = 0f;
                    agent.speed = patrolSpeed;
                    if (patrolPoints != null && patrolPoints.Length > 0)
                    {
                        patrolIndex = NearestPatrolIndex();
                        SetDest(patrolPoints[patrolIndex].position);
                    }
                    break;
                case GuardState.Suspicious:
                    agent.speed = suspiciousSpeed;
                    SetDest(LastKnownPosition);
                    break;
                case GuardState.Alert:
                    Suspicion = 1f;
                    agent.speed = chaseSpeed;
                    alertTime = 0f;
                    ShareAlert();
                    break;
                case GuardState.Search:
                    Suspicion = Mathf.Max(Suspicion, 0.6f);
                    agent.speed = searchSpeed;
                    searchTimer = searchDuration;
                    // 플레이어가 자주 숨던 엄폐물부터 확인(적응형 지휘)
                    priorityPoint = GuardDirector.Instance != null ? GuardDirector.Instance.PreferredSearchPoint(LastKnownPosition, this) : null;
                    SetDest(priorityPoint ?? LastKnownPosition);
                    break;
            }
            if (prev != next) StateChanged?.Invoke(this, prev, next);
        }

        void UpdatePatrol(float dt)
        {
            agent.speed = patrolSpeed;
            if (Suspicion >= 1f) { EnterState(GuardState.Alert); return; }
            if (Suspicion >= suspiciousThreshold) { EnterState(GuardState.Suspicious); return; }
            if (patrolPoints == null || patrolPoints.Length == 0) return;

            if (stationary)
            {
                // 초소에서 좌우로 천천히 둘러본다
                if (Arrived())
                {
                    float y = homeYaw + Mathf.Sin(Time.time * scanSpeed + scanPhase) * scanAngle;
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0f, y, 0f), 1f - Mathf.Exp(-3f * dt));
                }
                return;
            }

            if (Arrived())
            {
                waitTimer += dt;
                LookAround(dt, 35f);
                float scale = (pointWaitScale != null && patrolIndex < pointWaitScale.Length) ? pointWaitScale[patrolIndex] : 1f;
                if (waitTimer >= waitAtPoint * scale)
                {
                    waitTimer = 0f;
                    hasLookBase = false;
                    patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
                    SetDest(patrolPoints[patrolIndex].position);
                }
            }
        }

        void UpdateSuspicious(float dt)
        {
            agent.speed = suspiciousSpeed;
            if (Suspicion >= 1f) { EnterState(GuardState.Alert); return; }

            if (CanSeePlayer)
            {
                investigating = false;
                hasLookBase = false;
                SetDestThrottled(LastKnownPosition);
                FaceTowards(player.transform.position, dt);
            }
            else if (!investigating && Arrived())
            {
                investigating = true;
                hasLookBase = false;
            }

            if (investigating)
            {
                IsLookingAround = true;
                LookAround(dt, 80f);
                if (Suspicion <= 0f) EnterState(GuardState.Patrol);
            }
        }

        void UpdateAlert(float dt)
        {
            agent.speed = chaseSpeed;
            Suspicion = 1f;
            alertTime += dt;

            // 시야를 잃은 직후 잠깐은 플레이어의 움직임을 직감으로 따라간다.
            if (!CanSeePlayer && timeSinceSeen < 0.75f && player != null &&
                (player.transform.position - transform.position).sqrMagnitude < 64f)
                LastKnownPosition = player.transform.position;

            SetDestThrottled(LastKnownPosition);

            if (player != null)
            {
                Vector3 d = player.transform.position - transform.position;
                float vertical = Mathf.Abs(d.y);
                d.y = 0f;
                if (alertTime >= alertReactionTime && d.magnitude <= catchDistance && vertical < 1.5f)
                {
                    GameManager.Instance?.PlayerCaught(this);
                    return;
                }
            }

            if (!CanSeePlayer && timeSinceSeen >= loseSightTime &&
                (Arrived() || timeSinceSeen > loseSightTime + 4f))
            {
                EnterState(GuardState.Search);
            }
        }

        void UpdateSearch(float dt)
        {
            agent.speed = searchSpeed;
            if (Suspicion >= 1f) { EnterState(GuardState.Alert); return; }

            if (CanSeePlayer)
            {
                SetDestThrottled(LastKnownPosition);
                FaceTowards(player.transform.position, dt);
                waitTimer = 0f;
            }

            searchTimer -= dt;

            if (Arrived())
            {
                waitTimer += dt;
                IsLookingAround = true;
                LookAround(dt, 60f);
                if (waitTimer >= (priorityPoint.HasValue ? 2.2f : 1.2f))
                {
                    waitTimer = 0f;
                    hasLookBase = false;
                    priorityPoint = null;
                    SetDest(RandomSearchPoint());
                }
            }

            if (searchTimer <= 0f && !CanSeePlayer)
                EnterState(GuardState.Patrol);
        }

        // ---------------- Perception ----------------

        /// <summary>0이면 안 보임. 0보다 크면 의심 게이지 상승 배율.</summary>
        float EvaluateSight()
        {
            if (player == null) return 0f;

            Vector3 eye = EyePosition;
            Vector3 toPlayer = player.CenterPoint - eye;
            Vector3 flat = new Vector3(toPlayer.x, 0f, toPlayer.z);
            float dist = flat.magnitude;

            float light = Mathf.Lerp(darkRangeFactor, brightRangeFactor, Mathf.Clamp01(player.Brightness));
            float range = EffectiveViewDistance * player.VisibilityRangeFactor * light;
            if (dist > range) return 0f;

            float halfAngle = viewAngle * 0.5f;
            float angle = dist > 0.001f ? Vector3.Angle(transform.forward, flat) : 0f;
            bool inCone = angle <= halfAngle;
            bool felt = dist <= proximityRadius * (player.IsCrouching ? 0.6f : 1f);
            if (!inCone && !felt) return 0f;

            if (!HasLineOfSight(eye, player.HeadPoint) && !HasLineOfSight(eye, player.CenterPoint))
                return 0f;

            float distFactor = Mathf.Lerp(2.5f, 0.35f, dist / range);
            float angleFactor = inCone ? Mathf.Lerp(1f, 0.55f, angle / halfAngle) : 0.6f;
            return distFactor * angleFactor * player.VisibilityExposure;
        }

        bool HasLineOfSight(Vector3 eye, Vector3 target)
        {
            Vector3 dir = target - eye;
            float len = dir.magnitude;
            if (len < 0.01f) return true;
            if (Physics.Raycast(eye, dir / len, out RaycastHit hit, len + 0.3f, sightMask, QueryTriggerInteraction.Ignore))
                return hit.collider.transform.IsChildOf(player.transform);
            return true;
        }

        /// <summary>시야 안에 기절한 동료가 보이면 경보</summary>
        void CheckForBodies()
        {
            if (State == GuardState.Alert) return;
            foreach (var other in All)
            {
                if (other == this || !other.IsKnockedOut || other.BodyDiscovered || other.BeingDragged) continue;
                Vector3 body = other.transform.position + Vector3.up * 0.35f;
                Vector3 eye = EyePosition;
                Vector3 to = body - eye;
                Vector3 flat = new Vector3(to.x, 0f, to.z);
                float dist = flat.magnitude;
                float light = Mathf.Lerp(darkRangeFactor, brightRangeFactor, Mathf.Clamp01(LightZone.BrightnessAt(body)));
                if (dist > viewDistance * 0.85f * light) continue;
                bool near = dist < 2f;
                if (!near && Vector3.Angle(transform.forward, flat) > viewAngle * 0.5f) continue;
                // 몸이 엄폐물 뒤(낮은 위치)에 있으면 가려진다
                if (Physics.Raycast(eye, to.normalized, out RaycastHit hit, to.magnitude - 0.2f, sightMask, QueryTriggerInteraction.Ignore))
                {
                    if (!hit.collider.transform.IsChildOf(other.transform)) continue;
                }
                DiscoverBody(other);
                return;
            }
        }

        void DiscoverBody(GuardAI body)
        {
            body.BodyDiscovered = true;
            LastKnownPosition = body.transform.position;
            Suspicion = Mathf.Max(Suspicion, 0.6f);
            EnterState(GuardState.Search);
            searchTimer = searchDuration * 1.6f;
            BodyFound?.Invoke(this, body);
        }

        /// <summary>경보 발령(기절체 발견 등): 주변 경비가 그 위치를 수색하러 온다</summary>
        public void ReceiveAlarm(Vector3 position)
        {
            if (IsKnockedOut || State == GuardState.Alert) return;
            LastKnownPosition = position;
            Suspicion = Mathf.Max(Suspicion, 0.6f);
            if (State != GuardState.Search) EnterState(GuardState.Search);
            else
            {
                searchTimer = Mathf.Max(searchTimer, searchDuration);
                SetDest(position);
            }
        }

        /// <summary>지휘 명령: 특정 지점을 확인하러 간다(무전 응답 없는 동료 확인 등)</summary>
        public void Investigate(Vector3 position)
        {
            if (IsKnockedOut || State == GuardState.Alert || State == GuardState.Search) return;
            LastKnownPosition = position;
            Suspicion = Mathf.Max(Suspicion, suspiciousThreshold + 0.25f);
            if (State == GuardState.Suspicious) { investigating = false; hasLookBase = false; SetDest(position); }
            else EnterState(GuardState.Suspicious);
        }

        void HandleNoise(Vector3 pos, float radius, GameObject source)
        {
            if (IsKnockedOut || GameManager.IsOver || !GameManager.GameplayActive || agent == null || !agent.isOnNavMesh) return;

            float r = radius * hearingMultiplier;
            Vector3 d = pos - transform.position;
            d.y = 0f;
            float dist = d.magnitude;
            if (dist > r) return;

            // 벽 너머 소리는 약하게 들린다.
            if (Physics.Linecast(EyePosition, pos + Vector3.up, out RaycastHit hit, sightMask, QueryTriggerInteraction.Ignore))
            {
                bool hitSource = source != null && hit.collider.transform.IsChildOf(source.transform);
                if (!hitSource)
                {
                    r *= wallMuffle;
                    if (dist > r) return;
                }
            }

            LastKnownPosition = pos;
            switch (State)
            {
                case GuardState.Patrol:
                    Suspicion = Mathf.Max(Suspicion, suspiciousThreshold + 0.1f);
                    EnterState(GuardState.Suspicious);
                    break;
                case GuardState.Suspicious:
                    Suspicion = Mathf.Min(1f, Suspicion + 0.06f);
                    investigating = false;
                    hasLookBase = false;
                    SetDest(pos);
                    break;
                case GuardState.Search:
                    searchTimer = Mathf.Max(searchTimer, searchDuration * 0.6f);
                    Suspicion = Mathf.Min(1f, Suspicion + 0.05f);
                    waitTimer = 0f;
                    priorityPoint = null;
                    SetDest(pos);
                    break;
                case GuardState.Alert:
                    break;
            }
        }

        void ShareAlert()
        {
            // 무전으로 마지막 목격 위치 공유(반경 안의 깨어 있는 경비)
            var heard = new List<GuardAI>();
            foreach (var g in All)
            {
                if (g == this || g.IsKnockedOut || g.State == GuardState.Alert) continue;
                if (Vector3.Distance(g.transform.position, transform.position) > alertShareRadius) continue;
                heard.Add(g);
            }
            foreach (var g in heard) g.ReceiveAlert(LastKnownPosition);
            if (GuardDirector.Instance != null) GuardDirector.Instance.OnAlertShared(this, LastKnownPosition, heard.Count);
        }

        public void ReceiveAlert(Vector3 position)
        {
            if (IsKnockedOut) return;
            LastKnownPosition = position;
            Suspicion = Mathf.Max(Suspicion, 0.7f);
            if (State == GuardState.Suspicious)
            {
                investigating = false;
                SetDest(position);
            }
            else if (State != GuardState.Alert)
            {
                EnterState(GuardState.Suspicious);
            }
        }

        // ---------------- Takedown ----------------

        /// <summary>플레이어가 등 뒤에서 제압할 수 있는가(못 본 상태, 등 뒤 110° 밖, 1.6m 이내)</summary>
        public bool CanBeTakenDownBy(PlayerController p)
        {
            if (IsKnockedOut || p == null || State == GuardState.Alert || CanSeePlayer) return false;
            Vector3 d = p.transform.position - transform.position;
            if (Mathf.Abs(d.y) > 0.8f) return false;
            d.y = 0f;
            if (d.magnitude > 1.6f) return false;
            return Vector3.Angle(transform.forward, d) > 110f;
        }

        public void KnockOut()
        {
            if (IsKnockedOut) return;
            var prev = State;
            IsKnockedOut = true;
            KnockedOutTime = Time.time;
            KnockedOutPosition = transform.position;
            Suspicion = 0f;
            CanSeePlayer = false;
            IsLookingAround = false;
            if (agent.isOnNavMesh) { agent.isStopped = true; agent.ResetPath(); }
            agent.enabled = false;
            foreach (var c in GetComponents<Collider>()) c.isTrigger = true;
            State = GuardState.Patrol;
            if (prev != GuardState.Patrol) StateChanged?.Invoke(this, prev, GuardState.Patrol);
            KnockedOutEvent?.Invoke(this);
        }

        // ---------------- Director ----------------

        /// <summary>적응형 지휘: 순찰 경로를 바꾼다(초소형이면 stationary)</summary>
        public void SetPatrolRoute(Transform[] points, bool post, float scan)
        {
            patrolPoints = points;
            stationary = post;
            scanAngle = scan;
            pointWaitScale = null;
            patrolIndex = 0;
            RefreshHome();
            if (!IsKnockedOut && State == GuardState.Patrol && agent != null && agent.isOnNavMesh) EnterState(GuardState.Patrol);
        }

        // ---------------- Helpers ----------------

        void SetDest(Vector3 p)
        {
            if (agent.isOnNavMesh) agent.SetDestination(p);
        }

        void SetDestThrottled(Vector3 p)
        {
            if (repathTimer > 0f) return;
            repathTimer = 0.15f;
            SetDest(p);
        }

        bool Arrived()
        {
            if (agent.pathPending) return false;
            return agent.remainingDistance <= agent.stoppingDistance + 0.15f;
        }

        void LookAround(float dt, float amplitude)
        {
            if (!hasLookBase)
            {
                baseYaw = transform.eulerAngles.y;
                lookTimer = 0f;
                hasLookBase = true;
            }
            lookTimer += dt;
            float yaw = baseYaw + Mathf.Sin(lookTimer * 1.6f) * amplitude;
            Quaternion target = Quaternion.Euler(0f, yaw, 0f);
            transform.rotation = Quaternion.Slerp(transform.rotation, target, 1f - Mathf.Exp(-6f * dt));
        }

        void FaceTowards(Vector3 worldPos, float dt)
        {
            if (agent.velocity.sqrMagnitude > 0.2f) return;
            Vector3 d = worldPos - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude < 0.01f) return;
            Quaternion target = Quaternion.LookRotation(d, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, target, 1f - Mathf.Exp(-8f * dt));
        }

        int NearestPatrolIndex()
        {
            int best = 0;
            float bestDist = float.MaxValue;
            for (int i = 0; i < patrolPoints.Length; i++)
            {
                if (patrolPoints[i] == null) continue;
                float d = (patrolPoints[i].position - transform.position).sqrMagnitude;
                if (d < bestDist) { bestDist = d; best = i; }
            }
            return best;
        }

        Vector3 RandomSearchPoint()
        {
            for (int i = 0; i < 10; i++)
            {
                Vector2 c = Random.insideUnitCircle * searchRadius;
                Vector3 p = LastKnownPosition + new Vector3(c.x, 0f, c.y);
                if (!NavMesh.SamplePosition(p, out NavMeshHit hit, 1.2f, NavMesh.AllAreas)) continue;
                if (Mathf.Abs(hit.position.y - transform.position.y) > 0.4f) continue; // 상자 위 고립 영역 제외
                if (!agent.CalculatePath(hit.position, scratchPath) || scratchPath.status != NavMeshPathStatus.PathComplete) continue;
                return hit.position;
            }
            return LastKnownPosition;
        }

        void OnDrawGizmosSelected()
        {
            Vector3 eye = transform.position + Vector3.up * eyeHeight;
            Gizmos.color = Application.isPlaying ? ColorFor(State) : Color.yellow;
            float half = viewAngle * 0.5f;
            Vector3 l = Quaternion.Euler(0f, -half, 0f) * transform.forward * viewDistance;
            Vector3 r = Quaternion.Euler(0f, half, 0f) * transform.forward * viewDistance;
            Gizmos.DrawLine(eye, eye + l);
            Gizmos.DrawLine(eye, eye + r);
            Vector3 prev = eye + l;
            for (int i = 1; i <= 16; i++)
            {
                Vector3 next = eye + Quaternion.Euler(0f, -half + viewAngle * i / 16f, 0f) * transform.forward * viewDistance;
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
            Gizmos.DrawWireSphere(transform.position, proximityRadius);

            if (patrolPoints != null && patrolPoints.Length > 1)
            {
                Gizmos.color = Color.cyan;
                for (int i = 0; i < patrolPoints.Length; i++)
                {
                    var a = patrolPoints[i];
                    var b = patrolPoints[(i + 1) % patrolPoints.Length];
                    if (a != null && b != null) Gizmos.DrawLine(a.position + Vector3.up * 0.1f, b.position + Vector3.up * 0.1f);
                }
            }

            if (Application.isPlaying && State != GuardState.Patrol)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(LastKnownPosition, 0.4f);
            }
        }
    }
}
