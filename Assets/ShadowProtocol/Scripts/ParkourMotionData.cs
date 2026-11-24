using UnityEngine;

namespace ShadowProtocol
{
    /// <summary>
    /// Mixamo 파쿠르 클립에서 뽑은 이동 곡선(ShadowProtocolMixamoBake가 만든다).
    /// 클립의 골반이 앞으로 간 거리(m)를 시간(초)별로 저장해, ParkourController가 캐릭터를 그대로 움직이게 한다(발 미끄러짐 없음).
    /// </summary>
    public class ParkourMotionData : ScriptableObject
    {
        [Header("뛰어넘기")]
        public float vaultLength;
        public AnimationCurve vaultForward = new AnimationCurve();
        public AnimationCurve vaultHipHeight = new AnimationCurve();
        [Tooltip("쓰는 구간(초): 디딤 ~ 착지 후 다시 달리기")]
        public float vaultStart, vaultEnd;
        [Tooltip("골반이 가장 높은 시점(초) = 장애물 위")]
        public float vaultApex;

        [Header("슬라이딩")]
        public float slideLength;
        public AnimationCurve slideForward = new AnimationCurve();
        public AnimationCurve slideHipHeight = new AnimationCurve();
        public float slideStart, slideEnd;

        public float VaultDistance(float a, float b) => vaultForward.Evaluate(b) - vaultForward.Evaluate(a);
        public float SlideDistance(float a, float b) => slideForward.Evaluate(b) - slideForward.Evaluate(a);
    }
}
