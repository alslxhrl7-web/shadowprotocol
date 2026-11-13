using UnityEngine;

namespace ShadowProtocol
{
    /// <summary>침투 경로 표시 영역(환기구·덕트·캣워크·정면 하역문·사무동 출입문). 적응형 지휘가 경로 사용을 기록한다.</summary>
    public class RouteMarker : MonoBehaviour
    {
        public string routeId = "Vent";
        public string label = "환기구";
        public Vector3 size = new Vector3(2f, 2f, 2f);

        public bool Contains(Vector3 p) => new Bounds(transform.position, size).Contains(p);

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.35f);
            Gizmos.DrawWireCube(transform.position, size);
        }
    }
}
