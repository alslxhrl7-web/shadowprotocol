using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShadowProtocol
{
    /// <summary>
    /// 카메라와 플레이어 사이를 가리는 큰 소품(컨테이너·지게차·선반·사물함·서버)을 반투명하게 한다.
    /// 3인칭 카메라는 이 소품에 부딪혀 당겨지지 않고 통과한다. 미리 만든 Fade 재질(같은 텍스처)로 바꾸고
    /// MaterialPropertyBlock으로 알파만 조절한다(재질 인스턴스를 만들지 않음).
    /// </summary>
    public class CameraFadeable : MonoBehaviour
    {
        public static readonly List<CameraFadeable> All = new List<CameraFadeable>();
        static readonly int ColorId = Shader.PropertyToID("_Color");

        [Serializable]
        public class Slot
        {
            public Renderer renderer;
            public Material[] opaque;
            public Material[] fade;
        }

        public Slot[] slots;
        public float fadedAlpha = 0.28f;
        [Tooltip("카메라가 이 소품의 콜라이더에 부딪혀 당겨지지 않게 한다")]
        public bool cameraPassThrough = true;

        public Bounds WorldBounds { get; private set; }
        public float Alpha => alpha;

        float alpha = 1f, target = 1f;
        bool faded;
        MaterialPropertyBlock mpb;
        readonly HashSet<Collider> colliders = new HashSet<Collider>();

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        void Start()
        {
            mpb = new MaterialPropertyBlock();
            RecomputeBounds();
            foreach (var c in GetComponentsInChildren<Collider>()) colliders.Add(c);
        }

        public void RecomputeBounds()
        {
            bool has = false;
            var b = new Bounds(transform.position, Vector3.zero);
            if (slots != null)
                foreach (var s in slots)
                {
                    if (s == null || s.renderer == null) continue;
                    if (!has) { b = s.renderer.bounds; has = true; }
                    else b.Encapsulate(s.renderer.bounds);
                }
            WorldBounds = b;
        }

        public bool Owns(Collider c) => colliders.Contains(c);

        /// <summary>이번 프레임 목표 알파(카메라가 매 프레임 호출)</summary>
        public void SetTarget(float a) { target = a; }

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            alpha = Mathf.MoveTowards(alpha, target, dt * 4f);
            target = 1f; // 다음 프레임에 다시 요청하지 않으면 원래대로
            bool want = alpha < 0.995f;
            if (want != faded)
            {
                faded = want;
                foreach (var s in slots)
                {
                    if (s == null || s.renderer == null) continue;
                    s.renderer.sharedMaterials = want && s.fade != null && s.fade.Length == s.opaque.Length ? s.fade : s.opaque;
                    if (!want) s.renderer.SetPropertyBlock(null);
                }
            }
            if (!faded) return;
            foreach (var s in slots)
            {
                if (s == null || s.renderer == null || s.fade == null) continue;
                for (int i = 0; i < s.fade.Length; i++)
                {
                    var m = s.fade[i];
                    if (m == null || !m.HasProperty(ColorId)) continue;
                    Color c = m.GetColor(ColorId);
                    c.a = alpha;
                    s.renderer.GetPropertyBlock(mpb, i);
                    mpb.SetColor(ColorId, c);
                    s.renderer.SetPropertyBlock(mpb, i);
                }
            }
        }
    }
}
