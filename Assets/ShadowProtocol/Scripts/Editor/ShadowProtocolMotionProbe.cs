using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ShadowProtocol.EditorTools
{
    /// <summary>
    /// 모션 측정(플레이 모드): 봇처럼 에디터를 멈춘 채 프레임을 직접 진행하며 정해진 입력을 넣고
    /// 플레이어 속도·몸 방향과 경비 몸 속도·방향 차이를 기록한다. 에디터 창이 포커스를 잃어도 동작한다.
    /// 사용: 플레이 모드에서 ShadowProtocolMotionProbe.Run() → Running이 false가 되면 Report 확인.
    /// </summary>
    public static class ShadowProtocolMotionProbe
    {
        public struct Phase { public float seconds; public Vector2 move; public bool run, crouch, action; public string name; }

        public static bool Running { get; private set; }
        public static string Report { get; private set; } = "";
        public static int FramesPerTick = 4;
        public static int PauseAtFrame = -1; // 이 프레임에서 멈추고 Running 유지(스크린샷용). Resume()으로 계속.

        static List<Phase> phases;
        static int phaseIndex, frame;
        static float phaseStart;
        static PlayerController player;
        static StringBuilder log;
        static bool holding;

        public static List<Phase> Default()
        {
            return new List<Phase>
            {
                new Phase { name = "정지", seconds = 0.5f },
                new Phase { name = "걷기 북동", seconds = 1.6f, move = new Vector2(0.7071f, 0.7071f) },
                new Phase { name = "반대로 달리기", seconds = 1.6f, move = new Vector2(-0.7071f, -0.7071f), run = true },
                new Phase { name = "90도 꺾어 달리기", seconds = 1.2f, move = new Vector2(0.7071f, -0.7071f), run = true },
                new Phase { name = "멈춤", seconds = 1.0f },
            };
        }

        public static void Run(List<Phase> seq = null)
        {
            phases = seq ?? Default();
            phaseIndex = 0; frame = 0; holding = false;
            phaseStart = -1f;
            log = new StringBuilder();
            log.AppendLine("t | 단계 | 플레이어 속도 | 몸 방향 | 진행 방향 | 경비(Yard) 몸속도/원하는속도/방향차");
            Running = true;
            player = null;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.isPaused = true;
        }

        public static void Resume() { holding = false; PauseAtFrame = -1; }

        static void Stop()
        {
            Running = false;
            EditorApplication.update -= Tick;
            if (player != null) { player.VirtualMove = Vector2.zero; player.VirtualRun = false; player.VirtualCrouch = false; }
            Report = log.ToString();
        }

        static void Tick()
        {
            if (!Running) { EditorApplication.update -= Tick; return; }
            if (!EditorApplication.isPlaying) { Stop(); return; }
            if (holding) return;
            if (!EditorApplication.isPaused) EditorApplication.isPaused = true;
            for (int f = 0; f < FramesPerTick && Running; f++)
            {
                Frame();
                EditorApplication.Step();
                frame++;
                if (frame == PauseAtFrame) { holding = true; Report = log.ToString(); return; }
            }
        }

        static void Frame()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;
            if (player == null) player = Object.FindAnyObjectByType<PlayerController>();
            if (player == null) return;
            if (gm.Flow != GameManager.FlowState.Playing) { gm.StartMission(); return; }
            if (phaseStart < 0f) phaseStart = Time.time;
            player.UseVirtualInput = true;
            player.VirtualMoveIsWorld = true;

            var ph = phases[phaseIndex];
            if (Time.time - phaseStart >= ph.seconds)
            {
                phaseIndex++;
                phaseStart = Time.time;
                if (phaseIndex >= phases.Count) { Stop(); return; }
                ph = phases[phaseIndex];
            }
            player.VirtualMove = ph.move;
            player.VirtualRun = ph.run;
            player.VirtualCrouch = ph.crouch;
            if (ph.action)
            {
                var pk = player.GetComponent<ParkourController>();
                if (pk != null && !pk.Busy && pk.TryAction()) log.AppendLine($"{Time.time:0.00} | Space 동작 시작 at {player.transform.position.x:0.0},{player.transform.position.z:0.0}");
            }

            if (frame % 6 == 0)
            {
                Vector3 v = player.PlanarVelocity;
                float heading = v.sqrMagnitude > 0.01f ? Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg : float.NaN;
                string g = "";
                foreach (var ga in GuardAI.All)
                {
                    if (ga.name != "Guard_Yard") continue;
                    var loc = ga.GetComponent<GuardLocomotion>();
                    var ag = ga.GetComponent<UnityEngine.AI.NavMeshAgent>();
                    if (loc == null || ag == null || !ag.enabled) break;
                    Vector3 d = ag.desiredVelocity; d.y = 0f;
                    float diff = d.sqrMagnitude > 0.01f ? Vector3.Angle(ga.transform.forward, d) : 0f;
                    g = $"{loc.Speed:0.00}/{d.magnitude:0.00}/{diff:0}°";
                }
                log.AppendLine($"{Time.time:0.00} | {ph.name} | {v.magnitude:0.00} | {player.transform.eulerAngles.y:0} | {(float.IsNaN(heading) ? "-" : heading.ToString("0"))} | {g}");
            }
        }
    }
}
