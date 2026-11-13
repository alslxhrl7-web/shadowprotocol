using UnityEngine;

namespace ShadowProtocol
{
    /// <summary>
    /// 플레이어 발밑에 현재 소음 반경을 원으로 보여준다.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class PlayerNoiseRing : MonoBehaviour
    {
        public PlayerController player;
        [Range(12, 96)] public int segments = 48;
        public Color color = new Color(1f, 1f, 1f, 0.5f);

        LineRenderer line;
        float radius;

        void Awake()
        {
            line = GetComponent<LineRenderer>();
            line.loop = true;
            line.useWorldSpace = false;
            line.positionCount = segments;
            if (player == null) player = GetComponentInParent<PlayerController>();
        }

        void LateUpdate()
        {
            if (player == null) return;
            float target = player.CurrentNoiseRadius;
            radius = Mathf.Lerp(radius, target, 1f - Mathf.Exp(-8f * Time.deltaTime));

            if (line.positionCount != segments) line.positionCount = segments;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                line.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f));
            }

            Color c = color;
            c.a = radius > 0.2f ? color.a : 0f;
            line.startColor = c;
            line.endColor = c;
        }
    }
}
