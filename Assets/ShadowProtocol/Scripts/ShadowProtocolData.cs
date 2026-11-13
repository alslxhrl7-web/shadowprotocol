using System.Collections.Generic;
using UnityEngine;

namespace ShadowProtocol
{
    /// <summary>플레이어 설정(PlayerPrefs 저장).</summary>
    public static class GameSettings
    {
        public static event System.Action Changed;

        static bool loaded;
        static float sens, music, sfx, fov;
        static bool invertY;

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            sens = PlayerPrefs.GetFloat("sp.sens", 2.2f);
            invertY = PlayerPrefs.GetInt("sp.invy", 0) == 1;
            music = PlayerPrefs.GetFloat("sp.music", 0.6f);
            sfx = PlayerPrefs.GetFloat("sp.sfx", 0.85f);
            fov = PlayerPrefs.GetFloat("sp.fov", 60f);
        }

        public static float MouseSensitivity { get { Load(); return sens; } set { Load(); sens = value; PlayerPrefs.SetFloat("sp.sens", value); Changed?.Invoke(); } }
        public static bool InvertY { get { Load(); return invertY; } set { Load(); invertY = value; PlayerPrefs.SetInt("sp.invy", value ? 1 : 0); Changed?.Invoke(); } }
        public static float MusicVolume { get { Load(); return music; } set { Load(); music = value; PlayerPrefs.SetFloat("sp.music", value); Changed?.Invoke(); } }
        public static float SfxVolume { get { Load(); return sfx; } set { Load(); sfx = value; PlayerPrefs.SetFloat("sp.sfx", value); Changed?.Invoke(); } }
        public static float Fov { get { Load(); return fov; } set { Load(); fov = value; PlayerPrefs.SetFloat("sp.fov", value); Changed?.Invoke(); } }
    }

    /// <summary>한 번 실행하는 동안 유지되는 상태(씬을 다시 불러와도 남는다).</summary>
    public static class Session
    {
        /// <summary>타이틀을 지나 임무를 시작한 적이 있으면 true(재시작 시 타이틀을 건너뛴다)</summary>
        public static bool Started;
        public static readonly HashSet<string> TutorialShown = new HashSet<string>();
    }

    /// <summary>마지막 체크포인트(실패 시 여기서 다시 시작).</summary>
    public static class CheckpointData
    {
        public static bool Has;
        public static int Index = -1;
        public static string Label;
        public static Vector3 Position;
        public static float Yaw;
        public static int Throwables = 1;
        public static bool Extract;
        public static float Elapsed;
        public static int Detections, Knockouts;
        public static readonly HashSet<int> SavedKeys = new HashSet<int>();

        public static void Clear()
        {
            Has = false; Index = -1; Label = null; Throwables = 1; Extract = false;
            Elapsed = 0f; Detections = 0; Knockouts = 0;
            SavedKeys.Clear();
            PuzzleState.NewRun();
        }
    }

    /// <summary>
    /// 퍼즐 상태. 보안 코드(4자리)와 색 순서는 판마다 무작위(처음부터 다시 시작하면 새로 만든다).
    /// 체크포인트 재시작에서는 같은 코드·읽은 쪽지를 유지한다.
    /// </summary>
    public static class PuzzleState
    {
        public static readonly string[] ColorNames = { "빨강", "파랑", "초록", "노랑" };
        public static readonly string[] ColorHex = { "FF5A4D", "4DA6FF", "5AE07A", "FFD24D" };
        public static readonly Color[] Colors = { new Color(1f, 0.35f, 0.3f), new Color(0.3f, 0.65f, 1f), new Color(0.35f, 0.88f, 0.48f), new Color(1f, 0.82f, 0.3f) };
        public static readonly string[] Icons = { "▲", "●", "■", "★" };

        static bool made;
        static int[] digitByColor = new int[4];
        static int[] colorOrder = new int[4];
        public static readonly HashSet<int> NotesRead = new HashSet<int>();
        static int seed;
        public static int Seed { get { Ensure(); return seed; } }

        public static void NewRun()
        {
            made = false;
            NotesRead.Clear();
        }

        static void Ensure()
        {
            if (made) return;
            made = true;
            seed = System.Environment.TickCount ^ UnityEngine.Random.Range(0, int.MaxValue);
            var rng = new System.Random(seed);
            for (int i = 0; i < 4; i++) { digitByColor[i] = rng.Next(0, 10); colorOrder[i] = i; }
            for (int i = 3; i > 0; i--) { int j = rng.Next(0, i + 1); int t = colorOrder[i]; colorOrder[i] = colorOrder[j]; colorOrder[j] = t; }
        }

        /// <summary>색(쪽지) 별 숫자</summary>
        public static int DigitOf(int color) { Ensure(); return digitByColor[color]; }
        /// <summary>키패드에 표시되는 색 순서(자리 i의 색)</summary>
        public static int ColorAt(int slot) { Ensure(); return colorOrder[slot]; }
        public static string Code
        {
            get { Ensure(); var s = ""; for (int i = 0; i < 4; i++) s += digitByColor[colorOrder[i]]; return s; }
        }
    }

    /// <summary>
    /// 적응형 경비 AI가 학습하는 플레이어 행동 기록. 재시작·체크포인트 재시작에도 유지된다.
    /// </summary>
    public static class PlayerBehaviorLog
    {
        public const float CellSize = 3f;
        public static readonly Dictionary<Vector2Int, float> Heat = new Dictionary<Vector2Int, float>();
        public static readonly Dictionary<string, int> RouteUses = new Dictionary<string, int>();
        public static readonly Dictionary<string, float> CoverSeconds = new Dictionary<string, float>();
        public static readonly Dictionary<string, Vector3> CoverHidePos = new Dictionary<string, Vector3>();
        /// <summary>적용된 적응(다음 시도에도 다시 적용)</summary>
        public static readonly List<string> Applied = new List<string>();
        /// <summary>사람이 읽는 적응 기록</summary>
        public static readonly List<string> History = new List<string>();
        public static int Attempts;

        public static Vector2Int Cell(Vector3 p) => new Vector2Int(Mathf.FloorToInt(p.x / CellSize), Mathf.FloorToInt(p.z / CellSize));
        public static Vector3 CellCenter(Vector2Int c) => new Vector3((c.x + 0.5f) * CellSize, 0f, (c.y + 0.5f) * CellSize);

        public static int Uses(string route) => RouteUses.TryGetValue(route, out int n) ? n : 0;

        public static void Reset()
        {
            Heat.Clear(); RouteUses.Clear(); CoverSeconds.Clear(); CoverHidePos.Clear();
            Applied.Clear(); History.Clear(); Attempts = 0;
        }
    }
}
