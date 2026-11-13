using System.Collections.Generic;
using UnityEngine;

namespace ShadowProtocol
{
    /// <summary>E로 상호작용하는 퍼즐 장치(키패드·쪽지·배전반)의 공통 부모.</summary>
    public abstract class PuzzleInteractable : MonoBehaviour
    {
        public static readonly List<PuzzleInteractable> All = new List<PuzzleInteractable>();

        public float radius = 1.5f;
        [Tooltip("적응형 지휘에 기록되는 퍼즐 ID")]
        public string puzzleId = "Puzzle";
        public string puzzleLabel = "퍼즐";

        protected virtual void OnEnable() { All.Add(this); }
        protected virtual void OnDisable() { All.Remove(this); }

        public virtual bool Available => true;
        public abstract string PromptText { get; }
        public abstract void Interact(PlayerTools tools);

        public bool InRange(Vector3 p)
        {
            Vector3 d = transform.position - p;
            if (Mathf.Abs(d.y - 1f) > 1.6f) return false;
            d.y = 0f;
            return d.magnitude <= radius;
        }

        /// <summary>근처 경비에게 들리는 소리(오답·힌트)</summary>
        protected void MakeNoise(float r)
        {
            NoiseSystem.Emit(transform.position, r, gameObject);
        }

        public static PuzzleInteractable Nearest(Vector3 p)
        {
            PuzzleInteractable best = null;
            float bd = float.MaxValue;
            foreach (var i in All)
            {
                if (i == null || !i.Available || !i.InRange(p)) continue;
                float d = (i.transform.position - p).sqrMagnitude;
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }
    }
}
