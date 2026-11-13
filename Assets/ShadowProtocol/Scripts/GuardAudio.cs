using UnityEngine;

namespace ShadowProtocol
{
    /// <summary>경비병 상태 전환음(의심 "?", 경보 "!" + 무전 잡음·삐).</summary>
    [RequireComponent(typeof(GuardAI))]
    public class GuardAudio : MonoBehaviour
    {
        GuardAI g;
        GuardState last;
        bool lastKo;

        void Start()
        {
            g = GetComponent<GuardAI>();
            last = g.State;
        }

        void Update()
        {
            var am = AudioManager.Instance;
            if (am == null || g == null) return;
            if (g.IsKnockedOut != lastKo) { lastKo = g.IsKnockedOut; last = g.State; return; }
            if (g.State == last) return;
            Vector3 head = transform.position + Vector3.up * 2f;
            if (g.State == GuardState.Alert)
            {
                am.PlayAt(am.guardAlert, head, 1f, 2f, 40f);
                am.PlayAt(am.radioBeep, head, 0.7f, 2f, 30f);
            }
            else if ((g.State == GuardState.Suspicious || g.State == GuardState.Search) && last == GuardState.Patrol)
            {
                am.PlayAt(am.guardSuspicious, head, 0.9f, 2f, 30f);
            }
            last = g.State;
        }
    }
}
