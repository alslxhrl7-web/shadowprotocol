using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ShadowProtocol.EditorTools
{
    /// <summary>
    /// 절차적 사운드 합성(외부 음원 없음). 노이즈 엔벨로프·필터·사인 공명으로 효과음을,
    /// 120BPM·16초 루프 3층(평상시 드론 / 긴장 / 경보)으로 음악을 만든다.
    /// 메뉴: ShadowProtocol/Generate Audio  (Build Demo Scene 시 없으면 자동 생성)
    /// </summary>
    public static class ShadowProtocolAudioBuilder
    {
        public const string Folder = "Assets/ShadowProtocol/Audio";
        const int SR = 44100;
        const float Bpm = 120f;
        const float LoopSec = 16f;

        [MenuItem("ShadowProtocol/Generate Audio")]
        public static void GenerateAll() => Generate(true);

        public static void EnsureAudio() => Generate(false);

        static void Generate(bool force)
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/ShadowProtocol", "Audio");
            var rnd = new System.Random(7);
            for (int i = 0; i < 4; i++)
            {
                Make("step_concrete_" + i, force, () => StepConcrete(rnd, i));
                Make("step_metal_" + i, force, () => StepMetal(rnd, i));
                Make("step_asphalt_" + i, force, () => StepAsphalt(rnd, i));
            }
            Make("guard_suspicious", force, Suspicious);
            Make("guard_alert", force, () => AlertStab(rnd));
            Make("radio_beep", force, () => RadioBeep(rnd));
            Make("siren_loop", force, Siren, true);
            Make("music_ambient", force, () => MusicAmbient(rnd), true);
            Make("music_tension", force, () => MusicTension(rnd), true);
            Make("music_alert", force, () => MusicAlert(rnd), true);
            Make("jingle_data", force, () => Arp(new[] { 523.25f, 659.25f, 783.99f, 1046.5f }, 0.11f, 1.1f));
            Make("sting_win", force, WinSting);
            Make("sting_fail", force, () => FailSting(rnd));
            Make("bottle_break", force, () => BottleBreak(rnd));
            Make("can_land", force, () => CanLand(rnd));
            Make("knockout", force, () => Knockout(rnd));
            Make("ui_click", force, () => Tone(2100f, 0.05f, 60f, 0.5f));
            Make("pickup", force, () => Arp(new[] { 880f, 1318.5f }, 0.06f, 0.25f));
            Make("throw_whoosh", force, () => Whoosh(rnd));
            Make("checkpoint", force, () => Arp(new[] { 659.25f, 987.77f }, 0.12f, 0.8f));
            AssetDatabase.Refresh();
        }

        // ---------------- 출력 ----------------

        static void Make(string name, bool force, Func<float[]> gen, bool loop = false)
        {
            string path = Folder + "/" + name + ".wav";
            if (!force && File.Exists(path)) return;
            float[] s = gen();
            Normalize(s, loop ? 0.7f : 0.89f);
            WriteWav(path, s);
            AssetDatabase.ImportAsset(path);
            var imp = (AudioImporter)AssetImporter.GetAtPath(path);
            var st = imp.defaultSampleSettings;
            st.loadType = loop ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            st.compressionFormat = loop ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.PCM;
            st.quality = 0.6f;
            imp.defaultSampleSettings = st;
            imp.forceToMono = true;
            imp.SaveAndReimport();
        }

        static void WriteWav(string path, float[] s)
        {
            using (var fs = new FileStream(path, FileMode.Create))
            using (var w = new BinaryWriter(fs))
            {
                int bytes = s.Length * 2;
                w.Write(new[] { 'R', 'I', 'F', 'F' }); w.Write(36 + bytes);
                w.Write(new[] { 'W', 'A', 'V', 'E' }); w.Write(new[] { 'f', 'm', 't', ' ' });
                w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(SR); w.Write(SR * 2); w.Write((short)2); w.Write((short)16);
                w.Write(new[] { 'd', 'a', 't', 'a' }); w.Write(bytes);
                foreach (float v in s) w.Write((short)Mathf.Clamp(Mathf.RoundToInt(v * 32767f), -32768, 32767));
            }
        }

        static void Normalize(float[] s, float peak)
        {
            float m = 0.0001f;
            foreach (float v in s) m = Mathf.Max(m, Mathf.Abs(v));
            float g = peak / m;
            for (int i = 0; i < s.Length; i++) s[i] *= g;
        }

        // ---------------- 기본 재료 ----------------

        static float[] Buf(float sec) => new float[Mathf.CeilToInt(sec * SR)];
        static float N(System.Random r) => (float)(r.NextDouble() * 2.0 - 1.0);
        static float Coef(float fc) => 1f - Mathf.Exp(-2f * Mathf.PI * fc / SR);

        static void LowPass(float[] s, float fc) { float a = Coef(fc), y = 0; for (int i = 0; i < s.Length; i++) { y += a * (s[i] - y); s[i] = y; } }
        static void HighPass(float[] s, float fc) { float a = Coef(fc), y = 0; for (int i = 0; i < s.Length; i++) { y += a * (s[i] - y); s[i] -= y; } }

        static void AddSine(float[] s, float start, float freq, float amp, float decay, float len = -1f)
        {
            int i0 = (int)(start * SR), n = len > 0 ? (int)(len * SR) : s.Length - i0;
            for (int i = 0; i < n && i0 + i < s.Length; i++)
            {
                float t = i / (float)SR;
                s[i0 + i] += Mathf.Sin(2f * Mathf.PI * freq * t) * amp * Mathf.Exp(-decay * t) * Mathf.Min(1f, t * 400f);
            }
        }

        static void AddNoiseBurst(float[] s, System.Random r, float start, float amp, float decay, float len)
        {
            int i0 = (int)(start * SR), n = (int)(len * SR);
            for (int i = 0; i < n && i0 + i < s.Length; i++) { float t = i / (float)SR; s[i0 + i] += N(r) * amp * Mathf.Exp(-decay * t) * Mathf.Min(1f, t * 800f); }
        }

        static float[] Tone(float f, float len, float decay, float amp) { var s = Buf(len); AddSine(s, 0, f, amp, decay); return s; }

        static float[] Arp(float[] notes, float step, float len)
        {
            var s = Buf(len);
            for (int k = 0; k < notes.Length; k++) { AddSine(s, k * step, notes[k], 0.5f, 5f); AddSine(s, k * step, notes[k] * 2f, 0.12f, 9f); }
            return s;
        }

        // 16초 루프용: 끝과 시작을 겹쳐 이음매 없는 루프
        static float[] Loopify(float[] s, float fadeSec)
        {
            int f = (int)(fadeSec * SR), L = s.Length - f;
            var o = new float[L];
            Array.Copy(s, o, L);
            for (int i = 0; i < f; i++) { float a = i / (float)f; o[i] = s[i] * a + s[L + i] * (1f - a); }
            return o;
        }

        // ---------------- 효과음 ----------------

        static float[] StepConcrete(System.Random r, int v)
        {
            var s = Buf(0.16f);
            AddNoiseBurst(s, r, 0, 0.8f, 38f + v * 4, 0.16f);
            LowPass(s, 1300f + v * 180f);
            AddSine(s, 0, 85f + v * 6, 0.5f, 40f);
            return s;
        }

        static float[] StepMetal(System.Random r, int v)
        {
            var s = Buf(0.35f);
            AddNoiseBurst(s, r, 0, 0.5f, 60f, 0.05f);
            HighPass(s, 400f);
            float[] modes = { 520f, 1340f, 2210f, 3120f };
            foreach (float m in modes) AddSine(s, 0, m * (1f + v * 0.03f), 0.22f, 16f + v * 2f);
            AddSine(s, 0, 110f, 0.35f, 30f);
            return s;
        }

        static float[] StepAsphalt(System.Random r, int v)
        {
            var s = Buf(0.14f);
            AddNoiseBurst(s, r, 0, 0.7f, 45f + v * 5, 0.14f);
            LowPass(s, 2600f); HighPass(s, 250f);
            for (int k = 0; k < 6; k++) AddNoiseBurst(s, r, 0.01f + (float)r.NextDouble() * 0.08f, 0.25f, 300f, 0.01f); // 자갈 사각거림
            return s;
        }

        static float[] Suspicious()
        {
            var s = Buf(0.45f);
            float ph = 0;
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)SR;
                float f = t < 0.18f ? Mathf.Lerp(520f, 760f, t / 0.18f) : Mathf.Lerp(700f, 820f, (t - 0.2f) / 0.25f);
                ph += 2f * Mathf.PI * f / SR;
                float env = (t < 0.18f ? Mathf.Exp(-6f * t) : (t > 0.2f ? Mathf.Exp(-8f * (t - 0.2f)) : 0f)) * Mathf.Min(1f, t * 300f);
                s[i] = Mathf.Sin(ph) * env * 0.5f;
            }
            return s;
        }

        static float[] AlertStab(System.Random r)
        {
            var s = Buf(0.5f);
            AddNoiseBurst(s, r, 0, 0.6f, 50f, 0.06f);
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)SR;
                float sq = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 880f * t)) * 0.25f + Mathf.Sin(2f * Mathf.PI * 1320f * t) * 0.3f;
                s[i] += sq * Mathf.Exp(-7f * t) * Mathf.Min(1f, t * 600f);
            }
            LowPass(s, 5000f);
            return s;
        }

        static float[] RadioBeep(System.Random r)
        {
            var s = Buf(0.55f);
            AddNoiseBurst(s, r, 0, 0.35f, 2f, 0.38f);
            LowPass(s, 3200f); HighPass(s, 900f);
            AddSine(s, 0.38f, 1000f, 0.45f, 0f, 0.12f);
            return s;
        }

        static float[] Siren()
        {
            float total = 2f, fade = 0.1f;
            var s = Buf(total + fade);
            float ph = 0;
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)SR;
                float lfo = 0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * t / total);
                float f = Mathf.Lerp(560f, 900f, lfo);
                ph += 2f * Mathf.PI * f / SR;
                s[i] = (Mathf.Sin(ph) + 0.3f * Mathf.Sin(ph * 2f)) * 0.4f;
            }
            LowPass(s, 3000f);
            return Loopify(s, fade);
        }

        static float[] WinSting()
        {
            var s = Buf(2.4f);
            float[] chord = { 261.63f, 329.63f, 392f, 523.25f };
            foreach (float f in chord) { AddSine(s, 0.05f, f, 0.25f, 1.2f); AddSine(s, 0.05f, f * 1.005f, 0.12f, 1.2f); }
            float[] arp = { 523.25f, 659.25f, 783.99f, 1046.5f, 1318.5f };
            for (int k = 0; k < arp.Length; k++) AddSine(s, 0.15f + k * 0.09f, arp[k], 0.3f, 4f);
            return s;
        }

        static float[] FailSting(System.Random r)
        {
            var s = Buf(2.2f);
            float[] notes = { 392f, 349.23f, 311.13f, 233.08f };
            for (int k = 0; k < notes.Length; k++) AddSine(s, k * 0.22f, notes[k], 0.35f, 2.2f);
            AddSine(s, 0.9f, 48f, 0.8f, 2.5f);
            AddNoiseBurst(s, r, 0.9f, 0.4f, 6f, 0.8f);
            LowPass(s, 2500f);
            return s;
        }

        static float[] BottleBreak(System.Random r)
        {
            var s = Buf(0.7f);
            AddNoiseBurst(s, r, 0, 0.9f, 25f, 0.3f);
            HighPass(s, 1800f);
            for (int k = 0; k < 14; k++) AddSine(s, (float)r.NextDouble() * 0.25f, 2200f + (float)r.NextDouble() * 5200f, 0.12f, 18f + (float)r.NextDouble() * 20f, 0.3f);
            return s;
        }

        static float[] CanLand(System.Random r)
        {
            var s = Buf(0.7f);
            float[] at = { 0f, 0.17f, 0.29f, 0.37f };
            float[] amp = { 1f, 0.55f, 0.3f, 0.15f };
            for (int k = 0; k < at.Length; k++)
            {
                AddNoiseBurst(s, r, at[k], 0.25f * amp[k], 120f, 0.02f);
                AddSine(s, at[k], 930f, 0.3f * amp[k], 22f, 0.25f);
                AddSine(s, at[k], 1710f, 0.22f * amp[k], 28f, 0.2f);
                AddSine(s, at[k], 2630f, 0.15f * amp[k], 34f, 0.15f);
            }
            return s;
        }

        static float[] Knockout(System.Random r)
        {
            var s = Buf(0.7f);
            AddNoiseBurst(s, r, 0, 0.6f, 30f, 0.12f);
            LowPass(s, 900f);
            AddSine(s, 0, 62f, 0.9f, 9f);
            AddNoiseBurst(s, r, 0.28f, 0.35f, 20f, 0.2f); // 쓰러지는 소리
            AddSine(s, 0.28f, 55f, 0.6f, 10f);
            LowPass(s, 1400f);
            return s;
        }

        static float[] Whoosh(System.Random r)
        {
            var s = Buf(0.35f);
            for (int i = 0; i < s.Length; i++) { float t = i / (float)s.Length; s[i] = N(r) * Mathf.Sin(Mathf.PI * t) * 0.6f; }
            LowPass(s, 1800f); HighPass(s, 300f);
            return s;
        }

        // ---------------- 음악(120BPM, 16초 = 8마디) ----------------

        static float Beat => 60f / Bpm;

        static float[] MusicAmbient(System.Random r)
        {
            float fade = 0.5f;
            var s = Buf(LoopSec + fade);
            // 저음 드론(서로 조금 어긋난 음이 느리게 맥놀이)
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)SR;
                float sw = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * t / LoopSec);
                s[i] = 0.32f * Mathf.Sin(2f * Mathf.PI * 55f * t) + 0.18f * Mathf.Sin(2f * Mathf.PI * 55.4f * t)
                     + 0.12f * Mathf.Sin(2f * Mathf.PI * 82.5f * t) * sw + 0.06f * Mathf.Sin(2f * Mathf.PI * 164.8f * t) * (1f - sw);
            }
            // 바람 같은 노이즈
            var wind = Buf(LoopSec + fade);
            for (int i = 0; i < wind.Length; i++) wind[i] = N(r);
            LowPass(wind, 380f);
            for (int i = 0; i < s.Length; i++) { float t = i / (float)SR; s[i] += wind[i] * 0.35f * (0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * t / 8f)); }
            // 드문드문 울리는 패드 음
            float[] pads = { 220f, 196f, 246.94f, 174.61f };
            for (int k = 0; k < 4; k++) AddSine(s, k * 4f + 0.5f, pads[k], 0.06f, 0.6f, 3.5f);
            return Loopify(s, fade);
        }

        static float[] MusicTension(System.Random r)
        {
            float fade = 0.3f;
            var s = Buf(LoopSec + fade);
            int beats = Mathf.RoundToInt(LoopSec / Beat);
            for (int b = 0; b < beats + 1; b++)
            {
                float t0 = b * Beat;
                AddSine(s, t0, 58f, 0.55f, 14f, 0.3f);           // 심장 박동 1
                AddSine(s, t0 + 0.16f, 52f, 0.35f, 16f, 0.25f);  // 심장 박동 2
            }
            // 높은 떨림 음(트레몰로)
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)SR;
                s[i] += 0.05f * Mathf.Sin(2f * Mathf.PI * 880f * t) * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 6f * t)) * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * t / LoopSec));
                s[i] += 0.04f * Mathf.Sin(2f * Mathf.PI * 932.33f * t) * (0.5f - 0.5f * Mathf.Sin(2f * Mathf.PI * t / LoopSec));
            }
            return Loopify(s, fade);
        }

        static float[] MusicAlert(System.Random r)
        {
            float fade = 0.05f;
            var s = Buf(LoopSec + fade);
            int beats = Mathf.RoundToInt(LoopSec / Beat);
            float[] bass = { 55f, 55f, 65.41f, 55f, 49f, 55f, 73.42f, 65.41f };
            for (int b = 0; b < beats + 1; b++)
            {
                float t0 = b * Beat;
                // 킥: 120→45Hz 하강
                int i0 = (int)(t0 * SR);
                float ph = 0;
                for (int i = 0; i < (int)(0.25f * SR) && i0 + i < s.Length; i++) { float t = i / (float)SR; float f = Mathf.Lerp(130f, 45f, Mathf.Min(1f, t * 12f)); ph += 2f * Mathf.PI * f / SR; s[i0 + i] += Mathf.Sin(ph) * 0.7f * Mathf.Exp(-14f * t); }
                // 16분 하이햇
                for (int k = 0; k < 4; k++) AddNoiseBurst(s, r, t0 + k * Beat / 4f, k == 2 ? 0.16f : 0.08f, 90f, 0.04f);
                // 8분 베이스(톱니파 흉내)
                for (int k = 0; k < 2; k++)
                {
                    float f = bass[(b * 2 + k) % bass.Length];
                    int j0 = (int)((t0 + k * Beat / 2f) * SR);
                    for (int i = 0; i < (int)(Beat / 2f * SR * 0.9f) && j0 + i < s.Length; i++)
                    {
                        float t = i / (float)SR;
                        float saw = 2f * ((f * t) % 1f) - 1f;
                        s[j0 + i] += saw * 0.18f * Mathf.Exp(-5f * t);
                    }
                }
            }
            // 마디마다 날카로운 스탭
            for (int bar = 0; bar < 9; bar++) AddSine(s, bar * Beat * 4f, 440f, 0.12f, 6f, 0.4f);
            HighPass(s, 30f);
            return Loopify(s, fade);
        }
    }
}
