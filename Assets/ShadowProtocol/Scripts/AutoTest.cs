using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.AI;

namespace ShadowProtocol
{
    /// <summary>
    /// 실행 인자 -autotest: 타이틀을 건너뛰고 임무를 시작해 30초 동안 경비 이동 거리를 재고 로그에 남긴 뒤 종료한다.
    /// (-autotest-seconds N 으로 시간 변경). 빌드 exe에서 NavMesh·경비 이동을 사람 없이 확인하는 용도.
    /// </summary>
    public class AutoTest : MonoBehaviour
    {
        static bool created;
        float seconds = 30f;
        int navErrors, errors, exceptions;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (created || !HasArg("-autotest")) return;
            created = true;
            var go = new GameObject("AutoTest");
            DontDestroyOnLoad(go);
            go.AddComponent<AutoTest>();
        }

        static bool HasArg(string a)
        {
            foreach (var s in System.Environment.GetCommandLineArgs()) if (s == a) return true;
            return false;
        }

        static string ArgValue(string a)
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == a) return args[i + 1];
            return null;
        }

        void OnEnable() { Application.logMessageReceived += OnLog; }
        void OnDisable() { Application.logMessageReceived -= OnLog; }

        void OnLog(string msg, string stack, LogType type)
        {
            if (msg.Contains("NavMesh") && (type == LogType.Error || type == LogType.Warning || msg.Contains("Failed to create agent"))) navErrors++;
            if (type == LogType.Error || type == LogType.Assert) errors++;
            if (type == LogType.Exception) exceptions++;
        }

        IEnumerator Start()
        {
            // 창이 포커스를 잃어도 멈추지 않게(플레이어 설정 runInBackground=false라 자동 검증이 배경에서 멈췄다)
            Application.runInBackground = true;
            var v = ArgValue("-autotest-seconds");
            if (v != null && float.TryParse(v, out float f) && f > 1f) seconds = f;
            Debug.Log($"[AutoTest] 시작: {seconds:0}초 동안 경비 이동 거리 측정");
            yield return null;
            yield return null;
            var gm = GameManager.Instance;
            if (gm != null && gm.Flow != GameManager.FlowState.Playing) gm.StartMission();
            // 측정 중 플레이어가 붙잡혀 경비가 멈추지 않도록 플레이어를 경비 시야 밖(시작 지점 아래)으로 숨긴다
            var player = gm != null ? gm.player : null;
            if (player != null)
            {
                player.InputEnabled = false;
                player.enabled = false;
                var cc = player.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                player.transform.position += Vector3.down * 50f;
                foreach (var r in player.GetComponentsInChildren<Renderer>()) r.enabled = false;
            }

            var guards = new List<GuardAI>(GuardAI.All);
            var dist = new float[guards.Count];
            var last = new Vector3[guards.Count];
            for (int i = 0; i < guards.Count; i++) last[i] = guards[i].transform.position;
            int onNav = 0;
            foreach (var g in guards) { var a = g.GetComponent<NavMeshAgent>(); if (a != null && a.isOnNavMesh) onNav++; }

            float t = 0f;
            while (t < seconds)
            {
                yield return null;
                t += Time.deltaTime;
                for (int i = 0; i < guards.Count; i++)
                {
                    if (guards[i] == null) continue;
                    var p = guards[i].transform.position;
                    dist[i] += Vector3.Distance(new Vector3(p.x, 0f, p.z), new Vector3(last[i].x, 0f, last[i].z));
                    last[i] = p;
                }
            }

            var sb = new StringBuilder();
            float total = 0f; int moved = 0, patrollers = 0;
            foreach (var g in guards) if (g != null && !g.stationary) patrollers++;
            for (int i = 0; i < guards.Count; i++)
            {
                total += dist[i];
                bool post = guards[i] != null && guards[i].stationary;
                if (dist[i] > 1f && !post) moved++;
                sb.Append($" {(guards[i] != null ? guards[i].name : "?")}={dist[i]:0.0}m{(post ? "(고정 초소)" : "")}");
            }
            int tris = NavMesh.CalculateTriangulation().indices.Length / 3;
            Debug.Log($"[AutoTest] {seconds:0}초 경비 이동 거리:{sb}");
            Debug.Log($"[AutoTest] 결과: 경비 {guards.Count}명 중 NavMesh 위 {onNav}명, 순찰 경비 {patrollers}명 중 1m 이상 이동 {moved}명, 합계 {total:0.0}m, NavMesh 삼각형 {tris}, NavMesh 오류 {navErrors}, 오류 {errors}, 예외 {exceptions} → {(moved == patrollers && onNav == guards.Count && guards.Count > 0 && navErrors == 0 && exceptions == 0 ? "PASS" : "FAIL")}");
            yield return null;
            Application.Quit();
        }
    }
}
