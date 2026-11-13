using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShadowProtocol
{
    /// <summary>
    /// uGUI 화면 전체(코드로 생성): 타이틀·브리핑·일시정지·설정·결과, 플레이 HUD
    /// (탐지 게이지, 화면 밖 경비 표시, 미니맵 — 본 적 있는 경비만, 현재 목표, 상태·던질 물건, 안내 문구, 무전 자막, 도움말).
    /// </summary>
    [DefaultExecutionOrder(50)]
    public class GameUI : MonoBehaviour
    {
        public static GameUI Instance { get; private set; }

        public PlayerController player;
        public PlayerTools tools;
        [Header("지도 범위(월드 XZ)")]
        public Vector2 mapMin = new Vector2(-41.5f, -21.5f);
        public Vector2 mapMax = new Vector2(16.5f, 22.5f);
        public Vector3 goalPosition = new Vector3(13.2f, 0f, 17.4f);
        public Vector3 extractPosition = new Vector3(-35.5f, 0f, -15f);
        public int mapPixelsPerMeter = 8;
        public MakingOfGallery gallery;

        static readonly Color Accent = new Color(0.35f, 0.85f, 1f);
        static readonly Color PanelColor = new Color(0.03f, 0.05f, 0.08f, 0.86f);
        static readonly Color ButtonColor = new Color(0.1f, 0.18f, 0.24f, 0.95f);

        Font font;
        RectTransform root;
        GameObject titleScreen, briefingScreen, pauseScreen, settingsScreen, resultScreen, hud;
        bool settingsOpen;

        Image gaugeFill;
        RectTransform gaugeFillRect;
        Text gaugeLabel, objectiveText, statusText, throwText, promptText, bannerText, radioText, tutorialText;
        CanvasGroup bannerGroup, radioGroup, tutorialGroup;
        RawImage minimap;
        RectTransform minimapRect, mmPlayer, mmGoal, mmExtract;
        readonly List<Image> mmGuards = new List<Image>();
        readonly List<RectTransform> indicators = new List<RectTransform>();
        readonly List<Text> indicatorSymbols = new List<Text>();
        readonly List<Text> indicatorDistances = new List<Text>();
        readonly List<Image> indicatorBars = new List<Image>();
        Texture2D mapTex;
        readonly HashSet<GuardAI> seen = new HashSet<GuardAI>();
        float seenTimer;

        readonly Queue<KeyValuePair<string, float>> radioQueue = new Queue<KeyValuePair<string, float>>();
        float radioTimer, tutorialTimer;

        Text resultTitle, resultStats, resultGrade, resultLog;
        Button resultCheckpoint, resultRestart, pauseCheckpoint;
        Slider sSens, sMusic, sSfx, sFov;
        Toggle tInvert;
        Text vSens, vMusic, vSfx, vFov;

        public int SeenGuardCount => seen.Count;

        void Awake()
        {
            Instance = this;
            GuardDirector.Radio += OnRadio;
            BuildCanvas();
        }

        void OnDestroy()
        {
            GuardDirector.Radio -= OnRadio;
            if (Instance == this) Instance = null;
            if (mapTex != null) Destroy(mapTex);
        }

        void Start()
        {
            if (player == null) player = FindAnyObjectByType<PlayerController>();
            if (tools == null && player != null) tools = player.GetComponent<PlayerTools>();
            BuildMapTexture();
            minimap.texture = mapTex;
            foreach (var ri in briefingScreen.GetComponentsInChildren<RawImage>(true)) ri.texture = mapTex;
            RefreshSettingsControls();
        }

        // ================= 공용 =================

        public void ShowTutorial(string text, float seconds)
        {
            tutorialText.text = text;
            tutorialTimer = seconds;
        }

        void OnRadio(string text, float seconds)
        {
            radioQueue.Enqueue(new KeyValuePair<string, float>(text, seconds));
        }

        // ================= 갱신 =================

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;
            var flow = gm.Flow;
            bool showSettings = settingsOpen && (flow == GameManager.FlowState.Title || flow == GameManager.FlowState.Paused);
            if (!showSettings) settingsOpen = false;
            SetActive(titleScreen, flow == GameManager.FlowState.Title && !showSettings);
            SetActive(briefingScreen, flow == GameManager.FlowState.Briefing);
            SetActive(pauseScreen, flow == GameManager.FlowState.Paused && !showSettings);
            SetActive(settingsScreen, showSettings);
            SetActive(resultScreen, flow == GameManager.FlowState.Result);
            SetActive(hud, flow == GameManager.FlowState.Playing || flow == GameManager.FlowState.Paused);

            float udt = Time.unscaledDeltaTime;
            UpdateRadio(udt);
            if (flow == GameManager.FlowState.Playing || flow == GameManager.FlowState.Paused) UpdateHud(gm, udt);
            if (flow == GameManager.FlowState.Paused && pauseCheckpoint != null) pauseCheckpoint.interactable = CheckpointData.Has;
            if (flow == GameManager.FlowState.Result) UpdateResult(gm);
            if (showSettings) UpdateSettingsLabels();
            UpdatePuzzles(flow);
            bool menuFlow = flow == GameManager.FlowState.Title || flow == GameManager.FlowState.Paused;
            if (!menuFlow) { galleryOpen = false; creditsOpen = false; }
            if (galleryOpen || creditsOpen) { SetActive(titleScreen, false); SetActive(pauseScreen, false); }
            SetActive(makingOfScreen, galleryOpen);
            SetActive(creditsScreen, creditsOpen);
            if (galleryOpen) UpdateMakingOf();
            else if (gallery != null && gallery.Current >= 0) { gallery.Hide(); }
        }

        static void SetActive(GameObject go, bool on)
        {
            if (go != null && go.activeSelf != on) go.SetActive(on);
        }

        void UpdateRadio(float dt)
        {
            if (radioTimer > 0f)
            {
                radioTimer -= dt;
                radioGroup.alpha = Mathf.Clamp01(radioTimer / 0.4f);
                return;
            }
            radioGroup.alpha = 0f;
            if (radioQueue.Count == 0) return;
            var m = radioQueue.Dequeue();
            radioText.text = m.Key;
            radioTimer = m.Value;
            radioGroup.alpha = 1f;
            var am = AudioManager.Instance;
            if (am != null) am.PlayUI(am.radioBeep, 0.45f);
        }

        void UpdateHud(GameManager gm, float dt)
        {
            // 탐지 게이지
            float maxSusp = 0f;
            GuardState worst = GuardState.Patrol;
            foreach (var g in GuardAI.All)
            {
                if (g.IsKnockedOut) continue;
                maxSusp = Mathf.Max(maxSusp, g.Suspicion);
                if (Severity(g.State) > Severity(worst)) worst = g.State;
            }
            gaugeFillRect.sizeDelta = new Vector2(440f * Mathf.Clamp01(maxSusp), gaugeFillRect.sizeDelta.y);
            gaugeFill.color = maxSusp < 0.01f ? GuardAI.ColorFor(GuardState.Patrol) : GuardAI.ColorFor(worst == GuardState.Patrol ? GuardState.Suspicious : worst);
            string alarm = GuardDirector.Instance != null && GuardDirector.Instance.AlarmActive ? "  <color=#FF5040>경보 발령</color>" : "";
            gaugeLabel.text = $"탐지 {Mathf.RoundToInt(maxSusp * 100f)}%   ·   {StateName(worst)}{alarm}";

            string frag = "";
            if (PuzzleState.NotesRead.Count > 0 && !gm.HasData)
            {
                frag = "\n<size=18>코드 조각 ";
                for (int c = 0; c < 4; c++)
                    frag += $" <color=#{PuzzleState.ColorHex[c]}>{PuzzleState.Icons[c]}{(PuzzleState.NotesRead.Contains(c) ? PuzzleState.DigitOf(c).ToString() : "?")}</color>";
                frag += "</size>";
            }
            objectiveText.text = "<size=18><color=#8FB8C8>현재 목표</color></size>\n" + gm.CurrentObjective + frag;

            if (player != null)
            {
                string stance = player.IsCrouching ? "웅크리기" : (player.IsRunning ? "달리기" : (player.IsMoving ? "걷기" : "정지"));
                float b = player.Brightness;
                string light = b < 0.55f ? "<color=#7FA7FF>어두움</color>" : (b < 0.9f ? "보통" : "<color=#FFE070>밝음</color>");
                statusText.text = $"자세 <b>{stance}</b>   소음 <b>{player.CurrentNoiseRadius:0}m</b>   노출 <b>{light}</b>   시간 <b>{FormatTime(gm.Elapsed)}</b>";
            }
            if (tools != null)
            {
                int n = tools.Throwables;
                string kind = n > 0 ? ThrowablePickup.KindName(tools.NextKind) : "-";
                throwText.text = n > 0
                    ? $"던질 물건 <b>{n}/{tools.maxThrowables}</b> ({kind})   <color=#9FC>우클릭 조준 · 좌클릭 던지기 · G</color>"
                    : $"던질 물건 <b>0/{tools.maxThrowables}</b>   <color=#889>바닥의 병·캔을 주울 수 있다</color>";
                promptText.text = tools.Prompt ?? "";
            }

            string banner = gm.Banner;
            bannerGroup.alpha = string.IsNullOrEmpty(banner) ? 0f : Mathf.Clamp01(gm.BannerTimer / 0.4f);
            if (!string.IsNullOrEmpty(banner)) bannerText.text = banner;

            if (tutorialTimer > 0f) tutorialTimer -= dt;
            tutorialGroup.alpha = Mathf.Clamp01(tutorialTimer / 0.5f);

            UpdateSeen(dt);
            UpdateMinimap(gm);
            UpdateIndicators();
        }

        void UpdateSeen(float dt)
        {
            seenTimer -= dt;
            if (seenTimer > 0f) return;
            seenTimer = 0.2f;
            var cam = Camera.main;
            if (cam == null) return;
            foreach (var g in GuardAI.All)
            {
                if (g == null || seen.Contains(g)) continue;
                if (g.State == GuardState.Alert) { seen.Add(g); continue; }
                Vector3 eye = g.EyePosition;
                if ((eye - cam.transform.position).sqrMagnitude > 40f * 40f) continue;
                Vector3 vp = cam.WorldToViewportPoint(eye);
                if (vp.z <= 0f || vp.x < 0f || vp.x > 1f || vp.y < 0f || vp.y > 1f) continue;
                if (Physics.Linecast(cam.transform.position, eye, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore))
                {
                    var hg = hit.collider.GetComponentInParent<GuardAI>();
                    if (hg != g && hit.collider.GetComponentInParent<PlayerController>() == null && hit.collider.GetComponentInParent<CameraFadeable>() == null) continue;
                }
                seen.Add(g);
            }
        }

        Vector2 MapPos(Vector3 w, Vector2 size)
        {
            return new Vector2((w.x - mapMin.x) / (mapMax.x - mapMin.x) * size.x, (w.z - mapMin.y) / (mapMax.y - mapMin.y) * size.y);
        }

        void UpdateMinimap(GameManager gm)
        {
            Vector2 size = minimapRect.rect.size;
            if (player != null)
            {
                mmPlayer.anchoredPosition = MapPos(player.transform.position, size);
                mmPlayer.localEulerAngles = new Vector3(0f, 0f, -player.transform.eulerAngles.y);
            }
            bool data = gm.HasData;
            mmGoal.gameObject.SetActive(!data);
            mmGoal.anchoredPosition = MapPos(goalPosition, size);
            mmExtract.gameObject.SetActive(data);
            mmExtract.anchoredPosition = MapPos(extractPosition, size);

            int i = 0;
            foreach (var g in GuardAI.All)
            {
                if (g == null || !seen.Contains(g)) continue;
                if (i >= mmGuards.Count)
                {
                    var dot = NewImage("GuardDot", minimapRect, Color.white);
                    var rt = dot.rectTransform;
                    rt.anchorMin = rt.anchorMax = Vector2.zero;
                    rt.sizeDelta = new Vector2(11f, 11f);
                    mmGuards.Add(dot);
                }
                var img = mmGuards[i++];
                img.gameObject.SetActive(true);
                img.rectTransform.anchoredPosition = MapPos(g.transform.position, size);
                img.color = g.IsKnockedOut ? new Color(0.55f, 0.55f, 0.6f) : GuardAI.ColorFor(g.State);
                img.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
            }
            for (; i < mmGuards.Count; i++) mmGuards[i].gameObject.SetActive(false);
        }

        void UpdateIndicators()
        {
            var cam = Camera.main;
            int used = 0;
            if (cam != null)
            {
                Vector2 half = root.rect.size * 0.5f;
                foreach (var g in GuardAI.All)
                {
                    if (g == null || g.IsKnockedOut) continue;
                    if (g.State == GuardState.Patrol && g.Suspicion < 0.05f) continue;
                    Vector3 sp = cam.WorldToScreenPoint(g.transform.position + Vector3.up * 2f);
                    bool behind = sp.z < 0f;
                    Vector2 local;
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(root, new Vector2(sp.x, sp.y), null, out local);
                    if (!behind && Mathf.Abs(local.x) < half.x && Mathf.Abs(local.y) < half.y) continue;
                    if (behind) local = -local;
                    if (local.sqrMagnitude < 1f) local = Vector2.down;
                    const float margin = 40f;
                    float k = Mathf.Min((half.x - margin) / Mathf.Max(Mathf.Abs(local.x), 0.001f), (half.y - margin) / Mathf.Max(Mathf.Abs(local.y), 0.001f));
                    Vector2 p = local * k;
                    if (used >= indicators.Count) MakeIndicator();
                    var rt = indicators[used];
                    rt.gameObject.SetActive(true);
                    rt.anchoredPosition = p;
                    Color col = GuardAI.ColorFor(g.State == GuardState.Patrol ? GuardState.Suspicious : g.State);
                    indicatorBars[used].color = col;
                    indicatorSymbols[used].text = g.State == GuardState.Alert ? "!" : "?";
                    indicatorSymbols[used].color = col;
                    float dist = player != null ? Vector3.Distance(player.transform.position, g.transform.position) : 0f;
                    indicatorDistances[used].text = $"{dist:0}m";
                    indicatorDistances[used].color = col;
                    used++;
                }
            }
            for (int i = used; i < indicators.Count; i++) indicators[i].gameObject.SetActive(false);
        }

        void UpdateResult(GameManager gm)
        {
            bool won = gm.CurrentResult == GameManager.Result.Won;
            resultTitle.text = won ? "임무 성공" : "임무 실패";
            resultTitle.color = won ? new Color(0.4f, 1f, 0.6f) : new Color(1f, 0.35f, 0.3f);
            string g = gm.Grade;
            resultGrade.text = g;
            resultGrade.color = g == "S" ? new Color(1f, 0.85f, 0.3f) : g == "A" ? new Color(0.4f, 1f, 0.6f) : g == "B" ? Accent : new Color(0.8f, 0.8f, 0.85f);
            string undetected = gm.Detections == 0 ? "<color=#7CFFB0>예 (한 번도 들키지 않음)</color>" : "아니오";
            resultStats.text =
                $"시간            <b>{FormatTime(gm.Elapsed)}</b>\n" +
                $"발각 횟수       <b>{gm.Detections}</b>\n" +
                $"기절시킨 경비   <b>{gm.Knockouts}</b>\n" +
                $"미발각          <b>{undetected}</b>\n" +
                (won ? $"점수            <b>{gm.Score}</b>" : $"사유            <b>{gm.CaughtBy}에게 붙잡힘</b>");
            var hist = PlayerBehaviorLog.History;
            var lines = new List<string>();
            for (int i = Mathf.Max(0, hist.Count - 5); i < hist.Count; i++) lines.Add(hist[i]);
            resultLog.text = "<color=#8FB8C8>적응형 경비 AI 기록</color>  (시도 " + PlayerBehaviorLog.Attempts + "회)\n" + (lines.Count > 0 ? string.Join("\n", lines) : "아직 학습한 행동이 없다");
            resultCheckpoint.gameObject.SetActive(!won && CheckpointData.Has);
            resultRestart.GetComponentInChildren<Text>().text = won ? "다시 플레이" : "처음부터 다시";
        }

        static int Severity(GuardState s) => s == GuardState.Alert ? 3 : s == GuardState.Search ? 2 : s == GuardState.Suspicious ? 1 : 0;

        static string StateName(GuardState s)
        {
            switch (s)
            {
                case GuardState.Suspicious: return "의심";
                case GuardState.Alert: return "경보/추격";
                case GuardState.Search: return "수색";
                default: return "순찰";
            }
        }

        static string FormatTime(float t)
        {
            int m = Mathf.FloorToInt(t / 60f);
            return $"{m:00}:{t - m * 60f:00.0}";
        }

        // ================= 미니맵 텍스처(NavMesh) =================

        void BuildMapTexture()
        {
            int w = Mathf.CeilToInt((mapMax.x - mapMin.x) * mapPixelsPerMeter);
            int h = Mathf.CeilToInt((mapMax.y - mapMin.y) * mapPixelsPerMeter);
            var walk = new float[w * h];
            for (int i = 0; i < walk.Length; i++) walk[i] = -999f;
            var tri = NavMesh.CalculateTriangulation();
            var v = tri.vertices;
            var idx = tri.indices;
            for (int t = 0; t + 2 < idx.Length; t += 3)
            {
                Vector3 a = v[idx[t]], b = v[idx[t + 1]], c = v[idx[t + 2]];
                Vector2 pa = ToPix(a), pb = ToPix(b), pc = ToPix(c);
                int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(pa.x, Mathf.Min(pb.x, pc.x))));
                int x1 = Mathf.Min(w - 1, Mathf.CeilToInt(Mathf.Max(pa.x, Mathf.Max(pb.x, pc.x))));
                int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(pa.y, Mathf.Min(pb.y, pc.y))));
                int y1 = Mathf.Min(h - 1, Mathf.CeilToInt(Mathf.Max(pa.y, Mathf.Max(pb.y, pc.y))));
                float area = Edge(pa, pb, pc);
                if (Mathf.Abs(area) < 1e-5f) continue;
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                        float w0 = Edge(pb, pc, p) / area, w1 = Edge(pc, pa, p) / area, w2 = Edge(pa, pb, p) / area;
                        if (w0 < -0.02f || w1 < -0.02f || w2 < -0.02f) continue;
                        float hy = a.y * w0 + b.y * w1 + c.y * w2;
                        int k = y * w + x;
                        if (hy > walk[k]) walk[k] = hy;
                    }
            }
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int k = y * w + x;
                    float hy = walk[k];
                    if (hy < -900f) { px[k] = new Color32(8, 11, 16, 215); continue; }
                    bool edge = false;
                    if (x == 0 || y == 0 || x == w - 1 || y == h - 1) edge = true;
                    else if (walk[k - 1] < -900f || walk[k + 1] < -900f || walk[k - w] < -900f || walk[k + w] < -900f) edge = true;
                    float wx = mapMin.x + (x + 0.5f) / mapPixelsPerMeter;
                    Color32 c;
                    if (hy > 1.5f) c = new Color32(70, 100, 128, 235);          // 캣워크
                    else if (wx < -15.5f) c = new Color32(34, 44, 54, 225);     // 야드
                    else c = new Color32(30, 62, 72, 230);                      // 실내
                    if (edge) c = new Color32(95, 170, 190, 245);
                    px[k] = c;
                }
            mapTex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "MinimapTex", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            mapTex.SetPixels32(px);
            mapTex.Apply(false, false);
        }

        Vector2 ToPix(Vector3 p) => new Vector2((p.x - mapMin.x) * mapPixelsPerMeter, (p.z - mapMin.y) * mapPixelsPerMeter);
        static float Edge(Vector2 a, Vector2 b, Vector2 c) => (c.x - a.x) * (b.y - a.y) - (c.y - a.y) * (b.x - a.x);

        // ================= 생성 =================

        void BuildCanvas()
        {
            try { font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "Noto Sans CJK KR", "NanumGothic", "Arial" }, 32); }
            catch { font = null; }
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var canvasGO = new GameObject("UICanvas");
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();
            root = canvasGO.GetComponent<RectTransform>();

            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }

            BuildHud();
            BuildTitle();
            BuildBriefing();
            BuildPause();
            BuildSettings();
            BuildResult();
            BuildPuzzles();
            BuildMakingOf();
            BuildCredits();
        }

        RectTransform NewRect(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        RectTransform Stretch(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        Image NewImage(string name, Transform parent, Color c)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        Image Panel(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color c)
        {
            var rt = NewRect(name, parent, anchor, pivot, pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        Text Label(Transform parent, string text, int size, Color color, TextAnchor align, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 box, FontStyle style = FontStyle.Normal)
        {
            var rt = NewRect("Text", parent, anchor, pivot, pos, box);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.fontStyle = style;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            var sh = rt.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.8f);
            sh.effectDistance = new Vector2(1.5f, -1.5f);
            return t;
        }

        Button MakeButton(Transform parent, string text, Vector2 anchor, Vector2 pos, Vector2 size, UnityAction onClick)
        {
            var rt = NewRect("Button_" + text, parent, anchor, new Vector2(0.5f, 0.5f), pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = Color.white;
            var btn = rt.gameObject.AddComponent<Button>();
            var cb = btn.colors;
            cb.normalColor = ButtonColor;
            cb.highlightedColor = new Color(0.18f, 0.42f, 0.55f, 1f);
            cb.pressedColor = new Color(0.3f, 0.65f, 0.8f, 1f);
            cb.selectedColor = ButtonColor;
            cb.disabledColor = new Color(0.1f, 0.12f, 0.14f, 0.6f);
            btn.colors = cb;
            var nav = btn.navigation;
            nav.mode = Navigation.Mode.None;
            btn.navigation = nav;
            btn.onClick.AddListener(() => AudioManager.Click());
            btn.onClick.AddListener(onClick);
            var accent = NewImage("Accent", rt, Accent);
            var art = accent.rectTransform;
            art.anchorMin = new Vector2(0f, 0f);
            art.anchorMax = new Vector2(0f, 1f);
            art.pivot = new Vector2(0f, 0.5f);
            art.sizeDelta = new Vector2(5f, 0f);
            art.anchoredPosition = Vector2.zero;
            var t = Label(rt, text, 28, Color.white, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
            t.raycastTarget = false;
            return btn;
        }

        Slider MakeSlider(Transform parent, string label, float min, float max, float y, out Text valueText, UnityAction<float> onChange)
        {
            Label(parent, label, 26, Color.white, TextAnchor.MiddleLeft, new Vector2(0.5f, 1f), new Vector2(0f, 0.5f), new Vector2(-300f, y), new Vector2(260f, 40f));
            var res = new DefaultControls.Resources();
            var go = DefaultControls.CreateSlider(res);
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(-20f, y);
            rt.sizeDelta = new Vector2(260f, 22f);
            foreach (var img in go.GetComponentsInChildren<Image>(true))
            {
                if (img.name == "Background") img.color = new Color(0.12f, 0.16f, 0.2f);
                else if (img.name == "Fill") img.color = Accent;
                else if (img.name == "Handle") { img.color = Color.white; img.rectTransform.sizeDelta = new Vector2(18f, 0f); }
            }
            var s = go.GetComponent<Slider>();
            s.minValue = min;
            s.maxValue = max;
            var nav = s.navigation; nav.mode = Navigation.Mode.None; s.navigation = nav;
            s.onValueChanged.AddListener(onChange);
            valueText = Label(parent, "", 24, Accent, TextAnchor.MiddleLeft, new Vector2(0.5f, 1f), new Vector2(0f, 0.5f), new Vector2(260f, y), new Vector2(120f, 40f));
            return s;
        }

        GameObject Screen(string name, Color dim)
        {
            var rt = Stretch(name, root);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = dim;
            img.raycastTarget = true;
            rt.gameObject.SetActive(false);
            return rt.gameObject;
        }

        // ---------------- HUD ----------------

        void BuildHud()
        {
            var h = Stretch("HUD", root);
            hud = h.gameObject;
            Vector2 top = new Vector2(0.5f, 1f), mid = new Vector2(0.5f, 0.5f);

            // 탐지 게이지(위 가운데)
            Panel("GaugeFrame", h, top, new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(460f, 58f), PanelColor);
            Panel("GaugeBack", h, top, new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(440f, 14f), new Color(1f, 1f, 1f, 0.12f));
            gaugeFill = Panel("GaugeFill", h, top, new Vector2(0f, 1f), new Vector2(-220f, -24f), new Vector2(0f, 14f), GuardAI.ColorFor(GuardState.Patrol));
            gaugeFillRect = gaugeFill.rectTransform;
            gaugeLabel = Label(h, "탐지 0%", 20, Color.white, TextAnchor.MiddleCenter, top, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(460f, 28f));

            // 배너(토스트)
            var bannerBg = Panel("Banner", h, top, new Vector2(0.5f, 1f), new Vector2(0f, -86f), new Vector2(900f, 48f), PanelColor);
            bannerGroup = bannerBg.gameObject.AddComponent<CanvasGroup>();
            bannerGroup.alpha = 0f;
            bannerText = Label(bannerBg.transform, "", 24, new Color(0.55f, 1f, 0.75f), TextAnchor.MiddleCenter, mid, mid, Vector2.zero, new Vector2(880f, 44f));

            // 미니맵(오른쪽 위) + 현재 목표
            float mmW = 330f, mmH = mmW * (mapMax.y - mapMin.y) / (mapMax.x - mapMin.x);
            Panel("MinimapFrame", h, Vector2.one, Vector2.one, new Vector2(-16f, -16f), new Vector2(mmW + 12f, mmH + 12f), PanelColor);
            var mmRt = NewRect("Minimap", h, Vector2.one, Vector2.one, new Vector2(-22f, -22f), new Vector2(mmW, mmH));
            minimap = mmRt.gameObject.AddComponent<RawImage>();
            minimap.raycastTarget = false;
            minimapRect = mmRt;
            mmGoal = Label(mmRt, "◆", 22, new Color(0.3f, 1f, 0.55f), TextAnchor.MiddleCenter, Vector2.zero, mid, Vector2.zero, new Vector2(30f, 30f)).rectTransform;
            mmExtract = Label(mmRt, "◎", 24, new Color(0.3f, 1f, 0.55f), TextAnchor.MiddleCenter, Vector2.zero, mid, Vector2.zero, new Vector2(30f, 30f)).rectTransform;
            mmPlayer = Label(mmRt, "▲", 20, Accent, TextAnchor.MiddleCenter, Vector2.zero, mid, Vector2.zero, new Vector2(26f, 26f), FontStyle.Bold).rectTransform;
            Label(h, "지도  ·  ◆ 목표  ▲ 나  ◇ 본 적 있는 경비", 15, new Color(0.7f, 0.8f, 0.85f), TextAnchor.UpperRight, Vector2.one, Vector2.one, new Vector2(-22f, -mmH - 32f), new Vector2(mmW, 22f));
            var objBg = Panel("ObjectiveFrame", h, Vector2.one, Vector2.one, new Vector2(-16f, -mmH - 60f), new Vector2(mmW + 12f, 78f), PanelColor);
            objectiveText = Label(objBg.transform, "", 21, Color.white, TextAnchor.MiddleLeft, mid, mid, Vector2.zero, new Vector2(mmW - 10f, 74f));

            // 상태·던질 물건(왼쪽 아래)
            var stBg = Panel("StatusFrame", h, Vector2.zero, Vector2.zero, new Vector2(16f, 16f), new Vector2(760f, 80f), PanelColor);
            statusText = Label(stBg.transform, "", 21, Color.white, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -8f), new Vector2(740f, 30f));
            throwText = Label(stBg.transform, "", 20, Color.white, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -42f), new Vector2(740f, 30f));

            // 안내 문구·무전 자막(아래 가운데)
            promptText = Label(h, "", 32, new Color(1f, 0.92f, 0.5f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 230f), new Vector2(900f, 44f), FontStyle.Bold);
            var radioBg = Panel("Radio", h, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 140f), new Vector2(1100f, 64f), new Color(0.02f, 0.04f, 0.03f, 0.85f));
            radioGroup = radioBg.gameObject.AddComponent<CanvasGroup>();
            radioGroup.alpha = 0f;
            var rIcon = Panel("RadioIcon", radioBg.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(6f, 64f), new Color(0.4f, 1f, 0.5f));
            rIcon.raycastTarget = false;
            radioText = Label(radioBg.transform, "", 25, new Color(0.75f, 1f, 0.8f), TextAnchor.MiddleCenter, mid, mid, Vector2.zero, new Vector2(1070f, 60f));

            // 도움말(왼쪽 가운데)
            var tutBg = Panel("Tutorial", h, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(16f, 40f), new Vector2(600f, 130f), new Color(0.03f, 0.07f, 0.1f, 0.9f));
            tutorialGroup = tutBg.gameObject.AddComponent<CanvasGroup>();
            tutorialGroup.alpha = 0f;
            Panel("TutAccent", tutBg.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(6f, 130f), new Color(1f, 0.85f, 0.3f));
            tutorialText = Label(tutBg.transform, "", 21, Color.white, TextAnchor.MiddleLeft, mid, mid, new Vector2(6f, 0f), new Vector2(570f, 124f));
        }

        void MakeIndicator()
        {
            var rt = NewRect("Indicator", hud.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(48f, 48f));
            var bg = rt.gameObject.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.65f);
            bg.raycastTarget = false;
            var bar = Panel("Bar", rt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(48f, 4f), Color.white);
            var sym = Label(rt, "?", 30, Color.white, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), new Vector2(48f, 36f), FontStyle.Bold);
            var dist = Label(rt, "", 15, Color.white, TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, -2f), new Vector2(70f, 20f));
            indicators.Add(rt);
            indicatorBars.Add(bar);
            indicatorSymbols.Add(sym);
            indicatorDistances.Add(dist);
        }

        // ---------------- 타이틀 ----------------

        void BuildTitle()
        {
            titleScreen = Screen("TitleScreen", new Color(0f, 0.01f, 0.03f, 0.35f));
            var t = titleScreen.transform;
            var left = new Vector2(0f, 0.5f);
            Panel("TitleShade", t, left, left, Vector2.zero, new Vector2(860f, 1400f), new Color(0f, 0.02f, 0.05f, 0.7f));
            Label(t, "SHADOW PROTOCOL", 96, Accent, TextAnchor.MiddleLeft, left, left, new Vector2(120f, 250f), new Vector2(1000f, 120f), FontStyle.Bold);
            Label(t, "적응형 AI 잠입 작전", 36, Color.white, TextAnchor.MiddleLeft, left, left, new Vector2(126f, 165f), new Vector2(900f, 50f));
            Label(t, "경비는 당신의 행동을 기억한다. 같은 길은 두 번 통하지 않는다.", 22, new Color(0.7f, 0.8f, 0.85f), TextAnchor.MiddleLeft, left, left, new Vector2(126f, 118f), new Vector2(900f, 34f));
            var gm = GameManager.Instance;
            MakeButton(t, "작전 시작", left, new Vector2(126f + 190f, -10f), new Vector2(380f, 62f), () => GameManager.Instance?.OpenBriefing());
            MakeButton(t, "제작 공정", left, new Vector2(126f + 190f, -82f), new Vector2(380f, 62f), () => { galleryOpen = true; SelectEntry(0); });
            MakeButton(t, "설정", left, new Vector2(126f + 190f, -154f), new Vector2(380f, 62f), () => { settingsOpen = true; RefreshSettingsControls(); });
            MakeButton(t, "크레딧", left, new Vector2(126f + 190f, -226f), new Vector2(380f, 62f), () => creditsOpen = true);
            MakeButton(t, "종료", left, new Vector2(126f + 190f, -298f), new Vector2(380f, 62f), () => GameManager.Instance?.QuitGame());
            Label(t, CreditLine, 19, new Color(0.7f, 0.82f, 0.88f), TextAnchor.LowerLeft, Vector2.zero, Vector2.zero, new Vector2(126f, 36f), new Vector2(1700f, 60f));
        }

        // ---------------- 브리핑 ----------------

        void BuildBriefing()
        {
            briefingScreen = Screen("BriefingScreen", new Color(0f, 0.01f, 0.03f, 0.55f));
            var t = briefingScreen.transform;
            var c = new Vector2(0.5f, 0.5f);
            var frame = Panel("BriefingFrame", t, c, c, Vector2.zero, new Vector2(1640f, 880f), PanelColor);
            var f = frame.transform;
            Vector2 tl = new Vector2(0f, 1f);
            Label(f, "작전 브리핑", 52, Accent, TextAnchor.UpperLeft, tl, tl, new Vector2(50f, -36f), new Vector2(800f, 70f), FontStyle.Bold);
            Label(f,
                "<b><color=#7CFFB0>목표</color></b>\n" +
                "1. 북동쪽 서버실의 <b>데이터 코어</b>를 확보한다.\n" +
                "2. 야드 남서쪽 <b>시작 지점</b>(녹색 표시)으로 돌아가 탈출한다.\n\n" +
                "<b><color=#FFD24D>정보</color></b>\n" +
                "· 경비 6명. 시야(바닥 부채꼴)와 소리로 감지한다. 어두운 곳에서는 덜 보인다.\n" +
                "· <b>적응형 경비 AI</b>: 자주 쓰는 경로·엄폐물을 기억해 다음 시도와 경보 후 배치를 바꾼다.\n" +
                "  환기구·하역문(정면)·사무동 출입문·캣워크·덕트 중 최소 3곳은 항상 열려 있다.\n" +
                "· 구역(홀·사무동·서버실)에 들어가면 자동 저장. 실패하면 마지막 체크포인트에서 다시.\n\n" +
                "<b><color=#59D9FF>조작</color></b>\n" +
                "WASD 이동 · 마우스 시점 · 휠 거리 · Shift 달리기 · Ctrl/C 웅크리기\n" +
                "우클릭 조준 + 좌클릭 던지기 · G 빠른 던지기 · E 등 뒤 제압 · F 기절체 끌기\n" +
                "Tab/V 전술 시점 · P/Esc 일시정지 · R 체크포인트 재시작",
                23, Color.white, TextAnchor.UpperLeft, tl, tl, new Vector2(50f, -120f), new Vector2(860f, 680f));
            float mw = 660f, mh = mw * (mapMax.y - mapMin.y) / (mapMax.x - mapMin.x);
            var mapRt = NewRect("BriefingMap", f, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-50f, -120f), new Vector2(mw, mh));
            var ri = mapRt.gameObject.AddComponent<RawImage>();
            ri.raycastTarget = false;
            Label(f, "구역 지도", 22, new Color(0.7f, 0.85f, 0.9f), TextAnchor.LowerLeft, new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(-50f - mw, -116f), new Vector2(300f, 30f));
            System.Action<string, Vector3, Color, int> mark = (txt, w, col, size) =>
            {
                var lt = Label(mapRt, txt, size, col, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(0.5f, 0.5f), MapPos(w, new Vector2(mw, mh)), new Vector2(200f, 30f), FontStyle.Bold);
                lt.horizontalOverflow = HorizontalWrapMode.Overflow;
            };
            Color zone = new Color(0.85f, 0.92f, 1f, 0.9f), route = new Color(1f, 0.85f, 0.35f);
            mark("야드", new Vector3(-27f, 0f, 1f), zone, 22);
            mark("메인 홀", new Vector3(0f, 0f, 2f), zone, 22);
            mark("사무동", new Vector3(-6f, 0f, 16.8f), zone, 18);
            mark("서버실", new Vector3(10.5f, 0f, 14f), zone, 18);
            mark("캣워크", new Vector3(0.5f, 0f, -8.6f), new Color(0.6f, 0.8f, 1f), 16);
            mark("환기구", new Vector3(-17.8f, 0f, -7.9f), route, 15);
            mark("하역문", new Vector3(-17.8f, 0f, -1f), route, 15);
            mark("사무동 문", new Vector3(-19f, 0f, 12.9f), route, 15);
            mark("덕트", new Vector3(4f, 0f, 20.6f), route, 15);
            mark("◆ 데이터 코어", goalPosition + new Vector3(0f, 0f, 1.8f), new Color(0.3f, 1f, 0.55f), 16);
            mark("◎ 시작·탈출", extractPosition + new Vector3(3f, 0f, -2.6f), new Color(0.3f, 1f, 0.55f), 16);
            MakeButton(f, "작전 개시", new Vector2(1f, 0f), new Vector2(-50f - 170f, 70f), new Vector2(340f, 70f), () => GameManager.Instance?.StartMission());
            MakeButton(f, "뒤로", new Vector2(1f, 0f), new Vector2(-50f - 340f - 30f - 110f, 70f), new Vector2(220f, 70f), () => GameManager.Instance?.BackToTitleScreen());
        }

        // ---------------- 일시정지 ----------------

        void BuildPause()
        {
            pauseScreen = Screen("PauseScreen", new Color(0f, 0f, 0f, 0.55f));
            var c = new Vector2(0.5f, 0.5f);
            var f = Panel("PauseFrame", pauseScreen.transform, c, c, Vector2.zero, new Vector2(560f, 720f), PanelColor).transform;
            Vector2 top = new Vector2(0.5f, 1f);
            Label(f, "일시정지", 48, Accent, TextAnchor.MiddleCenter, top, top, new Vector2(0f, -50f), new Vector2(500f, 70f), FontStyle.Bold);
            MakeButton(f, "계속하기", top, new Vector2(0f, -150f), new Vector2(420f, 64f), () => GameManager.Instance?.Resume());
            MakeButton(f, "설정", top, new Vector2(0f, -230f), new Vector2(420f, 64f), () => { settingsOpen = true; RefreshSettingsControls(); });
            pauseCheckpoint = MakeButton(f, "체크포인트에서 재시작", top, new Vector2(0f, -310f), new Vector2(420f, 64f), () => GameManager.Instance?.Restart());
            MakeButton(f, "처음부터 다시", top, new Vector2(0f, -390f), new Vector2(420f, 64f), () => GameManager.Instance?.RestartFromBeginning());
            MakeButton(f, "크레딧·제작 공정", top, new Vector2(0f, -470f), new Vector2(420f, 64f), () => creditsOpen = true);
            MakeButton(f, "타이틀로", top, new Vector2(0f, -550f), new Vector2(420f, 64f), () => GameManager.Instance?.QuitToTitle());
            Label(f, "P / Esc : 계속", 20, new Color(0.65f, 0.75f, 0.8f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(500f, 30f));
        }

        // ---------------- 설정 ----------------

        void BuildSettings()
        {
            settingsScreen = Screen("SettingsScreen", new Color(0f, 0f, 0f, 0.6f));
            var c = new Vector2(0.5f, 0.5f);
            var f = Panel("SettingsFrame", settingsScreen.transform, c, c, Vector2.zero, new Vector2(760f, 640f), PanelColor).transform;
            Vector2 top = new Vector2(0.5f, 1f);
            Label(f, "설정", 46, Accent, TextAnchor.MiddleCenter, top, top, new Vector2(0f, -50f), new Vector2(700f, 70f), FontStyle.Bold);
            sSens = MakeSlider(f, "마우스 감도", 0.5f, 6f, -140f, out vSens, v => GameSettings.MouseSensitivity = v);
            sMusic = MakeSlider(f, "음악 볼륨", 0f, 1f, -210f, out vMusic, v => GameSettings.MusicVolume = v);
            sSfx = MakeSlider(f, "효과음 볼륨", 0f, 1f, -280f, out vSfx, v => GameSettings.SfxVolume = v);
            sFov = MakeSlider(f, "시야각(FOV)", 50f, 85f, -350f, out vFov, v => GameSettings.Fov = Mathf.Round(v));
            Label(f, "마우스 Y축 반전", 26, Color.white, TextAnchor.MiddleLeft, top, new Vector2(0f, 0.5f), new Vector2(-300f, -420f), new Vector2(260f, 40f));
            var tg = DefaultControls.CreateToggle(new DefaultControls.Resources());
            tg.transform.SetParent(f, false);
            var trt = (RectTransform)tg.transform;
            trt.anchorMin = trt.anchorMax = top;
            trt.pivot = new Vector2(0f, 0.5f);
            trt.anchoredPosition = new Vector2(-20f, -420f);
            trt.sizeDelta = new Vector2(60f, 40f);
            foreach (var img in tg.GetComponentsInChildren<Image>(true))
            {
                if (img.name == "Background") { img.color = new Color(0.12f, 0.16f, 0.2f); img.rectTransform.sizeDelta = new Vector2(34f, 34f); }
                if (img.name == "Checkmark") { img.color = Accent; img.rectTransform.sizeDelta = new Vector2(-10f, -10f); }
            }
            var lbl = tg.GetComponentInChildren<Text>();
            if (lbl != null) lbl.text = "";
            tInvert = tg.GetComponent<Toggle>();
            tInvert.onValueChanged.AddListener(v => GameSettings.InvertY = v);
            MakeButton(f, "닫기", new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(320f, 64f), () => settingsOpen = false);
        }

        void RefreshSettingsControls()
        {
            if (sSens == null) return;
            sSens.SetValueWithoutNotify(GameSettings.MouseSensitivity);
            sMusic.SetValueWithoutNotify(GameSettings.MusicVolume);
            sSfx.SetValueWithoutNotify(GameSettings.SfxVolume);
            sFov.SetValueWithoutNotify(GameSettings.Fov);
            tInvert.SetIsOnWithoutNotify(GameSettings.InvertY);
            UpdateSettingsLabels();
        }

        void UpdateSettingsLabels()
        {
            vSens.text = GameSettings.MouseSensitivity.ToString("0.0");
            vMusic.text = Mathf.RoundToInt(GameSettings.MusicVolume * 100f) + "%";
            vSfx.text = Mathf.RoundToInt(GameSettings.SfxVolume * 100f) + "%";
            vFov.text = Mathf.RoundToInt(GameSettings.Fov) + "°";
        }

        // ---------------- 결과 ----------------

        void BuildResult()
        {
            resultScreen = Screen("ResultScreen", new Color(0f, 0f, 0f, 0.62f));
            var c = new Vector2(0.5f, 0.5f);
            var f = Panel("ResultFrame", resultScreen.transform, c, c, Vector2.zero, new Vector2(1180f, 780f), PanelColor).transform;
            Vector2 tl = new Vector2(0f, 1f), tr = new Vector2(1f, 1f);
            resultTitle = Label(f, "임무 성공", 64, Color.white, TextAnchor.UpperLeft, tl, tl, new Vector2(60f, -40f), new Vector2(800f, 90f), FontStyle.Bold);
            Label(f, "등급", 26, new Color(0.7f, 0.8f, 0.85f), TextAnchor.UpperCenter, tr, tr, new Vector2(-60f, -50f), new Vector2(260f, 40f));
            resultGrade = Label(f, "S", 200, Color.white, TextAnchor.UpperCenter, tr, tr, new Vector2(-60f, -80f), new Vector2(260f, 240f), FontStyle.Bold);
            resultStats = Label(f, "", 30, Color.white, TextAnchor.UpperLeft, tl, tl, new Vector2(70f, -150f), new Vector2(760f, 260f));
            resultLog = Label(f, "", 19, new Color(0.8f, 0.88f, 0.92f), TextAnchor.UpperLeft, tl, tl, new Vector2(70f, -420f), new Vector2(1050f, 170f));
            Vector2 b = new Vector2(0.5f, 0f);
            resultCheckpoint = MakeButton(f, "체크포인트에서 재시도", b, new Vector2(-370f, 70f), new Vector2(340f, 64f), () => GameManager.Instance?.Restart());
            resultRestart = MakeButton(f, "처음부터 다시", b, new Vector2(0f, 70f), new Vector2(340f, 64f), () => GameManager.Instance?.RestartFromBeginning());
            MakeButton(f, "타이틀로", b, new Vector2(370f, 70f), new Vector2(340f, 64f), () => GameManager.Instance?.QuitToTitle());
            Label(f, "R : 다시 시도", 18, new Color(0.6f, 0.7f, 0.75f), TextAnchor.MiddleRight, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 20f), new Vector2(300f, 26f));
        }
    

        // ================= 퍼즐 UI(키패드·배전반) =================

        GameObject keypadWin, breakerWin;
        KeypadPanel curKeypad;
        BreakerPanel curBreaker;
        Text keypadEntry, keypadSlots, keypadInfo, breakerInfo;
        readonly Image[] breakerCells = new Image[9];
        string entry = "";
        int breakerHint = -1;

        public bool PuzzleOpen => (keypadWin != null && keypadWin.activeSelf) || (breakerWin != null && breakerWin.activeSelf);
        public KeypadPanel OpenKeypadPanel => keypadWin != null && keypadWin.activeSelf ? curKeypad : null;
        public BreakerPanel OpenBreakerPanel => breakerWin != null && breakerWin.activeSelf ? curBreaker : null;

        void BuildPuzzles()
        {
            // 왼쪽 가운데(미니맵·목표·탐지 게이지를 가리지 않는다)
            Vector2 rc = new Vector2(0f, 0.5f), top = new Vector2(0.5f, 1f);
            // 키패드(오른쪽 가운데 — 게이지·경비 표시는 계속 보인다)
            var kp = Panel("KeypadWindow", root, rc, rc, new Vector2(40f, 10f), new Vector2(520f, 700f), new Color(0.03f, 0.05f, 0.08f, 0.96f));
            kp.raycastTarget = true;
            keypadWin = kp.gameObject;
            var k = kp.transform;
            Label(k, "보안 키패드", 38, Accent, TextAnchor.MiddleCenter, top, top, new Vector2(0f, -40f), new Vector2(480f, 50f), FontStyle.Bold);
            keypadSlots = Label(k, "", 30, Color.white, TextAnchor.MiddleCenter, top, top, new Vector2(0f, -100f), new Vector2(480f, 44f));
            keypadEntry = Label(k, "_ _ _ _", 54, new Color(0.6f, 1f, 0.7f), TextAnchor.MiddleCenter, top, top, new Vector2(0f, -160f), new Vector2(480f, 70f), FontStyle.Bold);
            for (int i = 0; i < 10; i++)
            {
                int d = i == 9 ? 0 : i + 1;
                int col = i == 9 ? 1 : i % 3, row = i == 9 ? 3 : i / 3;
                MakeButton(k, d.ToString(), top, new Vector2(-120f + col * 120f, -250f - row * 80f), new Vector2(104f, 68f), () => KeypadDigit(d));
            }
            MakeButton(k, "지우기", top, new Vector2(-120f, -490f), new Vector2(104f, 68f), () => { entry = ""; });
            MakeButton(k, "입력", top, new Vector2(120f, -490f), new Vector2(104f, 68f), KeypadSubmit);
            MakeButton(k, "힌트 (+20초·소음)", top, new Vector2(-110f, -570f), new Vector2(220f, 56f), () =>
            {
                if (curKeypad == null) return;
                string h = curKeypad.Hint();
                keypadInfo.text = h != null ? "힌트: " + h : "모든 조각을 이미 알고 있다";
            });
            MakeButton(k, "닫기 (Esc)", top, new Vector2(120f, -570f), new Vector2(200f, 56f), ClosePuzzle);
            keypadInfo = Label(k, "", 20, new Color(0.85f, 0.9f, 0.95f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(490f, 60f));
            keypadWin.SetActive(false);

            // 배전반 3×3
            var bp = Panel("BreakerWindow", root, rc, rc, new Vector2(40f, 10f), new Vector2(520f, 640f), new Color(0.03f, 0.05f, 0.08f, 0.96f));
            bp.raycastTarget = true;
            breakerWin = bp.gameObject;
            var b = bp.transform;
            Label(b, "배전반", 38, Accent, TextAnchor.MiddleCenter, top, top, new Vector2(0f, -40f), new Vector2(480f, 50f), FontStyle.Bold);
            Label(b, "스위치를 모두 <color=#5AE07A>초록</color>으로. 누른 칸과 상하좌우가 함께 바뀐다.", 20, Color.white, TextAnchor.MiddleCenter, top, top, new Vector2(0f, -92f), new Vector2(480f, 50f));
            for (int i = 0; i < 9; i++)
            {
                int idx = i;
                var btn = MakeButton(b, "", top, new Vector2(-120f + (i % 3) * 120f, -180f - (i / 3) * 112f), new Vector2(104f, 100f), () => { if (curBreaker != null) { curBreaker.Press(idx); breakerHint = -1; } });
                breakerCells[i] = btn.GetComponent<Image>();
                var cb = btn.colors; cb.normalColor = Color.white; cb.highlightedColor = new Color(0.85f, 0.85f, 0.85f); cb.selectedColor = Color.white; btn.colors = cb;
            }
            MakeButton(b, "힌트 (+15초·소음)", top, new Vector2(-110f, -530f), new Vector2(220f, 56f), () => { if (curBreaker != null) breakerHint = curBreaker.Hint(); });
            MakeButton(b, "닫기 (Esc)", top, new Vector2(120f, -530f), new Vector2(200f, 56f), ClosePuzzle);
            breakerInfo = Label(b, "", 20, new Color(0.85f, 0.9f, 0.95f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(490f, 50f));
            breakerWin.SetActive(false);
        }

        void BeginPuzzle()
        {
            if (player != null) { player.InputEnabled = false; player.ForceStand = true; }
            ThirdPersonCamera.LockCursor(false);
        }

        public void OpenKeypad(KeypadPanel k)
        {
            ClosePuzzle();
            curKeypad = k;
            entry = "";
            keypadInfo.text = "남은 조각은 사무동 경비실·사무실·창고에 있다. 틀리면 경고음!";
            keypadWin.SetActive(true);
            BeginPuzzle();
            if (GuardDirector.Instance != null) GuardDirector.Instance.RecordPuzzleUse(k.puzzleId, k.puzzleLabel);
        }

        public void OpenBreaker(BreakerPanel b)
        {
            ClosePuzzle();
            curBreaker = b;
            breakerHint = -1;
            breakerWin.SetActive(true);
            BeginPuzzle();
            if (GuardDirector.Instance != null) GuardDirector.Instance.RecordPuzzleUse(b.puzzleId, b.puzzleLabel);
        }

        public void ClosePuzzle()
        {
            bool was = PuzzleOpen;
            if (keypadWin != null) keypadWin.SetActive(false);
            if (breakerWin != null) breakerWin.SetActive(false);
            if (!was) return;
            if (player != null)
            {
                player.ForceStand = false;
                player.InputEnabled = GameManager.GameplayActive;
            }
            if (GameManager.GameplayActive) ThirdPersonCamera.LockCursor(true);
        }

        public void KeypadDigit(int d)
        {
            if (entry.Length < 4) entry += d.ToString();
        }

        public void KeypadSubmit()
        {
            if (curKeypad == null || entry.Length < 4) return;
            bool ok = curKeypad.Submit(entry);
            keypadInfo.text = ok ? "<color=#7CFFB0>해제됨</color>" : "<color=#FF6050>오답! 경고음이 울렸다</color>";
            entry = "";
            if (ok) ClosePuzzle();
        }

        void UpdatePuzzles(GameManager.FlowState flow)
        {
            if (!PuzzleOpen) return;
            if (flow != GameManager.FlowState.Playing && flow != GameManager.FlowState.Paused) { ClosePuzzle(); return; }
            if (GameManager.IsOver) { ClosePuzzle(); return; }
            if (keypadWin.activeSelf && curKeypad != null)
            {
                string s = "";
                for (int i = 0; i < 4; i++)
                {
                    int c = PuzzleState.ColorAt(i);
                    string d = PuzzleState.NotesRead.Contains(c) ? PuzzleState.DigitOf(c).ToString() : "?";
                    s += $"  <color=#{PuzzleState.ColorHex[c]}>{PuzzleState.Icons[c]}{d}</color>";
                }
                keypadSlots.text = "자리 순서:" + s;
                string e = "";
                for (int i = 0; i < 4; i++) e += (i < entry.Length ? entry[i].ToString() : "_") + (i < 3 ? " " : "");
                keypadEntry.text = e;
                if (Time.time < curKeypad.LockedUntil) keypadInfo.text = $"<color=#FF6050>오답 3회: {curKeypad.LockedUntil - Time.time:0}초 잠김</color>";
                if (flow == GameManager.FlowState.Playing)
                {
                    for (int d = 0; d <= 9; d++)
                        if (Input.GetKeyDown(KeyCode.Alpha0 + d) || Input.GetKeyDown(KeyCode.Keypad0 + d)) KeypadDigit(d);
                    if (Input.GetKeyDown(KeyCode.Backspace) && entry.Length > 0) entry = entry.Substring(0, entry.Length - 1);
                    if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) KeypadSubmit();
                }
            }
            if (breakerWin.activeSelf && curBreaker != null)
            {
                for (int i = 0; i < 9; i++)
                {
                    Color c = curBreaker.Cells[i] ? new Color(0.25f, 0.85f, 0.4f) : new Color(0.75f, 0.2f, 0.18f);
                    if (i == breakerHint) c = Color.Lerp(c, new Color(1f, 0.9f, 0.3f), 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f));
                    breakerCells[i].color = c;
                }
                breakerInfo.text = curBreaker.Solved ? "<color=#7CFFB0>차단 완료</color>" : $"누른 횟수 {curBreaker.Presses}   ·   푸는 동안 서 있어 노출된다!";
                if (curBreaker.Solved) ClosePuzzle();
            }
        }


        // ================= 제작 공정 갤러리·크레딧 =================

        /// <summary>사실과 맞는 제작 표기(레벨 구조는 Unity 기본 도형, 사운드는 코드 합성)</summary>
        public const string CreditLine =
            "All 3D models, characters and animations: VARCO 3D + Blender\n" +
            "캐릭터·소품 모델: VARCO 3D → Blender  ·  애니메이션: VARCO Animate  ·  환경(벽·바닥·펜스 등)·게임 장치·UI 표시물: Unity 기본 도형(환경 VARCO 모듈은 Blender 마무리 중)  ·  사운드: 코드 합성";

        GameObject makingOfScreen, creditsScreen;
        bool galleryOpen, creditsOpen;
        int galleryIndex, stageIndex;
        bool show3D;
        Text galTitle, galNote, galCaption, galStats, galPage, galStageText;
        Image galImage;
        RawImage galView;
        GameObject galImageGroup, galViewGroup;
        readonly System.Collections.Generic.List<Image> galButtons = new System.Collections.Generic.List<Image>();

        public bool GalleryOpen => galleryOpen;
        public void OpenGallery(int index, bool threeD) { galleryOpen = true; SelectEntry(index); show3D = threeD; }
        public void SetGalleryMode(int m) { if (gallery != null) gallery.SetMode((MakingOfGallery.ViewMode)m); show3D = true; }
        public void SetGalleryStage(int s) { stageIndex = s; show3D = false; }
        public void OpenCredits(bool on) { creditsOpen = on; }

        void BuildMakingOf()
        {
            makingOfScreen = Screen("MakingOfScreen", new Color(0.01f, 0.02f, 0.04f, 0.97f));
            var t = makingOfScreen.transform;
            Vector2 tl = new Vector2(0f, 1f), tr = new Vector2(1f, 1f), top = new Vector2(0.5f, 1f);
            Label(t, "제작 공정  ·  VARCO 3D + Blender", 44, Accent, TextAnchor.UpperLeft, tl, tl, new Vector2(48f, -28f), new Vector2(1200f, 60f), FontStyle.Bold);
            int n = gallery != null && gallery.entries != null ? gallery.entries.Length : 0;
            for (int i = 0; i < n; i++)
            {
                int idx = i;
                var e = gallery.entries[i];
                var b = MakeButton(t, e.title + (e.pending ? " (교체 예정)" : ""), tl, new Vector2(48f + 170f, -120f - i * 46f), new Vector2(340f, 40f), () => SelectEntry(idx));
                b.GetComponentInChildren<Text>().fontSize = 21;
                galButtons.Add(b.GetComponent<Image>());
            }
            galTitle = Label(t, "", 36, Color.white, TextAnchor.UpperLeft, tl, tl, new Vector2(440f, -100f), new Vector2(1000f, 50f), FontStyle.Bold);
            galNote = Label(t, "", 20, new Color(0.78f, 0.86f, 0.9f), TextAnchor.UpperLeft, tl, tl, new Vector2(440f, -150f), new Vector2(1400f, 60f));
            var area = NewRect("ViewArea", t, tl, tl, new Vector2(440f, -215f), new Vector2(1100f, 720f));
            galImageGroup = area.gameObject;
            var bg = area.gameObject.AddComponent<Image>(); bg.color = new Color(0.06f, 0.08f, 0.11f); bg.raycastTarget = false;
            galImage = NewImage("StageImage", area, Color.white);
            var irt = galImage.rectTransform; irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one; irt.offsetMin = new Vector2(10f, 10f); irt.offsetMax = new Vector2(-10f, -10f);
            galImage.preserveAspect = true;
            galStageText = Label(area, "", 26, new Color(0.88f, 0.94f, 1f), TextAnchor.MiddleLeft, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(980f, 620f));
            var view = NewRect("View3D", t, tl, tl, new Vector2(440f, -215f), new Vector2(1100f, 720f));
            galViewGroup = view.gameObject;
            galView = view.gameObject.AddComponent<RawImage>(); galView.raycastTarget = false;
            galCaption = Label(t, "", 24, new Color(1f, 0.9f, 0.55f), TextAnchor.UpperLeft, tl, tl, new Vector2(440f, -945f), new Vector2(1100f, 40f));
            galPage = Label(t, "", 22, Color.white, TextAnchor.UpperRight, tl, tl, new Vector2(440f, -945f), new Vector2(1100f, 40f));
            galStats = Label(t, "", 22, new Color(0.7f, 0.95f, 1f), TextAnchor.UpperLeft, tl, tl, new Vector2(440f, -985f), new Vector2(1100f, 40f));
            float x0 = -300f;
            MakeButton(t, "◀ 이전 단계", tr, new Vector2(x0 - 0f, -260f), new Vector2(260f, 56f), () => { show3D = false; stageIndex = Mathf.Max(0, stageIndex - 1); });
            MakeButton(t, "다음 단계 ▶", tr, new Vector2(x0, -326f), new Vector2(260f, 56f), () => { show3D = false; stageIndex++; });
            Label(t, "3D 턴테이블(실제 게임 모델)", 20, new Color(0.7f, 0.8f, 0.85f), TextAnchor.MiddleCenter, tr, tr, new Vector2(x0 + 130f, -396f), new Vector2(320f, 30f));
            MakeButton(t, "텍스처", tr, new Vector2(x0, -450f), new Vector2(260f, 56f), () => SetGalleryMode(0));
            MakeButton(t, "회색 클레이", tr, new Vector2(x0, -516f), new Vector2(260f, 56f), () => SetGalleryMode(1));
            MakeButton(t, "와이어프레임", tr, new Vector2(x0, -582f), new Vector2(260f, 56f), () => SetGalleryMode(2));
            MakeButton(t, "닫기", tr, new Vector2(x0, -980f), new Vector2(260f, 56f), () => galleryOpen = false);
            makingOfScreen.SetActive(false);
        }

        void SelectEntry(int i)
        {
            galleryIndex = i;
            stageIndex = 0;
            show3D = false;
            if (gallery != null) gallery.Show(i);
        }

        void UpdateMakingOf()
        {
            if (gallery == null || gallery.entries == null || gallery.entries.Length == 0) return;
            var e = gallery.entries[Mathf.Clamp(galleryIndex, 0, gallery.entries.Length - 1)];
            if (gallery.Current != galleryIndex) gallery.Show(galleryIndex);
            for (int i = 0; i < galButtons.Count; i++) galButtons[i].color = i == galleryIndex ? new Color(0.55f, 0.85f, 1f) : Color.white;
            galTitle.text = e.title + (e.pending ? "  <size=22><color=#FFC04D>고품질 리빌드로 교체 예정</color></size>" : "");
            galNote.text = e.note;
            int stages = e.stages != null ? e.stages.Length : 0;
            if (stages == 0) show3D = true;
            stageIndex = Mathf.Clamp(stageIndex, 0, Mathf.Max(0, stages - 1));
            galImageGroup.SetActive(!show3D);
            galViewGroup.SetActive(show3D);
            if (show3D)
            {
                galView.texture = gallery.Texture;
                string[] modes = { "텍스처", "회색 클레이", "와이어프레임" };
                galCaption.text = "게임 안 모델 턴테이블  ·  " + modes[(int)gallery.Mode];
                galPage.text = stages > 0 ? "◀ 이전 단계로 공정 이미지" : "공정 이미지 자료 없음";
            }
            else
            {
                var s = e.stages[stageIndex];
                galImage.sprite = s.image;
                bool hasText = !string.IsNullOrEmpty(s.text);
                galImage.enabled = s.image != null || !hasText;
                galImage.color = s.image != null ? Color.white : new Color(0.15f, 0.17f, 0.2f);
                galStageText.text = s.image == null && hasText ? s.text : "";
                galCaption.text = $"{stageIndex + 1}. {s.caption}" + (s.image == null && !hasText ? "  (자료 없음)" : "");
                galPage.text = $"{stageIndex + 1} / {stages}   → 마지막: 3D 턴테이블";
                if (stageIndex >= stages - 1 && Input.GetKeyDown(KeyCode.RightArrow)) show3D = true;
            }
            galStats.text = gallery.Stats;
            if (Input.GetKeyDown(KeyCode.RightArrow) && !show3D) { if (stageIndex < stages - 1) stageIndex++; else show3D = true; }
            if (Input.GetKeyDown(KeyCode.LeftArrow)) { if (show3D && stages > 0) show3D = false; else stageIndex = Mathf.Max(0, stageIndex - 1); }
        }

        void BuildCredits()
        {
            creditsScreen = Screen("CreditsScreen", new Color(0.01f, 0.02f, 0.04f, 0.95f));
            var c = new Vector2(0.5f, 0.5f);
            var f = Panel("CreditsFrame", creditsScreen.transform, c, c, Vector2.zero, new Vector2(1300f, 860f), PanelColor).transform;
            Vector2 top = new Vector2(0.5f, 1f);
            Label(f, "크레딧", 48, Accent, TextAnchor.MiddleCenter, top, top, new Vector2(0f, -50f), new Vector2(1200f, 70f), FontStyle.Bold);
            Label(f,
                "<b>SHADOW PROTOCOL</b>  —  적응형 AI 잠입 작전\n\n" +
                "<b><color=#59D9FF>All 3D models, characters and animations: VARCO 3D + Blender</color></b>\n" +
                "· 플레이어·경비병 3종 캐릭터: VARCO 3D 생성 → Blender 정리·리깅\n" +
                "· 소품 17종(컨테이너·지게차·서버·사물함·선반·상자·데이터 코어 등): VARCO 3D 생성 → Blender 정리\n" +
                "· 캐릭터 애니메이션 9종: VARCO Animate\n\n" +
                "<b>그 밖의 제작 방식(정확한 표기)</b>\n" +
                "· 환경(벽·바닥·펜스·캣워크·가로등·트러스): 현재 Unity 기본 도형 + 코드 생성 텍스처 — VARCO 3D 모듈을 Blender에서 마무리하는 중(벽 모듈은 제작 대기)\n" +
                "· 보안문·키패드·배전반·환풍기 등 퍼즐 장치, 던지는 병·캔, 레이저 빔: Unity 기본 도형\n" +
                "· 시야 원뿔·소음 고리·상태 표시 등 UI 표시물: Unity 기본 도형·라인 렌더러\n" +
                "· 사운드·음악: 코드로 합성(절차적 생성, 외부 음원 없음)\n" +
                "· UI: Unity uGUI(코드 생성), 글꼴: 운영체제 맑은 고딕\n" +
                "· 게임 로직·적응형 경비 AI: Unity C#\n\n" +
                "제작 공정은 타이틀의 '제작 공정'에서 모델별로 볼 수 있다.",
                24, Color.white, TextAnchor.UpperLeft, top, top, new Vector2(0f, -120f), new Vector2(1180f, 600f));
            MakeButton(f, "제작 공정 보기", new Vector2(0.5f, 0f), new Vector2(-180f, 60f), new Vector2(320f, 62f), () => { creditsOpen = false; galleryOpen = true; SelectEntry(0); });
            MakeButton(f, "닫기", new Vector2(0.5f, 0f), new Vector2(180f, 60f), new Vector2(320f, 62f), () => creditsOpen = false);
            creditsScreen.SetActive(false);
        }
}
}
