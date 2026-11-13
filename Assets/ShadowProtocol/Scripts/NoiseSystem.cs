using System;
using UnityEngine;

namespace ShadowProtocol
{
    /// <summary>
    /// 소음 이벤트를 경비병에게 전달하는 간단한 전역 허브.
    /// </summary>
    public static class NoiseSystem
    {
        /// <summary>위치, 반경, 소음을 낸 오브젝트</summary>
        public static event Action<Vector3, float, GameObject> OnNoise;

        public static void Emit(Vector3 position, float radius, GameObject source)
        {
            if (radius <= 0f) return;
            OnNoise?.Invoke(position, radius, source);
        }
    }
}
