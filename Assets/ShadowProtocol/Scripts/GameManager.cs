using System;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShadowProtocol
{
    /// <summary>
    /// 게임 흐름(타이틀 → 브리핑 → 플레이 ⇄ 일시정지 → 결과), 승패 판정, 기록(시간·발각·기절), 체크포인트 재시작.
    /// 화면은 GameUI(uGUI)가 이 상태를 읽어 그린다.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class GameManager : MonoBehaviour
    {
        public enum Result { Playing, Won, Lost }
        public enum MissionPhase { Infiltrate, Extract }
        public enum FlowState { Title, Briefing, Playing, Paused, Result }

        public static GameManager Instance { get; private set; }
        public static bool IsOver => Instance != null && Instance.result != Result.Playing;
        /// <summary>경비 AI·플레이어 입력이 돌아가는 중(타이틀·브리핑·일시정지·결과에서는 false)</summary>
        public static bool GameplayActive => Instance == null || (Instance.Flow == FlowState.Playing && Instance.result == Result.Playing);

        public static event Action PlayBegan;
        public static event Action<FlowState> FlowChanged;

        public PlayerController player;
        public PlayerTools tools;
        [Tooltip("NavMesh 데이터가 비어 있으면 시작 시 다시 굽는다")]
        public NavMeshSurface[] surfaces;
        [Tooltip("데이터 코어 확보 후 탈출 지점까지 돌아가야 성공")]
        public bool requireExtraction = true;
        [Tooltip("이 시간 안에 끝내면 시간 감점 없음(초)")]
        public float parTime = 210f;
        public float resultDelay = 1.6f;

        public FlowState Flow { get; private set; } = FlowState.Title;
        public MissionPhase Phase { get; private set; } = MissionPhase.Infiltrate;
        public Result CurrentResult => result;
        public float Elapsed { get; private set; }
        /// <summary>경보(!) 발생 횟수(동시에 여러 명이 경보여도 한 번)</summary>
        public int Detections { get; private set; }
        public int Knockouts { get; private set; }
        public string CaughtBy { get; private set; }
        public bool HasData => Phase == MissionPhase.Extract || result == Result.Won;
        public bool StartedFromCheckpoint { get; private set; }
        public string Banner => bannerTimer > 0f ? banner : null;
        public float BannerTimer => bannerTimer;

        public string CurrentObjective
        {
            get
            {
                if (result == Result.Won) return "임무 완료";
                if (Phase == MissionPhase.Extract) return "탈출: 야드 남서쪽 시작 지점(녹색 표시)으로 복귀";
                return "목표: 북동쪽 서버실의 데이터 코어 확보";
            }
        }

        Result result = Result.Playing;
        string banner;
        float bannerTimer;
        float resultTimer = -1f;

        void Awake()
        {
            Instance = this;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            if (surfaces != null)
            {
                foreach (var s in surfaces)
                    if (s != null && s.navMeshData == null) s.BuildNavMesh();
            }
            GuardAI.StateChanged += OnGuardStateChanged;
        }

        void Start()
        {
            if (player == null) player = FindAnyObjectByType<PlayerController>();
            if (tools == null && player != null) tools = player.GetComponent<PlayerTools>();
            if (Session.Started) BeginPlay();
            else SetFlow(FlowState.Title);
        }

        void OnDestroy()
        {
            GuardAI.StateChanged -= OnGuardStateChanged;
            if (Instance == this) Instance = null;
        }

        void SetFlow(FlowState f)
        {
            Flow = f;
            bool playing = f == FlowState.Playing;
            if (player != null) player.InputEnabled = playing && result == Result.Playing;
            Time.timeScale = f == FlowState.Paused ? 0f : 1f;
            AudioListener.pause = f == FlowState.Paused;
            ThirdPersonCamera.LockCursor(playing && result == Result.Playing);
            FlowChanged?.Invoke(f);
        }

        // ---------------- 흐름 ----------------

        public void OpenBriefing() { if (Flow == FlowState.Title) SetFlow(FlowState.Briefing); }
        public void BackToTitleScreen() { if (Flow == FlowState.Briefing) SetFlow(FlowState.Title); }

        /// <summary>브리핑에서 작전 시작</summary>
        public void StartMission()
        {
            Session.Started = true;
            BeginPlay();
        }

        void BeginPlay()
        {
            StartedFromCheckpoint = false;
            if (CheckpointData.Has && player != null)
            {
                StartedFromCheckpoint = true;
                var cc = player.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                player.transform.SetPositionAndRotation(CheckpointData.Position, Quaternion.Euler(0f, CheckpointData.Yaw, 0f));
                if (cc != null) cc.enabled = true;
                if (CheckpointData.Extract) Phase = MissionPhase.Extract;
                Elapsed = CheckpointData.Elapsed;
                Detections = CheckpointData.Detections;
                Knockouts = CheckpointData.Knockouts;
                if (tools != null) tools.Throwables = CheckpointData.Throwables;
                ShowBanner("체크포인트에서 재시작: " + CheckpointData.Label, 3f);
            }
            PlayerBehaviorLog.Attempts++;
            SetFlow(FlowState.Playing);
            var tpc = FindAnyObjectByType<ThirdPersonCamera>();
            if (tpc != null && player != null) tpc.SnapBehindPlayer();
            PlayBegan?.Invoke();
            Debug.Log($"[ShadowProtocol] 시도 #{PlayerBehaviorLog.Attempts} 시작" + (StartedFromCheckpoint ? $" (체크포인트: {CheckpointData.Label})" : ""));
        }

        public void Pause()
        {
            if (Flow == FlowState.Playing && result == Result.Playing) SetFlow(FlowState.Paused);
        }

        public void Resume()
        {
            if (Flow == FlowState.Paused) SetFlow(FlowState.Playing);
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (bannerTimer > 0f) bannerTimer -= dt;

            bool pauseKey = Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P);
            switch (Flow)
            {
                case FlowState.Playing:
                    if (result == Result.Playing)
                    {
                        Elapsed += Time.deltaTime;
                        if (pauseKey)
                        {
                            if (GameUI.Instance != null && GameUI.Instance.PuzzleOpen) GameUI.Instance.ClosePuzzle();
                            else Pause();
                        }
                    }
                    if (Input.GetKeyDown(KeyCode.R)) Restart();
                    break;
                case FlowState.Paused:
                    if (pauseKey) Resume();
                    break;
                case FlowState.Result:
                    if (Input.GetKeyDown(KeyCode.R)) Restart();
                    break;
            }

            if (resultTimer >= 0f)
            {
                resultTimer -= dt;
                if (resultTimer < 0f) SetFlow(FlowState.Result);
            }
        }

        // ---------------- 판정 ----------------

        void OnGuardStateChanged(GuardAI g, GuardState prev, GuardState next)
        {
            if (result != Result.Playing || next != GuardState.Alert || prev == GuardState.Alert) return;
            foreach (var o in GuardAI.All)
                if (o != g && o.State == GuardState.Alert && !o.IsKnockedOut) return;
            Detections++;
        }

        public void RegisterKnockout() { Knockouts++; }

        /// <summary>경비 시야 외의 발각(레이저 경보 등)</summary>
        public void RegisterDetection(string reason)
        {
            if (result != Result.Playing) return;
            Detections++;
            ShowBanner("경보! " + reason, 2.5f);
        }

        /// <summary>퍼즐 힌트 등 시간 패널티</summary>
        public void AddTimePenalty(float seconds)
        {
            Elapsed += seconds;
            ShowBanner($"힌트 사용: 시간 +{seconds:0}초, 소음 발생", 2.5f);
        }

        public void PlayerCaught(GuardAI guard)
        {
            if (result != Result.Playing) return;
            result = Result.Lost;
            CaughtBy = guard != null ? guard.callSign : "경비병";
            if (player != null) player.InputEnabled = false;
            ThirdPersonCamera.LockCursor(false);
            resultTimer = resultDelay;
            ShowBanner("발각! " + CaughtBy + "에게 붙잡혔다", resultDelay + 0.5f);
            var am = AudioManager.Instance;
            if (am != null) am.PlayUI(am.failSting, 0.9f);
            Debug.Log($"[ShadowProtocol] 실패: {CaughtBy}에게 잡힘 ({Elapsed:0.0}s)");
        }

        public void PlayerReachedGoal()
        {
            if (result != Result.Playing || Phase == MissionPhase.Extract) return;
            var am = AudioManager.Instance;
            if (am != null) am.PlayUI(am.dataJingle, 0.9f);
            if (requireExtraction)
            {
                Phase = MissionPhase.Extract;
                ShowBanner("데이터 확보!  야드 남서쪽 시작 지점(녹색 표시)으로 탈출하라", 5f);
                Debug.Log($"[ShadowProtocol] 데이터 확보 → 탈출 단계 ({Elapsed:0.0}s)");
                return;
            }
            Win();
        }

        public void PlayerExtracted()
        {
            if (result != Result.Playing || Phase != MissionPhase.Extract) return;
            Win();
        }

        void Win()
        {
            result = Result.Won;
            if (player != null) player.InputEnabled = false;
            ThirdPersonCamera.LockCursor(false);
            resultTimer = resultDelay * 0.75f;
            ShowBanner("임무 성공", resultDelay);
            var am = AudioManager.Instance;
            if (am != null) am.PlayUI(am.winSting, 0.9f);
            Debug.Log($"[ShadowProtocol] 성공: 임무 완료 ({Elapsed:0.0}s, 발각 {Detections}, 기절 {Knockouts})");
        }

        public void ShowBanner(string text, float seconds)
        {
            banner = text;
            bannerTimer = seconds;
        }

        // ---------------- 등급 ----------------

        public int Score
        {
            get
            {
                float s = 100f - Detections * 22f - Knockouts * 4f;
                if (Elapsed > parTime) s -= Mathf.Min(30f, (Elapsed - parTime) / 6f);
                return Mathf.Clamp(Mathf.RoundToInt(s), 0, 100);
            }
        }

        public string Grade
        {
            get
            {
                if (result != Result.Won) return "-";
                int s = Score;
                if (s >= 90 && Detections == 0) return "S";
                if (s >= 72) return "A";
                if (s >= 50) return "B";
                return "C";
            }
        }

        // ---------------- 재시작 ----------------

        /// <summary>R 키·실패 화면: 체크포인트가 있으면 거기서, 없으면 처음부터(적응 AI 기록은 유지)</summary>
        public void Restart()
        {
            if (result == Result.Won) CheckpointData.Clear();
            else if (!CheckpointData.Has) PuzzleState.NewRun();
            Session.Started = true;
            Reload();
        }

        public void RestartFromBeginning()
        {
            CheckpointData.Clear();
            Session.Started = true;
            Reload();
        }

        public void QuitToTitle()
        {
            CheckpointData.Clear();
            Session.Started = false;
            Reload();
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void Reload()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            var scene = SceneManager.GetActiveScene();
            if (scene.buildIndex >= 0) SceneManager.LoadScene(scene.buildIndex);
            else SceneManager.LoadScene(scene.name);
        }

        /// <summary>체크포인트 저장(CheckpointZone이 호출)</summary>
        public void SaveCheckpoint(int key, string label)
        {
            if (player == null) return;
            CheckpointData.Has = true;
            CheckpointData.Index = key;
            CheckpointData.Label = label;
            CheckpointData.Position = player.transform.position;
            CheckpointData.Yaw = player.transform.eulerAngles.y;
            CheckpointData.Throwables = tools != null ? tools.Throwables : 0;
            CheckpointData.Extract = Phase == MissionPhase.Extract;
            CheckpointData.Elapsed = Elapsed;
            CheckpointData.Detections = Detections;
            CheckpointData.Knockouts = Knockouts;
            CheckpointData.SavedKeys.Add(key);
            ShowBanner("체크포인트 저장: " + label, 2.5f);
            var am = AudioManager.Instance;
            if (am != null) am.PlayUI(am.checkpoint, 0.7f);
            Debug.Log($"[ShadowProtocol] 체크포인트 저장: {label} ({player.transform.position.x:0.0}, {player.transform.position.z:0.0})");
        }
    }
}
