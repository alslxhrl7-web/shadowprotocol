using System.Collections.Generic;
using UnityEngine;

namespace ShadowProtocol
{
    /// <summary>
    /// 음악 3층(평상시 앰비언트 / 의심 긴장 레이어 / 경보 리듬) + 사이렌을 경계 단계에 따라 크로스페이드하고,
    /// 3D 효과음 풀(spatialBlend 1, 선형 감쇠)을 제공한다.
    /// </summary>
    [DefaultExecutionOrder(-90)]
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Music")]
        public AudioClip musicAmbient, musicTension, musicAlert, siren;
        [Header("Footsteps")]
        public AudioClip[] stepConcrete, stepMetal, stepAsphalt;
        [Header("SFX")]
        public AudioClip guardSuspicious, guardAlert, radioBeep, dataJingle, winSting, failSting;
        public AudioClip bottleBreak, canLand, knockout, uiClick, pickup, throwWhoosh, checkpoint;

        AudioSource amb, ten, alr, sir, ui;
        readonly List<AudioSource> pool = new List<AudioSource>();
        int next;
        float lAmb = 1f, lTen, lAlr, lSir;

        public int Threat { get; private set; }

        void Awake()
        {
            Instance = this;
            amb = MakeLoop(musicAmbient);
            ten = MakeLoop(musicTension);
            alr = MakeLoop(musicAlert);
            sir = MakeLoop(siren);
            ui = gameObject.AddComponent<AudioSource>();
            ui.playOnAwake = false;
            ui.spatialBlend = 0f;
            ui.ignoreListenerPause = true;
            for (int i = 0; i < 24; i++)
            {
                var go = new GameObject("Sfx3D_" + i);
                go.transform.SetParent(transform, false);
                var s = go.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 1f;
                s.rolloffMode = AudioRolloffMode.Linear;
                s.dopplerLevel = 0f;
                pool.Add(s);
            }
            // 세 음악 층을 같은 시각에 시작해 박자가 맞게
            double t = AudioSettings.dspTime + 0.1;
            if (amb.clip) amb.PlayScheduled(t);
            if (ten.clip) ten.PlayScheduled(t);
            if (alr.clip) alr.PlayScheduled(t);
            if (sir.clip) sir.Play();
        }

        AudioSource MakeLoop(AudioClip c)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.clip = c;
            s.loop = true;
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            s.volume = 0f;
            s.ignoreListenerPause = true;
            return s;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            int threat = 0;
            foreach (var g in GuardAI.All)
            {
                if (g.IsKnockedOut) continue;
                if (g.State == GuardState.Alert) { threat = 2; break; }
                if (g.State == GuardState.Suspicious || g.State == GuardState.Search) threat = 1;
            }
            if (GuardDirector.Instance != null && GuardDirector.Instance.AlarmActive) threat = Mathf.Max(threat, 2);
            Threat = threat;

            var gm = GameManager.Instance;
            bool menu = gm != null && (gm.Flow == GameManager.FlowState.Title || gm.Flow == GameManager.FlowState.Briefing);
            float tAmb = threat == 2 ? 0.35f : (threat == 1 ? 0.75f : 1f);
            float tTen = threat == 2 ? 0.55f : (threat == 1 ? 1f : 0f);
            float tAlr = threat == 2 ? 1f : 0f;
            float tSir = threat == 2 ? 1f : 0f;
            if (menu) { tAmb = 0.9f; tTen = 0f; tAlr = 0f; tSir = 0f; }
            if (GameManager.IsOver) { tAmb = 0.35f; tTen = 0f; tAlr = 0f; tSir = 0f; }

            float dt = Time.unscaledDeltaTime;
            float up = 1f - Mathf.Exp(-dt * 3.5f), down = 1f - Mathf.Exp(-dt * 0.8f);
            lAmb = Mathf.Lerp(lAmb, tAmb, tAmb > lAmb ? down : up);
            lTen = Mathf.Lerp(lTen, tTen, tTen > lTen ? up : down);
            lAlr = Mathf.Lerp(lAlr, tAlr, tAlr > lAlr ? up * 1.5f : down);
            lSir = Mathf.Lerp(lSir, tSir, tSir > lSir ? up : down * 2f);

            float mv = GameSettings.MusicVolume;
            amb.volume = lAmb * mv * 0.8f;
            ten.volume = lTen * mv * 0.7f;
            alr.volume = lAlr * mv * 0.75f;
            sir.volume = lSir * GameSettings.SfxVolume * 0.18f;
        }

        public void PlayUI(AudioClip c, float vol = 1f)
        {
            if (c != null) ui.PlayOneShot(c, vol * GameSettings.SfxVolume);
        }

        public void PlayAt(AudioClip c, Vector3 pos, float vol = 1f, float minDist = 1f, float maxDist = 20f, float pitch = 1f)
        {
            if (c == null || pool.Count == 0) return;
            var s = pool[next];
            next = (next + 1) % pool.Count;
            s.transform.position = pos;
            s.clip = c;
            s.volume = vol * GameSettings.SfxVolume;
            s.minDistance = minDist;
            s.maxDistance = maxDist;
            s.pitch = pitch;
            s.Play();
        }

        public static AudioClip Pick(AudioClip[] a) => (a == null || a.Length == 0) ? null : a[Random.Range(0, a.Length)];

        public static void Click()
        {
            if (Instance != null) Instance.PlayUI(Instance.uiClick, 0.6f);
        }
    }
}
