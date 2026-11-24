using UnityEngine;

namespace ShadowProtocol
{
    /// <summary>
    /// 배전반(3×3 Lights Out): 스위치를 누르면 자신과 상하좌우가 뒤집힌다. 모두 켜면(초록) 풀린다.
    /// 풀면 덕트 환풍기가 멈춰 덕트를 지날 수 있고, 창고·서버실 조명이 꺼져 어두워진다(경비 시야 0.6배).
    /// 푸는 동안 플레이어는 서 있어야 해서 노출된다. 힌트: 정답 스위치 하나 표시(시간 +15초, 소음).
    /// </summary>
    public class BreakerPanel : PuzzleInteractable
    {
        public DuctFan fan;
        public Light[] lightsOff;
        public LightZone[] zonesDarken;
        public float darkBrightness = 0.25f;
        public float hintNoise = 5f;
        public float hintTimePenalty = 15f;
        public Renderer lamp;

        public bool[] Cells { get; private set; } = new bool[9];
        public bool Solved { get; private set; }
        public int Presses { get; private set; }
        bool[] solution = new bool[9];
        public override bool Available => !Solved;
        public override string PromptText => "[E] 배전반";

        void Start()
        {
            // 다 켜진 상태에서 무작위 4~5번 눌러 섞는다 → 그 누름들이 곧 정답(같은 칸을 두 번 누르면 상쇄)
            var rng = new System.Random(PuzzleState.Seed ^ 0x5A17);
            for (int i = 0; i < 9; i++) Cells[i] = true;
            int n = 4 + rng.Next(0, 2);
            for (int k = 0; k < n; k++)
            {
                int c = rng.Next(0, 9);
                Toggle(c);
                solution[c] = !solution[c];
            }
            if (AllOn()) { Toggle(4); solution[4] = !solution[4]; }
            UpdateLamp();
        }

        void Toggle(int i)
        {
            int x = i % 3, y = i / 3;
            Cells[i] = !Cells[i];
            if (x > 0) Cells[i - 1] = !Cells[i - 1];
            if (x < 2) Cells[i + 1] = !Cells[i + 1];
            if (y > 0) Cells[i - 3] = !Cells[i - 3];
            if (y < 2) Cells[i + 3] = !Cells[i + 3];
        }

        bool AllOn()
        {
            foreach (var c in Cells) if (!c) return false;
            return true;
        }

        public override void Interact(PlayerTools tools)
        {
            if (GameUI.Instance != null) GameUI.Instance.OpenBreaker(this);
        }

        /// <summary>스위치 누르기(UI·봇 공용)</summary>
        public void Press(int i)
        {
            if (Solved || i < 0 || i > 8) return;
            Toggle(i);
            solution[i] = !solution[i];
            Presses++;
            var am = AudioManager.Instance;
            if (am != null) am.PlayAt(am.uiClick, transform.position, 0.9f, 1f, 8f, 0.7f);
            if (AllOn()) Solve();
        }

        /// <summary>정답 스위치 목록(봇·힌트용)</summary>
        public int[] SolutionCells()
        {
            var list = new System.Collections.Generic.List<int>();
            for (int i = 0; i < 9; i++) if (solution[i]) list.Add(i);
            return list.ToArray();
        }

        public int Hint()
        {
            var s = SolutionCells();
            if (s.Length == 0) return -1;
            MakeNoise(hintNoise);
            if (GameManager.Instance != null) GameManager.Instance.AddTimePenalty(hintTimePenalty);
            return s[0];
        }

        void Solve()
        {
            Solved = true;
            if (fan != null) fan.Stop();
            if (lightsOff != null) foreach (var l in lightsOff) if (l != null) l.enabled = false;
            if (zonesDarken != null) foreach (var z in zonesDarken) if (z != null) z.brightness = Mathf.Min(z.brightness, darkBrightness);
            UpdateLamp();
            var am = AudioManager.Instance;
            if (am != null) am.PlayAt(am.checkpoint, transform.position, 0.8f, 1f, 10f, 0.8f);
            if (GuardDirector.Instance != null) GuardDirector.Instance.RecordPuzzleUse(puzzleId, puzzleLabel);
            if (GameManager.Instance != null) GameManager.Instance.ShowBanner("배전반 차단: 덕트 환풍기 정지 · 창고·서버실 조명 꺼짐", 3.5f);
            Debug.Log($"[ShadowProtocol] 배전반 해제 ({Presses}번 누름)");
        }

        void UpdateLamp()
        {
            if (lamp == null) return;
            var mpb = new MaterialPropertyBlock();
            Color c = Solved ? new Color(0.3f, 1f, 0.45f) : new Color(1f, 0.6f, 0.1f);
            lamp.GetPropertyBlock(mpb);
            SPRender.SetColor(mpb, lamp.sharedMaterial, c);
            SPRender.SetEmission(mpb, lamp.sharedMaterial, c * 2f);
            lamp.SetPropertyBlock(mpb);
        }
    }
}
