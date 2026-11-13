using UnityEngine;

namespace ShadowProtocol
{
    /// <summary>
    /// 고정 각도로 플레이어를 부드럽게 따라가는 3인칭 탑다운 카메라.
    /// </summary>
    public class TopDownCamera : MonoBehaviour
    {
        public Transform target;
        public Vector3 offset = new Vector3(0f, 14.5f, -8.5f);
        public float followSharpness = 6f;
        public float lookHeight = 0.5f;

        [Header("Focus clamp (맵 밖이 덜 보이게)")]
        public bool clampFocus = true;
        public Vector2 focusMin = new Vector2(-10.5f, -7f);
        public Vector2 focusMax = new Vector2(10.5f, 7f);

        Vector3 Focus()
        {
            Vector3 p = target.position;
            if (clampFocus)
            {
                p.x = Mathf.Clamp(p.x, focusMin.x, focusMax.x);
                p.z = Mathf.Clamp(p.z, focusMin.y, focusMax.y);
            }
            return p;
        }

        void Start()
        {
            Snap();
        }

        public void Snap()
        {
            if (target == null) return;
            transform.position = Focus() + offset;
            transform.rotation = Quaternion.LookRotation(-offset + Vector3.up * lookHeight, Vector3.up);
        }

        void LateUpdate()
        {
            if (target == null) return;
            Vector3 desired = Focus() + offset;
            float t = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, desired, t);
            transform.rotation = Quaternion.LookRotation(-offset + Vector3.up * lookHeight, Vector3.up);
        }
    }
}
