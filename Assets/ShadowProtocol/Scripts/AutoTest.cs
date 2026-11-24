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

        static float Avg(List<float> l) { if (l.Count == 0) return 0f; float s = 0f; foreach (var v in l) s += v; return s / l.Count; }
        static float Pct(List<float> l, float q) { if (l.Count == 0) return 0f; var c = new List<float>(l); c.Sort(); return c[Mathf.Clamp(Mathf.RoundToInt(q * (c.Count - 1)), 0, c.Count - 1)]; }

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
            var shownRenderers = new List<Renderer>();
            if (player != null)
            {
                player.InputEnabled = false;
                player.enabled = false;
                var cc = player.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                player.transform.position += Vector3.down * 50f;
                foreach (var r in player.GetComponentsInChildren<Renderer>()) { if (r.enabled) shownRenderers.Add(r); r.enabled = false; }
            }

            var guards = new List<GuardAI>(GuardAI.All);
            var dist = new float[guards.Count];
            var last = new Vector3[guards.Count];
            for (int i = 0; i < guards.Count; i++) last[i] = guards[i].transform.position;
            int onNav = 0;
            foreach (var g in guards) { var a = g.GetComponent<NavMeshAgent>(); if (a != null && a.isOnNavMesh) onNav++; }

            // 프레임 시간(GPU·CPU, ms): 처음 3초(셰이더 준비·노출 적응)는 빼고 모은다. vSync는 측정 동안 끈다.
            int oldVSync = QualitySettings.vSyncCount;
            QualitySettings.vSyncCount = 0;
            var gpuMs = new List<float>(); var cpuMs = new List<float>(); var frameMs = new List<float>();
            var timings = new FrameTiming[1];
            float t = 0f;
            while (t < seconds)
            {
                yield return null;
                t += Time.deltaTime;
                FrameTimingManager.CaptureFrameTimings();
                if (t > 3f)
                {
                    frameMs.Add(Time.unscaledDeltaTime * 1000f);
                    if (FrameTimingManager.GetLatestTimings(1, timings) > 0)
                    {
                        if (timings[0].gpuFrameTime > 0) gpuMs.Add((float)timings[0].gpuFrameTime);
                        if (timings[0].cpuMainThreadFrameTime > 0) cpuMs.Add((float)timings[0].cpuMainThreadFrameTime);
                    }
                }
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
            QualitySettings.vSyncCount = oldVSync;
            Debug.Log($"[AutoTest] 프레임 시간({Screen.width}×{Screen.height}, {SystemInfo.graphicsDeviceName}, {SystemInfo.graphicsDeviceType}): " +
                      $"GPU 평균 {Avg(gpuMs):0.00}ms 95% {Pct(gpuMs, 0.95f):0.00}ms (표본 {gpuMs.Count}) / CPU 주 스레드 평균 {Avg(cpuMs):0.00}ms 95% {Pct(cpuMs, 0.95f):0.00}ms / " +
                      $"프레임 평균 {Avg(frameMs):0.00}ms = {1000f / Mathf.Max(0.01f, Avg(frameMs)):0} fps, 95% {Pct(frameMs, 0.95f):0.00}ms");
            int tris = NavMesh.CalculateTriangulation().indices.Length / 3;
            Debug.Log($"[AutoTest] {seconds:0}초 경비 이동 거리:{sb}");
            Debug.Log($"[AutoTest] 결과: 경비 {guards.Count}명 중 NavMesh 위 {onNav}명, 순찰 경비 {patrollers}명 중 1m 이상 이동 {moved}명, 합계 {total:0.0}m, NavMesh 삼각형 {tris}, NavMesh 오류 {navErrors}, 오류 {errors}, 예외 {exceptions} → {(moved == patrollers && onNav == guards.Count && guards.Count > 0 && navErrors == 0 && exceptions == 0 ? "PASS" : "FAIL")}");
            string shotDir = ArgValue("-autotest-shots");
            if (!string.IsNullOrEmpty(shotDir) && player != null) yield return Shots(player, shownRenderers, shotDir);
            yield return null;
            Application.Quit();
        }

        // 화면 비교용 고정 시점 6곳(p7 캡처와 같은 플레이어 위치·카메라 각도): x, y, z, 몸 방향, 카메라 yaw, pitch
        static readonly float[][] Views =
        {
            new[] { -35.5f, 0.05f, -15f, 25f, 25f, 12f },   // 01 하역장 야드
            new[] { -10.5f, 0.05f, 0.2f, 95f, 95f, 12f },   // 02 중앙 홀
            new[] { -4f, 3.25f, -9.9f, 60f, 55f, 28f },     // 03 캣워크
            new[] { -10.5f, 0.05f, 13.2f, 90f, 88f, 14f },  // 04 사무 복도
            new[] { 9.4f, 0.05f, 13.6f, 45f, 45f, 18f },    // 05 서버실 레이저
            new[] { 11.5f, 0.05f, 10.1f, 0f, -20f, 12f },   // 06 키패드 퍼즐
        };
        static readonly string[] ViewNames = { "01_loading_yard", "02_main_hall", "03_catwalk", "04_office_corridor", "05_server_room_laser", "06_puzzle_keypad" };

        /// <summary>-autotest-shots &lt;폴더&gt;: 실제 실행 화면(실시간, HUD 숨김 — 키패드만 UI 포함)을 시점마다 3.5초 뒤 찍고 그 시점 GPU 시간을 잰다.
        /// 경비는 멈춰 둔다(촬영 중 발각 연출 방지).</summary>
        IEnumerator Shots(PlayerController player, List<Renderer> shown, string dir)
        {
            System.IO.Directory.CreateDirectory(dir);
            // 끄면 All에서 빠지므로 복사본으로 순회
            foreach (var g in new List<GuardAI>(GuardAI.All)) if (g != null) { g.enabled = false; var a = g.GetComponent<NavMeshAgent>(); if (a != null && a.isOnNavMesh) a.isStopped = true; }
            foreach (var r in shown) if (r != null) r.enabled = true;
            var cc = player.GetComponent<CharacterController>();
            player.enabled = true;
            var tpc = FindAnyObjectByType<ThirdPersonCamera>();
            var canvases = new List<Canvas>();
            foreach (var c in FindObjectsByType<Canvas>(FindObjectsInactive.Exclude)) if (c.enabled && c.isRootCanvas) canvases.Add(c);
            QualitySettings.vSyncCount = 0;
            var timings = new FrameTiming[1];
            var line = new StringBuilder();
            for (int i = 0; i < Views.Length; i++)
            {
                var v = Views[i];
                if (cc != null) cc.enabled = false;
                player.transform.SetPositionAndRotation(new Vector3(v[0], v[1], v[2]), Quaternion.Euler(0f, v[3], 0f));
                Physics.SyncTransforms();
                if (cc != null) cc.enabled = true;
                if (tpc != null) { tpc.SetAngles(v[4], v[5]); tpc.SnapBehindPlayer(); }
                bool keypad = i == 5;
                foreach (var c in canvases) if (c != null) c.enabled = keypad;
                var gpu = new List<float>();
                float t = 0f;
                bool opened = false;
                while (t < 3.5f)
                {
                    yield return null;
                    t += Time.unscaledDeltaTime;
                    if (keypad && !opened && t > 0.5f)
                    {
                        opened = true;
                        var go = GameObject.Find("Puzzles/Keypad_Front");
                        var k = go != null ? go.GetComponent<KeypadPanel>() : null;
                        if (k != null && GameUI.Instance != null) { k.Interact(player.GetComponent<PlayerTools>()); GameUI.Instance.KeypadDigit(4); GameUI.Instance.KeypadDigit(7); }
                    }
                    FrameTimingManager.CaptureFrameTimings();
                    if (t > 2f && FrameTimingManager.GetLatestTimings(1, timings) > 0 && timings[0].gpuFrameTime > 0) gpu.Add((float)timings[0].gpuFrameTime);
                }
                string file = System.IO.Path.Combine(dir, "hdrp_" + ViewNames[i] + ".png");
                ScreenCapture.CaptureScreenshot(file, 1);
                yield return null;
                yield return null;
                yield return new WaitForEndOfFrame();
                if (keypad && GameUI.Instance != null) GameUI.Instance.ClosePuzzle();
                line.Append($" {ViewNames[i]}={Avg(gpu):0.00}ms");
            }
            foreach (var c in canvases) if (c != null) c.enabled = true;
            Debug.Log($"[AutoTest] 시점별 GPU 시간({Screen.width}×{Screen.height}):{line} → 캡처 {dir}");
        }
    }
}
