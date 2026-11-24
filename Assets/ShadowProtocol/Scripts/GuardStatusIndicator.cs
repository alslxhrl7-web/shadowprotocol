using UnityEngine;

namespace ShadowProtocol
{
    /// <summary>
    /// 경비병 머리 위 상태 표시: 색 구슬, ?/! 글자, 의심 게이지 막대.
    /// </summary>
    public class GuardStatusIndicator : MonoBehaviour
    {
        public GuardAI guard;
        public TextMesh label;
        public Renderer orb;
        public Transform bar;
        public Renderer barRenderer;
        public float barWidth = 1f;

        Transform hips;
        Camera cam;
        Material orbMat;
        Material barMat;
        Vector3 labelBaseScale = Vector3.one;

        void Start()
        {
            if (guard == null) guard = GetComponent<GuardAI>();
            cam = Camera.main;
            if (orb != null) orbMat = orb.material;
            if (barRenderer != null) barMat = barRenderer.material;
            if (label != null) labelBaseScale = label.transform.localScale;
        }

        void LateUpdate()
        {
            if (guard == null) return;
            if (cam == null) cam = Camera.main;

            if (guard.IsKnockedOut && !guard.ShowDown)
            {
                // 암살 연출: 타격 순간까지는 표시를 숨긴다
                if (label != null) label.text = "";
                if (orb != null) orb.enabled = false;
                if (bar != null) bar.gameObject.SetActive(false);
                return;
            }
            if (guard.IsKnockedOut)
            {
                // 기절: 회색 구슬 + Zz
                Color ko = new Color(0.55f, 0.58f, 0.65f);
                if (label != null)
                {
                    label.text = "Zz";
                    label.color = ko;
                    label.transform.localScale = labelBaseScale * 0.6f;
                    // 쓰러진 몸(골반) 바로 위에 표시
                    if (hips == null) { var an = guard.GetComponentInChildren<Animator>(); if (an != null && an.isHuman) hips = an.GetBoneTransform(HumanBodyBones.Hips); }
                    label.transform.position = (hips != null ? hips.position : guard.transform.position) + Vector3.up * 0.75f;
                    if (cam != null) label.transform.rotation = cam.transform.rotation;
                }
                if (orb != null) orb.enabled = false;
                if (orbMat != null) { SPRender.SetColor(orbMat, ko); SPRender.SetEmission(orbMat, ko * 0.3f); }
                if (bar != null) bar.gameObject.SetActive(false);
                return;
            }

            GuardState s = guard.State;
            Color c = GuardAI.ColorFor(s);
            if (s == GuardState.Patrol && guard.Suspicion > 0.01f)
                c = Color.Lerp(c, GuardAI.ColorFor(GuardState.Suspicious), guard.Suspicion * 2f);

            if (label != null)
            {
                string text;
                switch (s)
                {
                    case GuardState.Suspicious: text = "?"; break;
                    case GuardState.Search: text = "??"; break;
                    case GuardState.Alert: text = "!"; break;
                    default: text = guard.Suspicion > 0.02f ? "?" : ""; break;
                }
                label.text = text;
                label.color = c;
                float pulse = s == GuardState.Alert ? 1f + 0.15f * Mathf.Sin(Time.time * 12f) : 1f;
                label.transform.localScale = labelBaseScale * (0.8f + 0.5f * guard.Suspicion) * pulse;
                if (cam != null) label.transform.rotation = cam.transform.rotation;
            }

            if (orbMat != null)
            {
                SPRender.SetColor(orbMat, c);
                SPRender.SetEmission(orbMat, c * 1.5f);
            }

            if (bar != null)
            {
                float w = Mathf.Max(0.001f, guard.Suspicion * barWidth);
                Vector3 sc = bar.localScale;
                bar.localScale = new Vector3(w, sc.y, sc.z);
                if (cam != null) bar.rotation = cam.transform.rotation;
                bar.gameObject.SetActive(guard.Suspicion > 0.01f && s != GuardState.Alert);
                if (barMat != null)
                {
                    SPRender.SetColor(barMat, c);
                    SPRender.SetEmission(barMat, c);
                }
            }
        }
    }
}
