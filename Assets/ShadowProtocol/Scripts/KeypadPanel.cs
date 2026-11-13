using UnityEngine;

namespace ShadowProtocol
{
    /// <summary>
    /// 서버실 보안문 키패드. 사무동에 흩어진 코드 조각(색별 숫자)을 키패드의 색 순서대로 입력한다.
    /// 오답: 경고음 + 소음(근처 경비 의심), 3회 연속 오답이면 10초 잠금. 힌트: 아직 모르는 조각 하나 공개(시간 +20초, 작은 소음).
    /// </summary>
    public class KeypadPanel : PuzzleInteractable
    {
        public SecurityDoor[] doors;
        public float wrongNoise = 9f;
        public float hintNoise = 5f;
        public float hintTimePenalty = 20f;
        public Renderer screen;

        public int WrongStreak { get; private set; }
        public float LockedUntil { get; private set; }
        public bool Solved => doors != null && doors.Length > 0 && !doors[0].locked;
        public override bool Available => !Solved;
        public override string PromptText => Time.time < LockedUntil ? $"키패드 잠김 ({LockedUntil - Time.time:0}초)" : "[E] 보안 키패드";

        public override void Interact(PlayerTools tools)
        {
            if (Time.time < LockedUntil) return;
            if (GameUI.Instance != null) GameUI.Instance.OpenKeypad(this);
        }

        /// <summary>코드 제출(UI·봇 공용). 맞으면 문이 열린다.</summary>
        public bool Submit(string code)
        {
            if (Solved) return true;
            if (Time.time < LockedUntil) return false;
            var am = AudioManager.Instance;
            if (code == PuzzleState.Code)
            {
                foreach (var d in doors) if (d != null) d.Unlock();
                WrongStreak = 0;
                if (am != null) am.PlayAt(am.checkpoint, transform.position, 0.9f, 1f, 10f);
                if (GuardDirector.Instance != null) GuardDirector.Instance.RecordPuzzleUse(puzzleId, puzzleLabel);
                if (GameManager.Instance != null) GameManager.Instance.ShowBanner("보안문 해제", 2.5f);
                Debug.Log($"[ShadowProtocol] 키패드 해제: {name} 코드 {code}");
                return true;
            }
            WrongStreak++;
            MakeNoise(wrongNoise);
            if (am != null) am.PlayAt(am.guardSuspicious, transform.position, 1f, 2f, 18f, 0.6f);
            if (WrongStreak >= 3) { LockedUntil = Time.time + 10f; WrongStreak = 0; }
            Debug.Log($"[ShadowProtocol] 키패드 오답: {code} (정답 아님) — 경보음, 소음 반경 {wrongNoise}m");
            return false;
        }

        /// <summary>힌트: 모르는 조각 하나를 알려준다(시간 패널티·소음)</summary>
        public string Hint()
        {
            for (int slot = 0; slot < 4; slot++)
            {
                int c = PuzzleState.ColorAt(slot);
                if (PuzzleState.NotesRead.Contains(c)) continue;
                PuzzleState.NotesRead.Add(c);
                MakeNoise(hintNoise);
                if (GameManager.Instance != null) GameManager.Instance.AddTimePenalty(hintTimePenalty);
                return $"{PuzzleState.ColorNames[c]} = {PuzzleState.DigitOf(c)}";
            }
            return null;
        }
    }
}
