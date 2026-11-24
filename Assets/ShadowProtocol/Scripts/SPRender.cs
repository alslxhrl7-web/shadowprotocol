using UnityEngine;

namespace ShadowProtocol
{
    /// <summary>
    /// 재질 색·발광을 렌더 파이프라인과 상관없이 바꾸는 도우미.
    /// HDRP/Lit는 _BaseColor·_EmissiveColor(선형 HDR, nits), Standard는 _Color·_EmissionColor를 쓴다.
    /// 발광 값은 예전 Standard 기준(색 × 세기)으로 넘기면 nits로 바꿔 준다.
    /// </summary>
    public static class SPRender
    {
        /// <summary>예전 Standard 발광 세기 1 = 이 nits. 밤 자동 노출(EV 약 1~4)에서 표시등이 또렷하되 하얗게 타지 않는 값.</summary>
        public const float NitsPerEmissionUnit = 12f;

        public static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        public static readonly int ColorId = Shader.PropertyToID("_Color");
        public static readonly int EmissiveColorId = Shader.PropertyToID("_EmissiveColor");
        public static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        /// <summary>이 재질의 주 색 속성(HDRP면 _BaseColor)</summary>
        public static int MainColorId(Material m) => m != null && m.HasProperty(BaseColorId) ? BaseColorId : ColorId;

        /// <summary>예전 Standard 발광 값(색 × 세기) → HDRP _EmissiveColor(선형 × nits)</summary>
        public static Color EmissionHDR(Color legacy)
        {
            float s = Mathf.Max(legacy.r, Mathf.Max(legacy.g, legacy.b));
            if (s <= 0.0001f) return Color.black;
            var ldr = new Color(legacy.r / s, legacy.g / s, legacy.b / s, 1f);
            return ldr.linear * (s * NitsPerEmissionUnit);
        }

        public static void SetColor(Material m, Color c)
        {
            if (m == null) return;
            if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, c);
            if (m.HasProperty(ColorId)) m.SetColor(ColorId, c);
        }

        public static void SetEmission(Material m, Color legacy)
        {
            if (m == null) return;
            if (m.HasProperty(EmissiveColorId)) m.SetColor(EmissiveColorId, EmissionHDR(legacy));
            else if (m.HasProperty(EmissionColorId)) m.SetColor(EmissionColorId, legacy);
        }

        /// <summary>MaterialPropertyBlock용: reference 재질의 셰이더 종류에 맞는 이름으로 쓴다</summary>
        public static void SetColor(MaterialPropertyBlock mpb, Material reference, Color c)
        {
            mpb.SetColor(MainColorId(reference), c);
        }

        public static void SetEmission(MaterialPropertyBlock mpb, Material reference, Color legacy)
        {
            if (reference != null && reference.HasProperty(EmissiveColorId)) mpb.SetColor(EmissiveColorId, EmissionHDR(legacy));
            else mpb.SetColor(EmissionColorId, legacy);
        }
    }
}
