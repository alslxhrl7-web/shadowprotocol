using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace ShadowProtocol
{
    /// <summary>
    /// 적응형 경비 지휘(Director).
    /// - 플레이어 행동 기록: 위치 히트맵, 엄폐물 사용 시간, 경로(환기구/덕트/캣워크/정면/사무동) 사용 횟수 — 재시도·체크포인트에도 유지.
    /// - 재시도/체크포인트 시작 시와 한 판 안의 경보 후: 자주 쓰는 경로 출구에 감시 배치(한 번에 1명, 동시에 최대 2명 변경 → 5개 경로 중 최소 3개는 열림),
    ///   히트맵이 높은 순찰 지점의 대기 시간 증가, 수색 시 자주 숨던 엄폐물 우선.
    /// - 경보 시 무전으로 마지막 목격 위치 공유, 기절한 경비는 일정 시간 후 동료가 확인, 기절체 발견 시 경보.
    /// - 무전 자막(Radio 이벤트)으로 플레이어에게 알린다.
    /// </summary>
    [DefaultExecutionOrder(-60)]
    public class GuardDirector : MonoBehaviour
    {
        public static GuardDirector Instance { get; private set; }
        /// <summary>(자막, 표시 시간)</summary>
        public static event Action<string, float> Radio;

        [Serializable]
        public class Adaptation
        {
            public string routeId;
            public string routeLabel;
            public GuardAI guard;
            public Transform[] route;
            public bool post = true;
            public float scanAngle = 45f;
            [Tooltip("자막에 쓰는 동작 설명")]
            public string action = "감시하기 시작했다";
            [Tooltip("0이면 기본 useThreshold")]
            public int threshold;
        }

        public PlayerController player;
        public RouteMarker[] routes;
        [Tooltip("엄폐물(이름으로 기록)")]
        public Transform[] covers;
        public Adaptation[] adaptations;

        [Header("규칙")]
        [Tooltip("이 횟수 이상 쓴 경로에 감시를 붙인다")]
        public int useThreshold = 2;
        [Tooltip("동시에 바뀌어 있는 경비 수 상한(경로 5개 중 최소 3개는 항상 열려 있다)")]
        public int maxAdapted = 2;
        public float koCheckDelay = 25f;
        public float alarmRadius = 30f;
        public float alarmDuration = 18f;
        public float heatWaitScale = 2f;

        public float AlarmUntil { get; private set; }
        public bool AlarmActive => Time.time < AlarmUntil;
        public IReadOnlyCollection<string> UsedThisAttempt => usedThisAttempt;

        readonly HashSet<string> usedThisAttempt = new HashSet<string>();
        readonly List<GuardAI> pendingChecks = new List<GuardAI>();
        readonly Dictionary<string, float> coverClaims = new Dictionary<string, float>();
        readonly HashSet<string> loggedCoverSearch = new HashSet<string>();
        readonly Dictionary<GuardAI, string> adaptedGuards = new Dictionary<GuardAI, string>();
        Bounds[] coverBounds;
        float heatTimer, coverTimer, calmTimer;
        bool alertEpisode, inRunAdapted;
        Vector3 alertPos;

        void Awake()
        {
            Instance = this;
            GuardAI.KnockedOutEvent += OnKnockedOut;
            GuardAI.BodyFound += OnBodyFound;
            GuardAI.StateChanged += OnStateChanged;
            GameManager.PlayBegan += OnPlayBegan;
            if (covers != null)
            {
                coverBounds = new Bounds[covers.Length];
                for (int i = 0; i < covers.Length; i++) coverBounds[i] = BoundsOf(covers[i]);
            }
        }

        void OnDestroy()
        {
            GuardAI.KnockedOutEvent -= OnKnockedOut;
            GuardAI.BodyFound -= OnBodyFound;
            GuardAI.StateChanged -= OnStateChanged;
            GameManager.PlayBegan -= OnPlayBegan;
            if (Instance == this) Instance = null;
        }

        static Bounds BoundsOf(Transform t)
        {
            if (t == null) return new Bounds();
            var cols = t.GetComponentsInChildren<Collider>();
            if (cols.Length > 0)
            {
                var b = cols[0].bounds;
                for (int i = 1; i < cols.Length; i++) b.Encapsulate(cols[i].bounds);
                return b;
            }
            return new Bounds(t.position, Vector3.one);
        }

        public static void Say(string text, float seconds = 4f)
        {
            Radio?.Invoke(text, seconds);
            Debug.Log("[Director] 자막: " + text);
        }

        static void Log(string line)
        {
            PlayerBehaviorLog.History.Add($"#{PlayerBehaviorLog.Attempts} {line}");
            Debug.Log("[Director] " + line);
        }

        Adaptation Find(string id)
        {
            if (adaptations == null) return null;
            foreach (var a in adaptations) if (a != null && a.routeId == id) return a;
            return null;
        }

        string LabelOf(string id)
        {
            if (routes != null) foreach (var r in routes) if (r != null && r.routeId == id) return r.label;
            var a = Find(id);
            return a != null ? a.routeLabel : id;
        }

        // ---------------- 시도 시작(재시도·체크포인트 포함) ----------------

        void OnPlayBegan()
        {
            usedThisAttempt.Clear();
            inRunAdapted = false;
            adaptedGuards.Clear();
            // 체크포인트가 경로 표시 영역 안이면 그 경로를 다시 쓴 것으로 세지 않는다
            if (player != null && routes != null)
                foreach (var r in routes)
                    if (r != null && r.Contains(player.transform.position + Vector3.up * 0.5f)) usedThisAttempt.Add(r.routeId);

            // 1) 새 적응 후보: 기준 이상 쓴 경로 중 가장 많이 쓴 곳(한 번에 1명만 바꾼다)
            string best = null;
            int bestUses = 0;
            foreach (var kv in PlayerBehaviorLog.RouteUses)
            {
                if (PlayerBehaviorLog.Applied.Contains(kv.Key)) continue;
                var a = Find(kv.Key);
                if (a == null || a.guard == null) continue;
                if (kv.Value < (a.threshold > 0 ? a.threshold : useThreshold)) continue;
                if (kv.Value > bestUses) { best = kv.Key; bestUses = kv.Value; }
            }
            if (best != null && PlayerBehaviorLog.Applied.Count >= maxAdapted)
            {
                // 상한: 이미 2명이 바뀌어 있으면 덜 쓰는 경로의 감시를 풀고 옮긴다
                string least = null;
                int leastUses = int.MaxValue;
                foreach (var id in PlayerBehaviorLog.Applied)
                {
                    int u = PlayerBehaviorLog.Uses(id);
                    if (u < leastUses) { least = id; leastUses = u; }
                }
                if (least != null && bestUses > leastUses)
                {
                    PlayerBehaviorLog.Applied.Remove(least);
                    Log($"감시 해제: {LabelOf(least)} (덜 쓰는 경로) → 원래 순찰로 복귀");
                }
                else best = null;
            }

            // 2) 지난 시도에서 정한 배치를 다시 적용
            foreach (var id in PlayerBehaviorLog.Applied)
            {
                var a = Find(id);
                if (a != null) Apply(a, true);
            }

            // 3) 새 적응 적용
            if (best != null)
            {
                var a = Find(best);
                if (PlayerBehaviorLog.Applied.Count < maxAdapted && !adaptedGuards.ContainsKey(a.guard))
                {
                    PlayerBehaviorLog.Applied.Add(best);
                    Apply(a, true);
                    Log($"재시도 적응: '{a.routeLabel}' 사용 {bestUses}회 → {a.guard.callSign} 배치 변경");
                    Say($"[무전] 지휘: 침입자가 {Josa.Eul(a.routeLabel)} 자주 쓴다. {a.guard.callSign}, 그쪽을 맡아라.", 4f);
                    Say($"경비가 {Josa.Eul(a.routeLabel)} {a.action}", 5f);
                }
            }
            else if (PlayerBehaviorLog.Applied.Count > 0)
            {
                var labels = new List<string>();
                foreach (var id in PlayerBehaviorLog.Applied) labels.Add(LabelOf(id));
                Say("경비 배치 유지: " + string.Join(", ", labels) + " 감시 중", 4f);
            }

            // 4) 히트맵: 자주 머물러 곳 근처 순찰 지점에서 더 오래 머문다(바뀌지 않은 경비만)
            ApplyHeatWeights();
        }

        void Apply(Adaptation a, bool warp)
        {
            if (a == null || a.guard == null || a.route == null || a.route.Length == 0) return;
            if (adaptedGuards.ContainsKey(a.guard)) return;
            adaptedGuards[a.guard] = a.routeId;
            a.guard.SetPatrolRoute(a.route, a.post, a.scanAngle);
            // 목적지가 잠긴 보안문 안쪽(서버실)이면 경비는 출입증으로 문을 지나간다
            if (a.route[0].position.x > 5.5f && a.route[0].position.z > 11.5f)
                foreach (var d in FindObjectsByType<SecurityDoor>()) d.guardOverride = true;
            if (warp)
            {
                var agent = a.guard.GetComponent<NavMeshAgent>();
                if (agent != null && agent.isOnNavMesh) agent.Warp(a.route[0].position);
                else a.guard.transform.position = a.route[0].position;
                a.guard.transform.rotation = Quaternion.Euler(0f, a.route[0].eulerAngles.y, 0f);
            }
            var drv = a.guard.GetComponentInChildren<CharacterAnimDriver>();
            if (drv != null) drv.Talk(2.5f);
        }

        void ApplyHeatWeights()
        {
            if (PlayerBehaviorLog.Heat.Count == 0) return;
            foreach (var g in GuardAI.All)
            {
                if (g == null || g.stationary || adaptedGuards.ContainsKey(g) || g.patrolPoints == null || g.patrolPoints.Length < 2) continue;
                int bestI = -1;
                float bestHeat = 0f;
                for (int i = 0; i < g.patrolPoints.Length; i++)
                {
                    if (g.patrolPoints[i] == null) continue;
                    float h = HeatNear(g.patrolPoints[i].position, 6f);
                    if (h > bestHeat) { bestHeat = h; bestI = i; }
                }
                if (bestI < 0 || bestHeat < 8f) continue;
                var scale = new float[g.patrolPoints.Length];
                for (int i = 0; i < scale.Length; i++) scale[i] = 1f;
                scale[bestI] = heatWaitScale;
                g.pointWaitScale = scale;
                Log($"순찰 가중치: {g.callSign} → {ZoneName(g.patrolPoints[bestI].position)} 지점 대기 {heatWaitScale:0.#}배 (히트 {bestHeat:0}초)");
            }
        }

        static float HeatNear(Vector3 p, float radius)
        {
            float sum = 0f;
            foreach (var kv in PlayerBehaviorLog.Heat)
            {
                Vector3 c = PlayerBehaviorLog.CellCenter(kv.Key);
                c.y = p.y;
                if ((c - p).sqrMagnitude <= radius * radius) sum += kv.Value;
            }
            return sum;
        }

        // ---------------- 매 프레임 기록 ----------------

        void Update()
        {
            if (!GameManager.GameplayActive || player == null) return;
            float dt = Time.deltaTime;
            Vector3 pos = player.transform.position;

            if (routes != null)
            {
                Vector3 probe = pos + Vector3.up * 0.5f;
                foreach (var r in routes)
                {
                    if (r == null || usedThisAttempt.Contains(r.routeId) || !r.Contains(probe)) continue;
                    usedThisAttempt.Add(r.routeId);
                    PlayerBehaviorLog.RouteUses[r.routeId] = PlayerBehaviorLog.Uses(r.routeId) + 1;
                    Log($"경로 사용: {r.label} (누적 {PlayerBehaviorLog.Uses(r.routeId)}회)");
                }
            }

            heatTimer += dt;
            if (heatTimer >= 0.5f)
            {
                var cell = PlayerBehaviorLog.Cell(pos);
                PlayerBehaviorLog.Heat.TryGetValue(cell, out float h);
                PlayerBehaviorLog.Heat[cell] = h + heatTimer;
                heatTimer = 0f;
            }

            coverTimer += dt;
            if (coverTimer >= 0.25f)
            {
                if (player.IsCrouching && covers != null && coverBounds != null)
                {
                    for (int i = 0; i < covers.Length; i++)
                    {
                        if (covers[i] == null) continue;
                        var b = coverBounds[i];
                        b.Expand(new Vector3(2.2f, 0f, 2.2f));
                        Vector3 q = pos;
                        q.y = b.center.y;
                        if (!b.Contains(q)) continue;
                        string n = covers[i].name;
                        PlayerBehaviorLog.CoverSeconds.TryGetValue(n, out float s);
                        PlayerBehaviorLog.CoverSeconds[n] = s + coverTimer;
                        PlayerBehaviorLog.CoverHidePos[n] = pos;
                        break;
                    }
                }
                coverTimer = 0f;
            }

            // 기절한 경비 무전 확인
            for (int i = pendingChecks.Count - 1; i >= 0; i--)
            {
                var ko = pendingChecks[i];
                if (ko == null || ko.BodyDiscovered) { pendingChecks.RemoveAt(i); continue; }
                if (Time.time < ko.KnockedOutTime + koCheckDelay) continue;
                pendingChecks.RemoveAt(i);
                DispatchCheck(ko);
            }

            // 경보가 끝나면(아무도 추격 중이 아니면) 그 위치에서 가까운 경로를 보강
            bool anyAlert = false;
            foreach (var g in GuardAI.All) if (!g.IsKnockedOut && g.State == GuardState.Alert) { anyAlert = true; break; }
            if (alertEpisode)
            {
                if (anyAlert) calmTimer = 0f;
                else
                {
                    calmTimer += dt;
                    if (calmTimer > 3f)
                    {
                        alertEpisode = false;
                        AdaptAfterAlert(alertPos);
                    }
                }
            }
        }

        void OnStateChanged(GuardAI g, GuardState prev, GuardState next)
        {
            if (next == GuardState.Alert && GameManager.GameplayActive)
            {
                alertEpisode = true;
                alertPos = g.LastKnownPosition;
                calmTimer = 0f;
            }
        }

        void AdaptAfterAlert(Vector3 where)
        {
            if (inRunAdapted || routes == null) return;
            RouteMarker nearest = null;
            float best = 18f;
            foreach (var r in routes)
            {
                if (r == null || PlayerBehaviorLog.Applied.Contains(r.routeId)) continue;
                float d = Vector3.Distance(new Vector3(where.x, r.transform.position.y, where.z), r.transform.position);
                // 이번 판에 쓴 경로를 우선
                if (usedThisAttempt.Contains(r.routeId)) d *= 0.6f;
                if (d < best) { best = d; nearest = r; }
            }
            if (nearest == null) return;
            var a = Find(nearest.routeId);
            if (a == null || a.guard == null || a.guard.IsKnockedOut || adaptedGuards.ContainsKey(a.guard)) return;
            if (PlayerBehaviorLog.Applied.Count >= maxAdapted) return;
            inRunAdapted = true;
            PlayerBehaviorLog.Applied.Add(a.routeId);
            Apply(a, false);
            Log($"경보 후 적응: 경보 위치({ZoneName(where)}) 근처 '{a.routeLabel}' → {a.guard.callSign} 배치 변경");
            Say($"[무전] 지휘: 침입자가 {a.routeLabel} 쪽으로 움직였다. {a.guard.callSign}, 그쪽을 막아라.", 4f);
            Say($"경비가 {Josa.Eul(a.routeLabel)} {a.action}", 5f);
        }

        // ---------------- 무전·기절·경보 ----------------

        public void OnAlertShared(GuardAI from, Vector3 pos, int heard)
        {
            Say($"[무전] {from.callSign}: 침입자 발견! {ZoneName(pos)}. 마지막 위치 공유" + (heard > 0 ? $" ({heard}명 수신)" : ""), 3.5f);
            var drv = from.GetComponentInChildren<CharacterAnimDriver>();
            if (drv != null) drv.Talk(2f);
        }

        void OnKnockedOut(GuardAI g)
        {
            if (!pendingChecks.Contains(g)) pendingChecks.Add(g);
            Log($"기절: {g.callSign} ({ZoneName(g.transform.position)}) — {koCheckDelay:0}초 뒤 무전 확인 예정");
        }

        void DispatchCheck(GuardAI ko)
        {
            GuardAI best = null;
            float bestD = float.MaxValue;
            foreach (var g in GuardAI.All)
            {
                if (g == ko || g.IsKnockedOut || g.State == GuardState.Alert) continue;
                float d = Vector3.Distance(g.transform.position, ko.KnockedOutPosition);
                if (g.State != GuardState.Patrol) d += 15f;
                if (g.stationary) d += 14f; // 초소 경비는 자리를 잘 비우지 않는다
                if (d < bestD) { bestD = d; best = g; }
            }
            if (best == null) return;
            best.Investigate(ko.KnockedOutPosition);
            var drv = best.GetComponentInChildren<CharacterAnimDriver>();
            if (drv != null) drv.Talk(3f);
            Say($"[무전] {best.callSign}: {ko.callSign}, 응답하라… 응답이 없다. 확인하러 간다.", 4f);
            Log($"무전 확인: {ko.callSign} 응답 없음 → {Josa.I(best.callSign)} {ZoneName(ko.KnockedOutPosition)} 확인");
        }

        void OnBodyFound(GuardAI finder, GuardAI body)
        {
            AlarmUntil = Time.time + alarmDuration;
            int n = 0;
            foreach (var g in GuardAI.All)
            {
                if (g == finder || g == body || g.IsKnockedOut) continue;
                if (Vector3.Distance(g.transform.position, body.transform.position) > alarmRadius) continue;
                g.ReceiveAlarm(body.transform.position);
                n++;
            }
            var drv = finder.GetComponentInChildren<CharacterAnimDriver>();
            if (drv != null) drv.Talk(3f);
            Say($"[무전] {finder.callSign}: {Josa.I(body.callSign)} 쓰러져 있다! 경보 발령, 전원 수색!", 4.5f);
            Log($"기절체 발견: {Josa.I(finder.callSign)} {body.callSign} 발견 → 경보, {n}명 수색 합류");
            var am = AudioManager.Instance;
            if (am != null) am.PlayAt(am.guardAlert, finder.transform.position + Vector3.up * 2f, 1f, 2f, 40f);
        }

        /// <summary>수색 시작 지점: 마지막 목격 위치 근처에서 플레이어가 자주 숨던 엄폐물</summary>
        public Vector3? PreferredSearchPoint(Vector3 lastKnown, GuardAI guard)
        {
            string best = null;
            float bestSec = 2f;
            foreach (var kv in PlayerBehaviorLog.CoverSeconds)
            {
                if (kv.Value < bestSec) continue;
                if (!PlayerBehaviorLog.CoverHidePos.TryGetValue(kv.Key, out Vector3 hp)) continue;
                if (Vector3.Distance(hp, lastKnown) > 15f) continue;
                if (coverClaims.TryGetValue(kv.Key, out float until) && Time.time < until) continue;
                best = kv.Key;
                bestSec = kv.Value;
            }
            if (best == null) return null;
            Vector3 p = PlayerBehaviorLog.CoverHidePos[best];
            if (!NavMesh.SamplePosition(p, out NavMeshHit hit, 1.5f, NavMesh.AllAreas)) return null;
            coverClaims[best] = Time.time + 12f;
            if (loggedCoverSearch.Add(best))
                Log($"수색: {Josa.I(guard.callSign)} 자주 숨던 엄폐물 '{best}'({PlayerBehaviorLog.CoverSeconds[best]:0}초)부터 확인");
            return hit.position;
        }

        /// <summary>퍼즐 사용 기록(시도당 1회). 자주 쓰는 퍼즐 장치는 경비 순찰 지점으로 추가된다.</summary>
        public void RecordPuzzleUse(string id, string label)
        {
            if (string.IsNullOrEmpty(id) || usedThisAttempt.Contains(id)) return;
            usedThisAttempt.Add(id);
            PlayerBehaviorLog.RouteUses[id] = PlayerBehaviorLog.Uses(id) + 1;
            Log($"퍼즐 사용: {label} (누적 {PlayerBehaviorLog.Uses(id)}회)");
        }

        /// <summary>보안 장치 경보(레이저 등): 사이렌 + 무전 자막</summary>
        public void RaiseAlarm(Vector3 pos, string radio)
        {
            AlarmUntil = Time.time + alarmDuration;
            Say(radio, 4f);
            Log($"보안 경보: {ZoneName(pos)}");
            alertEpisode = true;
            alertPos = pos;
            calmTimer = 0f;
        }

        public static string ZoneName(Vector3 p)
        {
            if (p.y > 2.4f) return "캣워크";
            if (p.x < -15.5f) return p.z < 0f ? "야드 남쪽" : "야드 북쪽";
            if (p.z > 11.5f) return p.x > 5.5f ? "서버실" : "사무동";
            return p.x < 0f ? "메인 홀 서쪽" : "메인 홀 동쪽";
        }
    }
}
