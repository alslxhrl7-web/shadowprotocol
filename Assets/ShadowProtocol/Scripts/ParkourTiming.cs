namespace ShadowProtocol
{
    /// <summary>
    /// Mixamo 파쿠르 클립에서 실제로 쓰는 구간. 애니메이터 빌더(전환 offset·exitTime, 정규화)와 ParkourController(초)가 같이 쓴다.
    /// 2026-11-23 실측(ShadowProtocolMixamoBake, 골반 높이·전진 거리 곡선):
    /// - Vault Over Box 3.53s: 0~0.3 도움닫기, 0.77 골반 최고 1.45m(상자 위), 1.5 착지, 1.6 이후 멈춰 웅크림(2.7s 골반 0.50m)
    ///   → 0.30~1.65s 사용(전진 2.16m)
    /// - Running Slide 1.53s: 0.3s 골반 0.24m까지 내려가 미끄러짐, 1.5s 다시 달리기 높이(0.90m), 전진 6.2m(처음 약 5.2 m/s)
    ///   → 0.05~1.45s 사용
    /// </summary>
    public static class ShadowProtocolParkourTiming
    {
        public const float VaultClipLength = 3.533f;
        public const float VaultStartSec = 0.30f;
        public const float VaultEndSec = 1.65f;
        public const float VaultStart = VaultStartSec / VaultClipLength;
        public const float VaultEnd = VaultEndSec / VaultClipLength;

        public const float SlideClipLength = 1.533f;
        public const float SlideStartSec = 0.05f;
        public const float SlideEndSec = 1.45f;
        public const float SlideStart = SlideStartSec / SlideClipLength;
        public const float SlideEnd = SlideEndSec / SlideClipLength;

        /// <summary>클립의 상자 높이 추정(골반 최고 1.45m − 서 있을 때 0.84m ≈ 0.6m 들림 → 상자 약 1.0m). 이보다 높은 장애물은 몸을 더 들어 올린다.</summary>
        public const float VaultClipObstacleHeight = 1.0f;
    }
}
