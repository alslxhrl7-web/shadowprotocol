using UnityEngine;

namespace ShadowProtocol
{
    /// <summary>
    /// 상황별 도움말(야드에서 이동·웅크리기·숨기·던지기·제압). 한 번 본 도움말은 다시 띄우지 않는다(재시작 포함).
    /// 경비 머리 위 아이콘 설명은 경비를 처음 화면에서 봤을 때 한 번.
    /// </summary>
    public class TutorialDirector : MonoBehaviour
    {
        public PlayerController player;
        public PlayerTools tools;
        public float hintSeconds = 7f;

        float playTime, cooldown;

        void Start()
        {
            if (player == null) player = FindAnyObjectByType<PlayerController>();
            if (tools == null && player != null) tools = player.GetComponent<PlayerTools>();
        }

        bool Try(string key, string text)
        {
            if (Session.TutorialShown.Contains(key) || cooldown > 0f || GameUI.Instance == null) return false;
            Session.TutorialShown.Add(key);
            GameUI.Instance.ShowTutorial(text, hintSeconds);
            cooldown = hintSeconds + 1.5f;
            return true;
        }

        void Update()
        {
            if (!GameManager.GameplayActive || player == null) return;
            float dt = Time.deltaTime;
            playTime += dt;
            cooldown -= dt;
            if (cooldown > 0f) return;

            if (playTime > 1f && Try("move", "<b>이동</b>  WASD 이동 · 마우스로 시점 · 휠 카메라 거리\nShift 달리기(빠르지만 발소리가 크다)  ·  P/Esc 일시정지")) return;

            GuardAI nearest = null;
            float nd = float.MaxValue;
            foreach (var g in GuardAI.All)
            {
                if (g == null || g.IsKnockedOut) continue;
                float d = Vector3.Distance(g.transform.position, player.transform.position);
                if (d < nd) { nd = d; nearest = g; }
            }

            // 경비 아이콘 설명: 경비가 화면에 처음 보일 때
            var cam = Camera.main;
            if (nearest != null && cam != null && nd < 28f && !Session.TutorialShown.Contains("icons"))
            {
                Vector3 vp = cam.WorldToViewportPoint(nearest.transform.position + Vector3.up * 1.5f);
                bool onScreen = vp.z > 0f && vp.x > 0.1f && vp.x < 0.9f && vp.y > 0.1f && vp.y < 0.9f;
                bool visible = false;
                if (onScreen)
                {
                    if (!Physics.Linecast(cam.transform.position, nearest.EyePosition, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore)) visible = true;
                    else visible = hit.collider.GetComponentInParent<GuardAI>() == nearest;
                }
                if (visible)
                {
                    if (Try("icons", "<b>경비 표시</b>  머리 위 구슬 <color=#59F273>초록=순찰</color> · <color=#FFD933>? 의심</color> · <color=#FF8C26>?? 수색</color> · <color=#FF3333>! 경보</color>\n바닥의 부채꼴이 시야. 화면 밖에서 경계 중인 경비는 가장자리에 방향이 표시된다.")) return;
                }
            }

            if (playTime > 6f && nd < 20f && Try("crouch", "<b>웅크리기</b>  Ctrl 또는 C\n발소리가 나지 않고, 시야에 잘 걸리지 않는다. 환기구·덕트는 웅크려야 지나간다.")) return;

            float maxSusp = 0f;
            foreach (var g in GuardAI.All) if (!g.IsKnockedOut) maxSusp = Mathf.Max(maxSusp, g.Suspicion);
            if ((maxSusp > 0.12f || (player.Brightness < 0.5f && playTime > 14f)) &&
                Try("hide", "<b>숨기</b>  위쪽 탐지 게이지가 차면 들킨다.\n어두운 곳·컨테이너와 상자 뒤에 웅크리면 게이지가 천천히 오르고, 시야에서 벗어나면 줄어든다.")) return;

            if (tools != null && tools.Throwables > 0 && playTime > 10f && nd < 18f &&
                Try("throw", "<b>던지기</b>  우클릭 조준(포물선 표시) → 좌클릭 던지기 · G 빠른 던지기\n떨어진 곳에서 소리가 나 경비가 확인하러 간다. 병·캔은 바닥에서 주울 수 있다.")) return;

            if (nearest != null && nd < 5f)
            {
                Vector3 toPlayer = player.transform.position - nearest.transform.position;
                toPlayer.y = 0f;
                if (Vector3.Angle(nearest.transform.forward, toPlayer) > 100f &&
                    Try("takedown", "<b>제압</b>  눈치채지 못한 경비의 등 뒤에서 [E]\n[F]로 기절한 경비를 끌어 숨길 수 있다. 쓰러진 동료를 발견하거나 무전에 응답이 없으면 경비가 확인하러 온다.")) return;
            }
        }
    }
}
