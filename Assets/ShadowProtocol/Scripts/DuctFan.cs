using UnityEngine;

namespace ShadowProtocol
{
    /// <summary>덕트 환풍기: 돌고 있으면 덕트를 막는다(배전반으로 정지). 도는 소리 대신 바람 흔들림 없이 날개 회전만.</summary>
    public class DuctFan : MonoBehaviour
    {
        public Transform blades;
        public Collider blocker;
        public float speed = 720f;
        public bool Running { get; private set; } = true;

        float spin;

        public void Stop()
        {
            Running = false;
            if (blocker != null) blocker.enabled = false;
        }

        void Update()
        {
            spin = Mathf.MoveTowards(spin, Running ? speed : 0f, Time.deltaTime * 400f);
            if (blades != null) blades.Rotate(spin * Time.deltaTime, 0f, 0f, Space.Self);
        }
    }
}
