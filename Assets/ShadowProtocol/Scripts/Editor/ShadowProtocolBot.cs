using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace ShadowProtocol.EditorTools
{
    /// <summary>
    /// 플레이 테스트 봇(에디터 전용). 플레이 모드를 일시정지한 채 한 프레임씩 진행(EditorApplication.Step)하며
    /// 가상 입력으로 플레이어를 움직인다. 이동 전에 경비 순찰을 NavMesh 경로로 앞질러 예측해 시야(각도·거리·밝기·가림)에
    /// 걸리지 않을 때만 출발하고, 아니면 기다리거나 직전 안전 지점으로 물러난다.
    /// 퍼즐은 정답 API로 풀되 키패드는 UI 흐름(숫자 버튼 → 입력)을 그대로 거친다.
    /// </summary>
    public static class ShadowProtocolBot
    {
        public class Step
        {
            public string name;
            public Func<bool> tick;   // true면 다음 단계
            public Action enter;
        }

        public static bool Running { get; private set; }
        public static string Status = "";
        public static readonly StringBuilder Log = new StringBuilder();
        public static int StepIndex;
        public static List<Step> Steps = new List<Step>();
        public static string RouteName = "";
        public static int FramesPerTick = 2;

        static PlayerController player;
        static PlayerTools tools;
        static bool entered;
        static float stepStart;
        static Vector3 lastSafe;
        // 한 칸 전 안전 지점: 지금 선 자리(lastSafe)가 곧 위험해지면 여기로 물러난다(웅크림 속도 1.05로 느려진 뒤 경비 순찰 끝점에서 기다리다 들킨 문제)
        static Vector3 prevSafe;
        static float waitAccum;
        static bool safeInit;

        static float Now => Time.time;

        public static void Run(string name, List<Step> steps)
        {
            RouteName = name;
            Steps = steps;
            StepIndex = 0;
            entered = false;
            Running = true;
            waitAccum = 0f;
            safeInit = false;
            Status = "running";
            Log.Clear();
            Log.AppendLine($"[봇] 경로 {name} 시작 (단계 {steps.Count})");
            GuardAI.StateChanged -= OnGuardState;
            GuardAI.StateChanged += OnGuardState;
            GuardDirector.Radio -= OnRadio;
            GuardDirector.Radio += OnRadio;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.isPaused = true;
            SteppedRenderFix();
        }

        /// <summary>
        /// 에디터를 멈춘 채 EditorApplication.Step()으로 진행하면 HDRP 디퍼드에서 스킨 메시(인물)의 Object 모션 벡터가
        /// 이전 프레임 스킨과 어긋나 몸이 반투명 유령처럼 보인다(실시간 플레이·빌드에서는 생기지 않음, 2026-11-24 확인).
        /// 봇·측정 중에만 플레이 모드 인스턴스의 인물 모션 벡터를 Camera로 바꾼다(씬 파일은 그대로).
        /// </summary>
        public static void SteppedRenderFix()
        {
            if (!EditorApplication.isPlaying) return;
            foreach (var r in UnityEngine.Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Include))
                r.motionVectorGenerationMode = MotionVectorGenerationMode.Camera;
        }

        static void OnGuardState(GuardAI g, GuardState prev, GuardState next)
        {
            if (!Running || player == null) return;
            Vector3 p = player.transform.position, q = g.transform.position;
            Log.AppendLine($"    t={Now:0.0} [{g.name}] {prev}→{next} 경비({q.x:0.0},{q.z:0.0}) 플레이어({p.x:0.0},{p.z:0.0}) 보임={g.CanSeePlayer} 자세={(player.IsCrouching ? "웅크림" : "서있음")}");
        }

        static void OnRadio(string text, float sec)
        {
            if (Running) Log.AppendLine($"    t={Now:0.0} 자막: {text}");
        }

        public static void Stop(string why)
        {
            Running = false;
            Status = why;
            Log.AppendLine($"[봇] 종료: {why}  (t={Now:0.0}, 단계 {StepIndex}/{Steps.Count})");
            EditorApplication.update -= Tick;
            if (player != null) { player.VirtualMove = Vector2.zero; player.VirtualRun = false; }
        }

        static void Tick()
        {
            if (!Running) { EditorApplication.update -= Tick; return; }
            if (!EditorApplication.isPlaying) { Stop("play mode ended"); return; }
            if (!EditorApplication.isPaused) EditorApplication.isPaused = true;
            for (int f = 0; f < FramesPerTick && Running; f++)
            {
                try { Frame(); }
                catch (Exception e) { Stop("exception: " + e.Message); Debug.LogException(e); return; }
                EditorApplication.Step();
            }
        }

        static void Frame()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;
            if (player == null || !player) { player = UnityEngine.Object.FindAnyObjectByType<PlayerController>(); tools = player != null ? player.GetComponent<PlayerTools>() : null; }
            if (player == null) return;
            if (!safeInit) { lastSafe = prevSafe = player.transform.position; safeInit = true; }
            player.UseVirtualInput = true;
            player.VirtualMoveIsWorld = true;
            if (gm.Flow == GameManager.FlowState.Title || gm.Flow == GameManager.FlowState.Briefing) { gm.StartMission(); return; }
            if (gm.CurrentResult != GameManager.Result.Playing && !(StepIndex < Steps.Count && Steps[StepIndex].name.StartsWith("restart")))
            {
                Stop(gm.CurrentResult == GameManager.Result.Won ? $"WON t={gm.Elapsed:0.0}s det={gm.Detections} ko={gm.Knockouts}" : $"LOST by {gm.CaughtBy} t={gm.Elapsed:0.0}s");
                return;
            }
            if (StepIndex >= Steps.Count) { Stop("steps done (result " + gm.CurrentResult + ")"); return; }
            var s = Steps[StepIndex];
            if (!entered)
            {
                entered = true;
                stepStart = Now;
                s.enter?.Invoke();
            }
            if (s.tick())
            {
                Log.AppendLine($"  t={Now:0.0} ({gm.Elapsed:0.0}) ✓ {s.name}" + (waitAccum > 0.05f ? $" (대기 {waitAccum:0.0}s)" : ""));
                waitAccum = 0f;
                StepIndex++;
                entered = false;
            }
            else if (Now - stepStart > 150f) Stop("timeout at " + s.name);
        }

        // ================= 예측 =================

        static readonly Dictionary<GuardAI, (Transform[] pts, List<Vector3>[] legs)> legCache = new Dictionary<GuardAI, (Transform[], List<Vector3>[])>();
        static readonly NavMeshPath scratch = new NavMeshPath();

        static List<Vector3> NavPath(Vector3 a, Vector3 b)
        {
            var list = new List<Vector3>();
            if (NavMesh.SamplePosition(a, out NavMeshHit ha, 1.5f, NavMesh.AllAreas) && NavMesh.SamplePosition(b, out NavMeshHit hb, 1.5f, NavMesh.AllAreas) &&
                NavMesh.CalculatePath(ha.position, hb.position, NavMesh.AllAreas, scratch) && scratch.status != NavMeshPathStatus.PathInvalid)
                list.AddRange(scratch.corners);
            if (list.Count == 0) { list.Add(a); list.Add(b); }
            return list;
        }

        static List<Vector3>[] Legs(GuardAI g)
        {
            if (legCache.TryGetValue(g, out var c) && c.pts == g.patrolPoints) return c.legs;
            var pts = g.patrolPoints;
            var legs = new List<Vector3>[pts.Length];
            for (int i = 0; i < pts.Length; i++) legs[i] = NavPath(pts[i].position, pts[(i + 1) % pts.Length].position);
            legCache[g] = (pts, legs);
            return legs;
        }

        /// <summary>경비 g의 t초 뒤 위치·시선·추가 각도(둘러보기)</summary>
        public static void Predict(GuardAI g, float t, out Vector3 pos, out Vector3 fwd, out float extra, out bool hostile)
        {
            pos = g.transform.position; fwd = g.transform.forward; extra = 10f; hostile = false;
            if (g.State == GuardState.Alert) { hostile = true; extra = 180f; return; }
            if (g.State == GuardState.Suspicious || g.State == GuardState.Search)
            {
                // 소리·목격 지점으로 간 뒤 두리번거린다
                float tt = 0f;
                var to = NavPath(pos, g.LastKnownPosition);
                if (Walk(to, g.State == GuardState.Search ? g.searchSpeed : g.suspiciousSpeed, ref tt, t, ref pos, ref fwd)) { extra = 30f; return; }
                if (g.State == GuardState.Suspicious)
                {
                    // 두리번거린 뒤(의심 게이지가 다 내려가면) 가장 가까운 순찰 지점으로 돌아간다
                    float look = Mathf.Max(0.3f, g.Suspicion / Mathf.Max(0.05f, g.suspicionDecayRate));
                    if (tt + look < t && g.patrolPoints != null && g.patrolPoints.Length > 0)
                    {
                        tt += look;
                        Transform nearest = g.patrolPoints[0];
                        foreach (var pp in g.patrolPoints) if ((pp.position - pos).sqrMagnitude < (nearest.position - pos).sqrMagnitude) nearest = pp;
                        var back = NavPath(pos, nearest.position);
                        Walk(back, g.patrolSpeed, ref tt, t, ref pos, ref fwd);
                        extra = 45f;
                        return;
                    }
                }
                extra = g.State == GuardState.Search ? 180f : 100f;
                hostile = g.State == GuardState.Search;
                return;
            }
            if (g.patrolPoints == null || g.patrolPoints.Length == 0) return;
            if (g.stationary) { fwd = Quaternion.Euler(0f, g.patrolPoints[0].eulerAngles.y, 0f) * Vector3.forward; extra = g.scanAngle + 8f; return; }
            var agent = g.GetComponent<NavMeshAgent>();
            var legs = Legs(g);
            int idx = g.PatrolIndex;
            float time = 0f;
            float waitFull(int i) => g.waitAtPoint * ((g.pointWaitScale != null && i < g.pointWaitScale.Length) ? g.pointWaitScale[i] : 1f);
            // 1) 현재 목적지까지
            var first = new List<Vector3> { pos };
            if (agent != null && agent.hasPath && agent.remainingDistance > 0.4f) first.AddRange(agent.path.corners);
            if (Walk(first, g.patrolSpeed, ref time, t, ref pos, ref fwd)) { extra = 12f + WalkSweep; return; }
            float w = Mathf.Max(0f, waitFull(idx) - (agent != null && agent.hasPath && agent.remainingDistance > 0.4f ? 0f : g.WaitTimer));
            if (time + w >= t) { extra = 42f; return; }
            time += w;
            for (int k = 0; k < 12; k++)
            {
                var leg = legs[idx];
                idx = (idx + 1) % legs.Length;
                if (Walk(leg, g.patrolSpeed, ref time, t, ref pos, ref fwd)) { extra = 12f + WalkSweep; return; }
                w = waitFull(idx);
                if (time + w >= t) { extra = 42f; return; }
                time += w;
            }
        }

        /// <summary>Walk가 '돌아서는 중'에서 멈췄으면 그 시선 훑기 반각(도), 아니면 0</summary>
        static float WalkSweep;

        static bool Walk(List<Vector3> pts, float speed, ref float time, float t, ref Vector3 pos, ref Vector3 fwd)
        {
            WalkSweep = 0f;
            // GuardLocomotion: 출발·정지 가속(약 0.55s 손해)과 모퉁이에서 먼저 돌아서는 시간(250°/s)을 더한다
            bool started = false;
            for (int i = 0; i + 1 < pts.Count; i++)
            {
                Vector3 a = pts[i], b = pts[i + 1];
                float len = Vector3.Distance(a, b);
                if (len < 0.01f) continue;
                float dur = len / Mathf.Max(0.1f, speed);
                Vector3 d = (b - a); d.y = 0f;
                if (d.sqrMagnitude > 0.0001f)
                {
                    float turn = Vector3.Angle(fwd, d);
                    float pause = (turn > 20f ? turn / 250f : 0f) + (started ? 0f : 0.55f);
                    started = true;
                    if (time + pause >= t)
                    {
                        // 돌아서는 중: 시선이 옛 방향→새 방향을 훑는다 → 가운데를 보고 회전량 절반만큼 넓게
                        pos = a;
                        fwd = Vector3.Slerp(fwd, d.normalized, 0.5f).normalized;
                        WalkSweep = turn * 0.5f;
                        return true;
                    }
                    fwd = d.normalized;
                    time += pause;
                }
                if (time + dur >= t) { pos = Vector3.Lerp(a, b, (t - time) / dur); return true; }
                time += dur;
                pos = b;
            }
            return false;
        }

        static bool LosClear(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            float len = d.magnitude;
            var hits = Physics.RaycastAll(from, d / len, len, ~0, QueryTriggerInteraction.Ignore);
            foreach (var h in hits)
            {
                var c = h.collider;
                if (c.GetComponentInParent<PlayerController>() != null || c.GetComponentInParent<GuardAI>() != null || c.GetComponentInParent<ThrownObject>() != null) continue;
                return false;
            }
            return true;
        }

        public static bool Visible(GuardAI g, Vector3 gpos, Vector3 gfwd, float extra, bool hostile, Vector3 p, bool crouch)
        {
            Vector3 eye = gpos + Vector3.up * g.eyeHeight;
            Vector3 tgt = p + Vector3.up * (crouch ? 0.6f : 1.0f);
            Vector3 flat = tgt - eye; flat.y = 0f;
            float dist = flat.magnitude;
            if (dist < 2.3f) return true;
            float light = Mathf.Lerp(g.darkRangeFactor, g.brightRangeFactor, Mathf.Clamp01(LightZone.BrightnessAt(tgt)));
            float range = g.viewDistance * (hostile ? 1.3f : 1f) * (crouch ? 0.6f : 1f) * light + 1.5f;
            if (dist > range) return false;
            if (Vector3.Angle(gfwd, flat) > g.viewAngle * 0.5f + extra + 8f) return false;
            if (LosClear(eye, tgt)) return true;
            if (!crouch && LosClear(eye, p + Vector3.up * 1.55f)) return true;
            return false;
        }

        /// <summary>경로를 따라 이동(그리고 끝에서 머무는) 동안 들키지 않는가</summary>
        public static bool PathSafe(List<Vector3> path, bool crouch, float speed, float extraHold, out string who) => PathSafe(path, crouch, speed, extraHold, false, out who);

        /// <summary>standAtEnd: 도착 뒤 머무는 동안은 서 있다(키패드·배전반 조작 — 퍼즐은 서서만 조작)</summary>
        public static bool PathSafe(List<Vector3> path, bool crouch, float speed, float extraHold, bool standAtEnd, out string who)
        {
            who = null;
            float total = 0f;
            for (int i = 0; i + 1 < path.Count; i++) total += Vector3.Distance(path[i], path[i + 1]);
            float legT = total / speed;
            float horizon = Mathf.Min(legT, 9f) + extraHold;
            for (float t = 0f; t <= horizon; t += 0.25f)
            {
                Vector3 p = PointAt(path, Mathf.Min(t, legT) * speed);
                foreach (var g in GuardAI.All)
                {
                    if (g == null || g.IsKnockedOut) continue;
                    Predict(g, t, out Vector3 gp, out Vector3 gf, out float ex, out bool hostile);
                    bool c = crouch && !(standAtEnd && t > legT);
                    if (Visible(g, gp, gf, ex, hostile, p, c)) { who = $"{g.name}@{t:0.0}s"; return false; }
                }
            }
            return true;
        }

        static Vector3 PointAt(List<Vector3> path, float d)
        {
            for (int i = 0; i + 1 < path.Count; i++)
            {
                float len = Vector3.Distance(path[i], path[i + 1]);
                if (d <= len) return Vector3.Lerp(path[i], path[i + 1], len > 0 ? d / len : 0f);
                d -= len;
            }
            return path[path.Count - 1];
        }

        // ================= 단계 =================

        static void Drive(Vector3 target, bool crouch, bool run)
        {
            Vector3 d = target - player.transform.position;
            d.y = 0f;
            player.VirtualCrouch = crouch;
            player.VirtualRun = run && !crouch;
            // 사람처럼 목표 40cm 안에서는 입력을 줄여 감속(플레이어 관성 때문에 정확한 지점에 서려면 필요)
            float mag = Mathf.Clamp(d.magnitude / 0.4f, 0.25f, 1f);
            player.VirtualMove = d.sqrMagnitude > 0.0001f ? new Vector2(d.x, d.z).normalized * mag : Vector2.zero;
        }

        static void Hold(bool crouch)
        {
            player.VirtualMove = Vector2.zero;
            player.VirtualCrouch = crouch;
            player.VirtualRun = false;
        }

        /// <summary>안전할 때 출발해 목표까지 이동(nav: NavMesh 경로, 아니면 직선)</summary>
        public static Step Move(Vector3 target, bool crouch = true, bool nav = true, float hold = 2.5f, bool check = true, string label = null, bool standAfter = false)
        {
            List<Vector3> path = null;
            int corner = 0;
            bool committed = false;
            bool retreating = false;
            Vector3 retreatTo = Vector3.zero;
            float lastCheck = -1f;
            return new Step
            {
                name = label ?? $"move {target.x:0.0},{target.z:0.0}" + (crouch ? " (웅크림)" : ""),
                enter = () => { path = null; committed = false; retreating = false; corner = 0; },
                tick = () =>
                {
                    Vector3 p = player.transform.position;
                    float speed = crouch ? player.crouchSpeed : player.walkSpeed;
                    if (!committed)
                    {
                        if (Now - lastCheck < 0.1f)
                        {
                            // 후퇴 중에는 검사 사이에도 계속 이동(플레이어 관성 때문에 한 프레임씩 입력하면 출발하지 못한다)
                            if (retreating && Vector3.Distance(p, retreatTo) > 0.3f) { Drive(retreatTo, true, false); return false; }
                            Hold(crouch); waitAccum += Time.deltaTime; return false;
                        }
                        lastCheck = Now;
                        path = nav ? NavPath(p, target) : new List<Vector3> { p, target };
                        if (path.Count > 0) path[path.Count - 1] = new Vector3(target.x, path[path.Count - 1].y, target.z);
                        if (!check || PathSafe(path, crouch, speed, hold, standAfter, out string who))
                        {
                            committed = true;
                            corner = 1;
                        }
                        else
                        {
                            // 지금 자리도 곧 위험하면 직전 안전 지점(지금 그 자리면 한 칸 전 지점)으로 물러난다
                            var here = new List<Vector3> { p, p };
                            Vector3 back = Vector3.Distance(p, lastSafe) > 0.5f ? lastSafe : prevSafe;
                            if (!retreating && Vector3.Distance(p, back) > 0.5f && !PathSafe(here, true, speed, 4f, out string w2))
                            {
                                retreating = true;
                                retreatTo = back;
                                Log.AppendLine($"    t={Now:0.0} 후퇴({w2}) → {back.x:0.0},{back.z:0.0}");
                            }
                            if (retreating)
                            {
                                if (Vector3.Distance(p, retreatTo) > 0.3f) { Drive(retreatTo, true, false); return false; }
                                retreating = false;
                            }
                            Hold(crouch);
                            waitAccum += Time.deltaTime;
                            return false;
                        }
                    }
                    if (corner >= path.Count) { Hold(crouch); prevSafe = lastSafe; lastSafe = p; return true; }
                    Vector3 c = path[corner];
                    Vector3 d = c - p; d.y = 0f;
                    if (d.magnitude < (corner == path.Count - 1 ? 0.2f : 0.35f)) { corner++; return false; }
                    Drive(c, crouch, false);
                    return false;
                }
            };
        }

        public static Step Do(string name, Action a) => new Step { name = name, tick = () => { a(); return true; } };

        public static Step WaitUntil(string name, Func<bool> cond, bool crouch = true) => new Step
        {
            name = name,
            tick = () => { Hold(crouch); if (cond()) return true; waitAccum += Time.deltaTime; return false; }
        };

        public static Step WaitSeconds(float s, bool crouch = true)
        {
            float until = 0f;
            return new Step { name = $"wait {s:0.0}s", enter = () => until = Now + s, tick = () => { Hold(crouch); return Now >= until; } };
        }

        /// <summary>키패드: UI를 열고 숫자 버튼을 하나씩 누른 뒤 입력(정답은 API로 얻는다)</summary>
        public static Step Keypad(string name)
        {
            int phase = 0; float next = 0f; KeypadPanel k = null; string code = null;
            return new Step
            {
                name = "keypad " + name,
                enter = () => { phase = 0; k = GameObject.Find("Puzzles/" + name)?.GetComponent<KeypadPanel>(); code = PuzzleState.Code; },
                tick = () =>
                {
                    Hold(false);
                    if (k == null) throw new Exception("keypad not found " + name);
                    if (k.Solved && phase < 6) phase = 6;
                    if (Now < next) return false;
                    next = Now + 0.25f;
                    if (phase == 0) { k.Interact(tools); phase = 1; return false; }
                    if (phase >= 1 && phase <= 4) { GameUI.Instance.KeypadDigit(code[phase - 1] - '0'); phase++; return false; }
                    if (phase == 5) { GameUI.Instance.KeypadSubmit(); phase = 6; return false; }
                    var door = k.doors[0];
                    return door.IsOpen;
                }
            };
        }

        /// <summary>배전반: UI를 열고 정답 스위치를 차례로 누른다</summary>
        public static Step Breaker()
        {
            int i = 0; float next = 0f; BreakerPanel b = null; int[] sol = null;
            return new Step
            {
                name = "breaker",
                enter = () => { b = UnityEngine.Object.FindAnyObjectByType<BreakerPanel>(); i = -1; },
                tick = () =>
                {
                    Hold(false);
                    if (b.Solved) return true;
                    if (Now < next) return false;
                    next = Now + 0.3f;
                    if (i < 0) { b.Interact(tools); sol = b.SolutionCells(); i = 0; return false; }
                    if (i < sol.Length) b.Press(sol[i++]);
                    return b.Solved;
                }
            };
        }

        /// <summary>레이저 격자 통과: 빔 k가 꺼진 박자에 다음 칸으로(들어갈 때 0→3, 나올 때 3→0)</summary>
        public static List<Step> Laser(bool inward)
        {
            var steps = new List<Step>();
            LaserGrid grid = null;
            Vector3 core = new Vector3(13.2f, 0f, 17.4f);
            Vector3 n = new Vector3(-0.72f, 0f, -0.694f).normalized;
            float[] mids = { 4.4f, 3.45f, 2.55f, 1.65f, 0.75f };
            for (int j = 0; j < 4; j++)
            {
                int beam = inward ? j : 3 - j;
                Vector3 from = core + n * (inward ? mids[j] : mids[4 - j]);
                Vector3 to = core + n * (inward ? mids[j + 1] : mids[3 - j]);
                bool going = false;
                steps.Add(new Step
                {
                    name = $"laser beam {beam + 1} " + (inward ? "in" : "out"),
                    enter = () => { going = false; grid = UnityEngine.Object.FindAnyObjectByType<LaserGrid>(); },
                    tick = () =>
                    {
                        var p = player.transform.position;
                        if (!going)
                        {
                            if (Vector3.Distance(new Vector3(p.x, 0, p.z), new Vector3(from.x, 0, from.z)) > 0.15f) { Drive(from, false, false); return false; }
                            Hold(false);
                            if (grid.OffBeam == beam && grid.TimeToNextStep > 0.55f) going = true;
                            else { waitAccum += Time.deltaTime; return false; }
                        }
                        Vector3 d = to - p; d.y = 0f;
                        if (d.magnitude < 0.12f) { Hold(false); return true; }
                        Drive(to, false, false);
                        return false;
                    }
                });
            }
            return steps;
        }

        public static Step ThrowAt(Vector3 target)
        {
            float t0 = 0f;
            return new Step
            {
                name = $"throw → {target.x:0.0},{target.z:0.0}",
                enter = () => { t0 = Now; if (!tools.ThrowAt(target)) Log.AppendLine("    던질 물건 없음"); },
                tick = () => { Hold(true); return Now - t0 > 1.8f; }
            };
        }

        public static Step Takedown(string guardName)
        {
            GuardAI g = null;
            return new Step
            {
                name = "takedown " + guardName,
                enter = () => g = GameObject.Find("Guards/" + guardName)?.GetComponent<GuardAI>(),
                tick = () =>
                {
                    if (g == null || g.IsKnockedOut) return true;
                    if (g.CanBeTakenDownBy(player)) { Hold(true); return tools.TryTakedown(); }
                    Vector3 behind = g.transform.position - g.transform.forward * 1.0f;
                    // 멀 때는 걸어서 따라붙고(발소리 3m), 가까워지면 웅크려 소리 없이
                    bool far = Vector3.Distance(player.transform.position, g.transform.position) > 4.2f;
                    // 벽을 돌아가도록 NavMesh 경로의 다음 모통이를 따라간다
                    var path = NavPath(player.transform.position, behind);
                    Vector3 next = behind;
                    for (int i = 1; i < path.Count; i++)
                    {
                        Vector3 dd = path[i] - player.transform.position; dd.y = 0f;
                        if (dd.magnitude > 0.3f || i == path.Count - 1) { next = path[i]; break; }
                    }
                    Drive(next, !far, false);
                    return false;
                }
            };
        }

        public static Step Drag(bool start) => Do(start ? "drag start" : "drag drop", () => tools.ToggleDrag());

        public static Step Restart(bool fromCheckpoint)
        {
            int phase = 0; GameManager old = null;
            return new Step
            {
                name = "restart" + (fromCheckpoint ? " (checkpoint)" : " (beginning)"),
                enter = () => { phase = 0; },
                tick = () =>
                {
                    var gm = GameManager.Instance;
                    if (phase == 0)
                    {
                        old = gm;
                        if (fromCheckpoint) gm.Restart(); else gm.RestartFromBeginning();
                        phase = 1;
                        player = null;
                        legCache.Clear();
                        return false;
                    }
                    if (gm == null || gm == old || Time.timeSinceLevelLoad < 0.1f) return false;
                    player = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
                    tools = player.GetComponent<PlayerTools>();
                    lastSafe = prevSafe = player.transform.position;
                    return true;
                }
            };
        }

        public static void ResetSafe() { if (player != null) lastSafe = prevSafe = player.transform.position; }
    }
}
