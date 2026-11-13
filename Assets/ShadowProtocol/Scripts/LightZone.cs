using System.Collections.Generic;
using UnityEngine;

namespace ShadowProtocol
{
    /// <summary>
    /// 밝기 구역(물리 충돌 없음, 순수 데이터 박스). 경비병 시야 거리가 플레이어 위치의 밝기로 보정된다.
    /// 여러 구역이 겹치면 가장 밝은 값.
    /// </summary>
    public class LightZone : MonoBehaviour
    {
        public static readonly List<LightZone> All = new List<LightZone>();
        public const float DefaultBrightness = 0.7f;

        public Vector3 size = new Vector3(10f, 3f, 10f);
        [Range(0f, 1.5f)] public float brightness = 1f;
        public string label = "";

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        public bool Contains(Vector3 p)
        {
            Vector3 l = transform.InverseTransformPoint(p);
            return Mathf.Abs(l.x) <= size.x * 0.5f && Mathf.Abs(l.y) <= size.y * 0.5f && Mathf.Abs(l.z) <= size.z * 0.5f;
        }

        public static float BrightnessAt(Vector3 p)
        {
            float best = -1f;
            for (int i = 0; i < All.Count; i++)
                if (All[i].Contains(p) && All[i].brightness > best) best = All[i].brightness;
            return best < 0f ? DefaultBrightness : best;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.9f, 0.3f, 0.15f + 0.3f * Mathf.Clamp01(brightness));
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, size);
        }
    }
}
