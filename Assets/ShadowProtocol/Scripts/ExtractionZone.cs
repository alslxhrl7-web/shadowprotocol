using UnityEngine;

namespace ShadowProtocol
{
    /// <summary>
    /// 탈출 지점. 데이터를 확보한 뒤(탈출 단계) 플레이어가 들어오면 임무 성공.
    /// 탈출 단계가 되면 표시등이 켜지고 깜빡인다.
    /// </summary>
    public class ExtractionZone : MonoBehaviour
    {
        public PlayerController player;
        public float radius = 2f;
        public Light beacon;
        public Renderer marker;

        float beaconBase;

        void Start()
        {
            if (player == null) player = FindAnyObjectByType<PlayerController>();
            if (beacon != null) { beaconBase = beacon.intensity; beacon.enabled = false; }
            if (marker != null) marker.enabled = false;
        }

        void Update()
        {
            var gm = GameManager.Instance;
            bool active = gm != null && gm.Phase == GameManager.MissionPhase.Extract;
            if (beacon != null)
            {
                beacon.enabled = active;
                if (active) beacon.intensity = beaconBase * (0.7f + 0.3f * Mathf.Sin(Time.time * 4f));
            }
            if (marker != null) marker.enabled = active;
            if (!active || player == null || !GameManager.GameplayActive) return;
            Vector3 d = player.transform.position - transform.position;
            d.y = 0f;
            if (d.magnitude <= radius) gm.PlayerExtracted();
        }
    }
}
