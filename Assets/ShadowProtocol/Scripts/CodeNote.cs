using UnityEngine;

namespace ShadowProtocol
{
    /// <summary>
    /// 보안 코드 조각(쪽지·CCTV 모니터 메모·화이트보드). E로 읽으면 색과 숫자 하나를 알게 된다.
    /// 숫자는 판마다 무작위(PuzzleState).
    /// </summary>
    public class CodeNote : PuzzleInteractable
    {
        [Range(0, 3)] public int color;
        public string place = "쪽지";
        public TextMesh display;
        public Renderer marker;

        public bool IsRead => PuzzleState.NotesRead.Contains(color);
        public override string PromptText => $"[E] {place} 살펴보기";

        void Start()
        {
            if (display != null) display.text = PuzzleState.Icons[color] + " " + PuzzleState.DigitOf(color);
            if (display != null) display.color = PuzzleState.Colors[color];
        }

        void Update()
        {
            if (marker != null) marker.enabled = !IsRead;
            // 멀리서는 색 기호만 보이고, 가까이 가서 읽어야 숫자를 알 수 있다
            // 글자는 벽 너머로 비치지 않게 가까이(6m)에서만 보인다
            var gmi = GameManager.Instance;
            if (display != null && gmi != null && gmi.player != null)
            {
                bool near = (gmi.player.transform.position - transform.position).sqrMagnitude < 36f;
                if (display.gameObject.activeSelf != near) display.gameObject.SetActive(near);
            }
            if (display != null) display.text = PuzzleState.Icons[color] + (IsRead ? " " + PuzzleState.DigitOf(color) : " ?");
        }

        public override void Interact(PlayerTools tools) { Read(); }

        public void Read()
        {
            bool first = PuzzleState.NotesRead.Add(color);
            var gm = GameManager.Instance;
            string txt = $"{place}: <color=#{PuzzleState.ColorHex[color]}>{PuzzleState.Icons[color]} {PuzzleState.ColorNames[color]} = {PuzzleState.DigitOf(color)}</color>  ({PuzzleState.NotesRead.Count}/4 조각)";
            if (gm != null) gm.ShowBanner(txt, 4f);
            var am = AudioManager.Instance;
            if (am != null && first) am.PlayUI(am.pickup, 0.6f);
            if (first)
            {
                if (GuardDirector.Instance != null) GuardDirector.Instance.RecordPuzzleUse(puzzleId, puzzleLabel);
                Debug.Log($"[ShadowProtocol] 코드 조각 읽음: {PuzzleState.ColorNames[color]}={PuzzleState.DigitOf(color)} ({place})");
            }
        }
    }
}
