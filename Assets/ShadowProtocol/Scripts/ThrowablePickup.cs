using System.Collections.Generic;
using UnityEngine;

namespace ShadowProtocol
{
    public enum ThrowKind { Bottle, Can }

    /// <summary>바닥에 놓인 던질 물건(병/캔). 플레이어가 가까이 가면 줍는다.</summary>
    public class ThrowablePickup : MonoBehaviour
    {
        public static readonly List<ThrowablePickup> All = new List<ThrowablePickup>();

        public ThrowKind kind = ThrowKind.Bottle;
        public float radius = 1.2f;
        public Transform visual;

        Vector3 basePos;

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        void Start()
        {
            if (visual == null && transform.childCount > 0) visual = transform.GetChild(0);
            if (visual != null) basePos = visual.localPosition;
        }

        void Update()
        {
            if (visual == null) return;
            visual.localPosition = basePos + Vector3.up * (0.06f + Mathf.Sin(Time.time * 2.4f) * 0.04f);
            visual.Rotate(0f, 70f * Time.deltaTime, 0f, Space.World);
        }

        public static string KindName(ThrowKind k) => k == ThrowKind.Bottle ? "빈 병" : "깡통";
    }
}
