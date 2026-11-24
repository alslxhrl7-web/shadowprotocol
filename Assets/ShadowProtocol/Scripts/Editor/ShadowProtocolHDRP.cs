using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace ShadowProtocol.EditorTools
{
    /// <summary>
    /// HDRP 전환(2026-11-23, 사용자 결정: 사실적인 화면).
    /// 빌더는 지금처럼 Standard 재질·빌트인 단위 조명으로 씬을 만들고, 마지막에 ApplyToScene()이
    /// 1) 씬이 참조하는 Standard 재질을 HDRP/Lit로 바꾸고(금속·AO·매끈함을 마스크 맵으로 묶음, 발광은 nits),
    /// 2) 조명을 물리 단위(칸델라·럭스)와 실제 기구 모양(가로등·투광등·천장등 = 아래로 비추는 스포트, 그림자)으로 바꾸고,
    /// 3) 밤 볼륨(하늘·볼류메트릭 안개·자동 노출·ACES·SSAO·SSGI·SSR·접촉 그림자, 모션 블러는 끔)·구역별 반사 프로브와 카메라(TAA)를 붙인다.
    /// 여러 번 실행해도 결과가 같다(알려진 조명은 절댓값, 이미 HDRP인 재질은 건너뜀).
    /// </summary>
    public static class ShadowProtocolHDRP
    {
        public const string Folder = "Assets/ShadowProtocol/Rendering";
        public const string PipelineAssetPath = Folder + "/SP_HDRenderPipeline.asset";
        public const string ProfilePath = Folder + "/SP_NightVolumeProfile.asset";

        // ---------------------------------------------------------------- 파이프라인

        [MenuItem("ShadowProtocol/HDRP/Setup Pipeline")]
        public static HDRenderPipelineAsset EnsurePipeline()
        {
            EnsureFolder(Folder);
            if (PlayerSettings.colorSpace != ColorSpace.Linear) PlayerSettings.colorSpace = ColorSpace.Linear;

            var asset = AssetDatabase.LoadAssetAtPath<HDRenderPipelineAsset>(PipelineAssetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<HDRenderPipelineAsset>();
                AssetDatabase.CreateAsset(asset, PipelineAssetPath);
            }

            var s = asset.currentPlatformRenderPipelineSettings;
            s.supportSSAO = true;
            s.supportSSR = true;
            s.supportSSGI = true;
            s.supportVolumetrics = true;
            s.supportMotionVectors = true;
            s.supportDecals = true;
            s.supportShadowMask = false;
            s.supportLightLayers = true; // 인물 전용 보조광(CharacterLayer)
            s.supportedLitShaderMode = RenderPipelineSettings.SupportedLitShaderMode.DeferredOnly; // 조명이 많은 실내·야외 → 디퍼드
            s.hdShadowInitParams.supportContactShadows = true;
            s.hdShadowInitParams.maxShadowRequests = 128;
            s.hdShadowInitParams.punctualLightShadowAtlas.shadowAtlasResolution = 8192;
            s.hdShadowInitParams.directionalShadowsDepthBits = DepthBits.Depth32;
            asset.currentPlatformRenderPipelineSettings = s;
            EditorUtility.SetDirty(asset);

            if (GraphicsSettings.defaultRenderPipeline != asset) GraphicsSettings.defaultRenderPipeline = asset;
            int cur = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                if (QualitySettings.renderPipeline != null) QualitySettings.renderPipeline = null; // 모든 단계가 기본(HDRP) 자산을 쓴다
            }
            QualitySettings.SetQualityLevel(cur, false);
            AssetDatabase.SaveAssets();
            return asset;
        }

        [MenuItem("ShadowProtocol/HDRP/Apply To Open Scene")]
        public static void ApplyToOpenSceneMenu()
        {
            ApplyToScene();
            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>빌더 마지막 단계에서 호출(씬 저장 전).</summary>
        public static void ApplyToScene()
        {
            EnsurePipeline();
            int mats = ConvertSceneMaterials();
            int lights = ConvertLights();
            int chars = SetupCharacterLighting();
            SetupVolume();
            int probes = SetupReflectionProbes();
            SetupCameras();
            DisableLegacyFog();
            AssetDatabase.SaveAssets();
            Debug.Log($"[ShadowProtocol] HDRP 적용: 재질 {mats}개 변환, 조명 {lights}개 물리 단위로 설정, 인물 보조광 {chars}명, 반사 프로브 {probes}개, 밤 볼륨·TAA 카메라 설정");
        }

        // ---------------------------------------------------------------- 재질

        static Shader litShader, unlitShader;

        /// <summary>씬이 참조하는 모든 재질(렌더러 + 컴포넌트가 들고 있는 재질 + 프리팹)을 HDRP로</summary>
        public static int ConvertSceneMaterials()
        {
            litShader = Shader.Find("HDRP/Lit");
            unlitShader = Shader.Find("HDRP/Unlit");
            var scene = EditorSceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();
            var deps = EditorUtility.CollectDependencies(roots);
            var set = new HashSet<Material>();
            foreach (var o in deps) if (o is Material m) set.Add(m);
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/ShadowProtocol/Materials" }))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (m != null) set.Add(m);
            }
            int n = 0;
            foreach (var m in set)
                if (ConvertMaterial(m)) n++;

            // 정점 색 반투명 표시(Sprites/Default: 시야 원뿔·소음 원·던지기 궤적)는 HDRP 노출을 받지 않아
            // 어두운 밤 화면에서 혼자 밝게 뜬다 → 색조로 낮춘다(게임 표시라 노출과 무관하게 일정한 밝기는 유지).
            foreach (var m in set)
            {
                if (m == null || m.shader == null || m.shader.name != "Sprites/Default") continue;
                if (m.name == "VisionCone") m.color = new Color(0.42f, 0.42f, 0.42f, 0.5f);
                else m.color = new Color(0.75f, 0.75f, 0.75f, 0.85f);
                EditorUtility.SetDirty(m);
            }
            return n;
        }

        /// <summary>Standard(빌트인) → HDRP/Lit. 이미 HDRP면 false.</summary>
        public static bool ConvertMaterial(Material m)
        {
            if (m == null || m.shader == null) return false;
            if (litShader == null) litShader = Shader.Find("HDRP/Lit");
            string sh = m.shader.name;
            if (sh != "Standard" && sh != "Standard (Specular setup)" && sh != "Legacy Shaders/Diffuse" && sh != "Diffuse") return false;
            string path = AssetDatabase.GetAssetPath(m);
            if (string.IsNullOrEmpty(path) || path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)) return false; // FBX 내장 재질은 바꿀 수 없음(remap된 재질만 씀)

            // ---- Standard 값 읽기(셰이더를 바꾸기 전에)
            Color color = m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
            Texture albedo = Tex(m, "_MainTex");
            Vector2 tile = m.HasProperty("_MainTex") ? m.GetTextureScale("_MainTex") : Vector2.one;
            Vector2 off = m.HasProperty("_MainTex") ? m.GetTextureOffset("_MainTex") : Vector2.zero;
            Texture normal = Tex(m, "_BumpMap");
            float normalScale = m.HasProperty("_BumpScale") ? m.GetFloat("_BumpScale") : 1f;
            Texture ms = Tex(m, "_MetallicGlossMap");
            Texture occ = Tex(m, "_OcclusionMap");
            float occStrength = m.HasProperty("_OcclusionStrength") ? m.GetFloat("_OcclusionStrength") : 1f;
            float metallic = m.HasProperty("_Metallic") ? m.GetFloat("_Metallic") : 0f;
            float gloss = m.HasProperty("_Glossiness") ? m.GetFloat("_Glossiness") : 0.3f;
            float glossScale = m.HasProperty("_GlossMapScale") ? m.GetFloat("_GlossMapScale") : 1f;
            bool smoothFromAlbedo = m.HasProperty("_SmoothnessTextureChannel") && m.GetFloat("_SmoothnessTextureChannel") > 0.5f;
            bool emissive = m.IsKeywordEnabled("_EMISSION");
            Color emission = m.HasProperty("_EmissionColor") ? m.GetColor("_EmissionColor") : Color.black;
            Texture emissionMap = Tex(m, "_EmissionMap");
            float mode = m.HasProperty("_Mode") ? m.GetFloat("_Mode") : 0f;
            float cutoff = m.HasProperty("_Cutoff") ? m.GetFloat("_Cutoff") : 0.5f;
            bool instancing = m.enableInstancing;

            Texture2D mask = null;
            if (ms != null || occ != null || (smoothFromAlbedo && albedo != null))
                mask = PackMask(m.name, path, ms, occ, smoothFromAlbedo ? albedo : null, metallic, gloss);

            // ---- HDRP/Lit
            m.shader = litShader;
            m.SetColor("_BaseColor", color);
            m.SetTexture("_BaseColorMap", albedo);
            m.SetTextureScale("_BaseColorMap", tile);
            m.SetTextureOffset("_BaseColorMap", off);
            m.SetTexture("_NormalMap", normal);
            m.SetFloat("_NormalScale", normalScale);
            m.SetTexture("_MaskMap", mask);
            if (mask != null)
            {
                m.SetFloat("_MetallicRemapMin", 0f);
                m.SetFloat("_MetallicRemapMax", ms != null ? 1f : metallic);
                m.SetFloat("_SmoothnessRemapMin", 0f);
                m.SetFloat("_SmoothnessRemapMax", (ms != null || smoothFromAlbedo) ? glossScale : 1f);
                m.SetFloat("_AORemapMin", 1f - Mathf.Clamp01(occStrength));
                m.SetFloat("_AORemapMax", 1f);
            }
            else
            {
                m.SetFloat("_Metallic", metallic);
                m.SetFloat("_Smoothness", gloss);
            }

            // 투명·컷아웃
            HDMaterial.SetAlphaClipping(m, mode > 0.5f && mode < 1.5f);
            if (mode > 0.5f && mode < 1.5f) HDMaterial.SetAlphaCutoff(m, cutoff);
            bool transparent = mode > 1.5f;
            HDMaterial.SetSurfaceType(m, transparent);
            if (transparent)
            {
                m.SetFloat("_BlendMode", 0f); // Alpha
                if (m.HasProperty("_EnableBlendModePreserveSpecularLighting")) m.SetFloat("_EnableBlendModePreserveSpecularLighting", 0f); // 사라질 때 반사광도 같이
            }

            // 발광(nits)
            if (emissive && emission.maxColorComponent > 0.0001f)
            {
                float strength = emission.maxColorComponent;
                var ldr = new Color(emission.r / strength, emission.g / strength, emission.b / strength, 1f);
                HDMaterial.SetUseEmissiveIntensity(m, true);
                HDMaterial.SetEmissiveColor(m, ldr);
                HDMaterial.SetEmissiveIntensity(m, EmissionNits(m.name, strength), EmissiveIntensityUnit.Nits);
                m.SetTexture("_EmissiveColorMap", emissionMap);
            }
            else
            {
                HDMaterial.SetUseEmissiveIntensity(m, true);
                HDMaterial.SetEmissiveColor(m, Color.black);
                HDMaterial.SetEmissiveIntensity(m, 0f, EmissiveIntensityUnit.Nits);
                m.SetTexture("_EmissiveColorMap", null);
            }

            m.enableInstancing = instancing;
            HDMaterial.ValidateMaterial(m);
            EditorUtility.SetDirty(m);
            return true;
        }

        /// <summary>실제 광원 기구의 발광면은 수천 nits, 화면·표시등은 SPRender 배율(게임 표시가 노출에 묻히지 않게)</summary>
        static float EmissionNits(string matName, float strength)
        {
            string n = matName.ToLowerInvariant();
            if (n.Contains("lamphead") || n.Contains("lens") || n.Contains("worklight") || n.Contains("bulb")) return 4000f * Mathf.Max(0.5f, strength / 2.5f);
            // 넓은 바닥 표시(시작·탈출 지점): 실제로는 도색 + 약한 LED 띠. 크게 빛나면 화면 GI로 주변이 통째로 물든다.
            if (n.Contains("startpad")) return 0.15f;
            if (n.Contains("extractmarker")) return 0.5f * strength;
            return strength * ShadowProtocol.SPRender.NitsPerEmissionUnit;
        }

        static Texture Tex(Material m, string prop) => m.HasProperty(prop) ? m.GetTexture(prop) : null;

        /// <summary>HDRP 마스크 맵(R 금속, G AO, B 디테일 마스크, A 매끈함)을 원본 맵들로 굽는다. 원본보다 새로우면 다시 쓰지 않음.</summary>
        static Texture2D PackMask(string matName, string matPath, Texture ms, Texture occ, Texture albedoForSmooth, float metallic, float gloss)
        {
            Texture refTex = ms != null ? ms : (occ != null ? occ : albedoForSmooth);
            string refPath = AssetDatabase.GetAssetPath(refTex);
            string dir = Path.GetDirectoryName(string.IsNullOrEmpty(refPath) || refPath.StartsWith("Resources/") || refPath.StartsWith("Library/") ? matPath : refPath).Replace('\\', '/');
            string outPath = $"{dir}/{Sanitize(matName)}_HDRPMask.png";

            System.DateTime newest = System.DateTime.MinValue;
            foreach (var t in new[] { ms, occ, albedoForSmooth })
            {
                if (t == null) continue;
                var p = AssetDatabase.GetAssetPath(t);
                if (!string.IsNullOrEmpty(p) && File.Exists(p)) { var w = File.GetLastWriteTimeUtc(p); if (w > newest) newest = w; }
            }
            if (File.Exists(outPath) && File.GetLastWriteTimeUtc(outPath) >= newest)
                return AssetDatabase.LoadAssetAtPath<Texture2D>(outPath);

            int wdt = refTex.width, hgt = refTex.height;
            Color[] msPx = ms != null ? ReadPixels(ms, wdt, hgt) : null;
            Color[] occPx = occ != null ? ReadPixels(occ, wdt, hgt) : null;
            Color[] albPx = albedoForSmooth != null ? ReadPixels(albedoForSmooth, wdt, hgt) : null;
            var outPx = new Color[wdt * hgt];
            for (int i = 0; i < outPx.Length; i++)
            {
                float r = msPx != null ? msPx[i].r : metallic;
                float g = occPx != null ? occPx[i].g : 1f;
                float a = albPx != null ? albPx[i].a : (msPx != null ? msPx[i].a : gloss);
                outPx[i] = new Color(r, g, 1f, a);
            }
            var tex = new Texture2D(wdt, hgt, TextureFormat.RGBA32, false, true);
            tex.SetPixels(outPx);
            tex.Apply();
            File.WriteAllBytes(outPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(outPath, ImportAssetOptions.ForceUpdate);
            var imp = AssetImporter.GetAtPath(outPath) as TextureImporter;
            if (imp != null)
            {
                imp.sRGBTexture = false;
                imp.alphaSource = TextureImporterAlphaSource.FromInput;
                imp.mipmapEnabled = true;
                imp.maxTextureSize = Mathf.Max(wdt, hgt) > 2048 ? 4096 : 2048;
                imp.textureCompression = TextureImporterCompression.CompressedHQ;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(outPath);
        }

        static Color[] ReadPixels(Texture t, int w, int h)
        {
            bool srgb = false;
            if (AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(t)) is TextureImporter ti) srgb = ti.sRGBTexture;
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, srgb ? RenderTextureReadWrite.sRGB : RenderTextureReadWrite.Linear);
            Graphics.Blit(t, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tmp = new Texture2D(w, h, TextureFormat.RGBA32, false, !srgb);
            tmp.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tmp.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            var px = tmp.GetPixels();
            Object.DestroyImmediate(tmp);
            return px;
        }

        static string Sanitize(string s)
        {
            foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s;
        }

        // ---------------------------------------------------------------- 조명

        struct LightSpec
        {
            public LightType type;
            public float candela;      // 점·스포트(칸델라). 방향광은 lux
            public float range;
            public float spotAngle;    // 바깥 원뿔(도)
            public float innerPercent; // 안쪽 원뿔(%)
            public int shadowRes;      // 0 = 그림자 없음
            public bool aimDown;
            public bool specular;
            public float volumetric;   // 볼류메트릭 안개 기여(0~1)
        }

        /// <summary>
        /// 기구별 실측에 가까운 값. 목표 조도: 가로등(5.7m) 아래 약 25 lux, 홀 투광등(4.7m) 아래 약 70 lux,
        /// 사무 천장등(2.5m) 아래 약 80 lux, 달빛 0.6 lux(만월 0.1~0.3 lux보다 약간 밝게 — 플레이 가독성).
        /// </summary>
        static bool Spec(Light l, string path, out LightSpec s)
        {
            s = new LightSpec { type = l.type, range = l.range, spotAngle = 120f, innerPercent = 30f, specular = true, volumetric = 1f };
            if (l.type == LightType.Directional) { s.candela = 0.6f; s.shadowRes = 2048; return true; }
            if (path.Contains("StreetLamps")) { s.type = LightType.Spot; s.candela = 820f; s.range = 24f; s.spotAngle = 135f; s.innerPercent = 25f; s.shadowRes = 1024; s.aimDown = true; return true; }
            if (path.Contains("WorkLights")) { s.type = LightType.Spot; s.candela = 1550f; s.range = 20f; s.spotAngle = 120f; s.innerPercent = 35f; s.shadowRes = 1024; s.aimDown = true; return true; }
            if (path.Contains("AnnexLights")) { s.type = LightType.Spot; s.candela = 500f; s.range = 11f; s.spotAngle = 150f; s.innerPercent = 20f; s.shadowRes = 512; s.aimDown = true; s.volumetric = 0.4f; return true; }
            // 인물 전용 보조광(SetupCharacterLighting이 인물 레이어만 비추게 함): 배경은 어둡게 두고 인물만 읽히게(2026-11-23 사용자 지시)
            if (path.EndsWith("PlayerFillLight")) { s.candela = PlayerFillCandela; s.range = 5f; s.volumetric = 0f; return true; }
            if (path.EndsWith("CharacterFill")) { s.candela = GuardFillCandela; s.range = 4.5f; s.volumetric = 0f; return true; }
            if (path.EndsWith("Goal_DataCore/Glow")) { s.candela = 30f; s.range = 7f; s.shadowRes = 0; s.volumetric = 0.5f; return true; }
            if (path.EndsWith("Beacon")) { s.candela = 40f; s.range = 8f; s.volumetric = 0.6f; return true; }
            return false;
        }

        public static int ConvertLights()
        {
            int n = 0;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
            {
                if (l.gameObject.scene != EditorSceneManager.GetActiveScene()) continue;
                string path = PathOf(l.transform);
                var hd = l.GetComponent<HDAdditionalLightData>();
                if (hd == null) { hd = l.gameObject.AddComponent<HDAdditionalLightData>(); HDAdditionalLightData.InitDefaultHDAdditionalLightData(hd); }

                if (!Spec(l, path, out var s))
                {
                    if (l.lightUnit == LightUnit.Candela && l.intensity > 2.5f) { n++; continue; } // 이미 변환됨(다시 실행)
                    // 알 수 없는 작은 표시등: 빌트인 강도 1 ≈ 거리 range/3에서 20 lux
                    float d = Mathf.Max(0.3f, l.range / 3f);
                    s.candela = Mathf.Max(1f, l.intensity * 20f * d * d);
                    s.shadowRes = 0;
                    s.volumetric = 0.3f;
                }

                l.type = s.type;
                l.range = s.range;
                if (s.type == LightType.Spot)
                {
                    l.spotAngle = s.spotAngle;
                    l.innerSpotAngle = s.spotAngle * s.innerPercent / 100f;
                    l.enableSpotReflector = false; // 칸델라를 그대로(원뿔 각도와 무관한 광도)
                }
                if (s.aimDown) l.transform.rotation = Quaternion.Euler(90f, l.transform.eulerAngles.y, 0f);

                if (s.type == LightType.Directional) { l.lightUnit = LightUnit.Lux; l.intensity = s.candela; }
                else { l.lightUnit = LightUnit.Candela; l.intensity = s.candela; }

                l.renderMode = LightRenderMode.Auto;
                l.lightmapBakeType = LightmapBakeType.Realtime;
                if (s.shadowRes > 0)
                {
                    l.shadows = LightShadows.Soft;
                    hd.SetShadowResolutionOverride(true);
                    hd.SetShadowResolution(s.shadowRes);
                    hd.shadowNearPlane = 0.15f;
                    hd.useContactShadow.@override = true;
                    hd.useContactShadow.useOverride = true;
                }
                else l.shadows = LightShadows.None;

                hd.affectSpecular = s.specular;
                hd.volumetricDimmer = s.volumetric;
                hd.affectsVolumetric = s.volumetric > 0f;
                if (s.type == LightType.Directional)
                {
                    hd.angularDiameter = 0.5f;   // 달 시직경
                    hd.interactsWithSky = false; // 그라디언트 하늘이라 해/달 원반을 그리지 않음
                }
                else
                {
                    l.shapeRadius =path.Contains("StreetLamps") || path.Contains("WorkLights") ? 0.12f : 0.04f; // 기구 크기 → 부드러운 그림자 가장자리
                }
                EditorUtility.SetDirty(l);
                EditorUtility.SetDirty(hd);
                n++;
            }
            return n;
        }

        // ---------------------------------------------------------------- 인물 전용 보조광

        /// <summary>렌더링 레이어 1번 비트 = 인물. 인물 모델은 기본(0번) + 인물(1번)을 받고, 보조광은 1번만 비춘다.</summary>
        public const uint CharacterLayerBit = 1u << 1;
        // 2026-11-24: 16 → 6 cd, 12 → 4.5 cd. 배경보다 인물이 훨씬 밝아 평평하게 바래 보였음(사용자: "왜 캐릭터가 뿌옇게 나와?").
        const float PlayerFillCandela = 6f;  // 카메라 쪽 위(뒤 0.6m, 높이 2.2m)에서 몸까지 약 1.2m → 약 4 lux
        const float GuardFillCandela = 4.5f; // 머리 위 앞(높이 2.5m, 앞 0.8m) → 몸통에 약 2 lux

        /// <summary>
        /// 배경은 어두운 밤 그대로 두고 인물(플레이어·경비)만 보이게: 인물 렌더러를 인물 레이어에 넣고,
        /// 인물 레이어만 비추는 그림자 없는 약한 보조광을 각 인물에 붙인다. 바닥·벽·안개에는 닿지 않는다.
        /// </summary>
        public static int SetupCharacterLighting()
        {
            int n = 0;
            var player = Object.FindAnyObjectByType<PlayerController>();
            if (player != null)
            {
                MarkCharacter(player.transform);
                var pl = player.transform.Find("PlayerFillLight");
                if (pl != null) CharacterOnly(pl.GetComponent<Light>());
                n++;
            }
            foreach (var g in Object.FindObjectsByType<GuardAI>(FindObjectsInactive.Include))
            {
                MarkCharacter(g.transform);
                var t = g.transform.Find("CharacterFill");
                if (t == null)
                {
                    t = new GameObject("CharacterFill").transform;
                    t.SetParent(g.transform, false);
                }
                t.localPosition = new Vector3(0f, 2.5f, 0.8f);
                var l = t.GetComponent<Light>();
                if (l == null) l = t.gameObject.AddComponent<Light>();
                var hd = t.GetComponent<HDAdditionalLightData>();
                if (hd == null) { hd = t.gameObject.AddComponent<HDAdditionalLightData>(); HDAdditionalLightData.InitDefaultHDAdditionalLightData(hd); }
                l.type = LightType.Point;
                l.color = new Color(0.85f, 0.9f, 1f);
                l.range = 4.5f;
                l.lightUnit = LightUnit.Candela;
                l.intensity = GuardFillCandela;
                l.shadows = LightShadows.None;
                hd.volumetricDimmer = 0f;
                hd.affectsVolumetric = false;
                CharacterOnly(l);
                n++;
            }
            return n;
        }

        /// <summary>인물 모델(Model 아래 스킨 메시)만 인물 레이어에 넣는다. 머리 위 표시·라벨 등은 제외.</summary>
        static void MarkCharacter(Transform root)
        {
            var model = root.Find("Model");
            if (model == null) return;
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                r.renderingLayerMask |= 1u | CharacterLayerBit;
                EditorUtility.SetDirty(r);
            }
        }

        static void CharacterOnly(Light l)
        {
            if (l == null) return;
            var hd = l.GetComponent<HDAdditionalLightData>();
            l.renderingLayerMask = (int)CharacterLayerBit;
            if (hd != null)
            {
                hd.lightlayersMask = (UnityEngine.Rendering.HighDefinition.RenderingLayerMask)CharacterLayerBit;
                EditorUtility.SetDirty(hd);
            }
            EditorUtility.SetDirty(l);
        }

        static string PathOf(Transform t)
        {
            string p = t.name;
            for (var x = t.parent; x != null; x = x.parent) p = x.name + "/" + p;
            return p;
        }

        // ---------------------------------------------------------------- 볼륨(하늘·안개·노출·후처리)

        static T Comp<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (!profile.TryGet<T>(out var c))
            {
                c = profile.Add<T>(false);
                c.name = typeof(T).Name;
                AssetDatabase.AddObjectToAsset(c, profile);
            }
            c.active = true;
            return c;
        }

        public static VolumeProfile EnsureProfile()
        {
            EnsureFolder(Folder);
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            var env = Comp<VisualEnvironment>(profile);
            env.skyType.Override((int)SkyType.Gradient);
            env.skyAmbientMode.Override(SkyAmbientMode.Dynamic);

            // 도시 외곽 밤하늘: 천정은 거의 검은 남색, 지평선은 나트륨 가로등 빛공해로 약간 갈색
            var sky = Comp<GradientSky>(profile);
            sky.top.Override(new Color(0.006f, 0.009f, 0.020f));
            sky.middle.Override(new Color(0.040f, 0.034f, 0.034f));
            sky.bottom.Override(new Color(0.010f, 0.010f, 0.012f));
            sky.gradientDiffusion.Override(2.2f);
            sky.exposure.Override(0f);
            sky.multiplier.Override(1f);

            // 볼류메트릭 안개: 가로등·투광등 아래 빛 기둥, 먼 곳은 습한 밤공기로 흐려짐
            var fog = Comp<Fog>(profile);
            fog.enabled.Override(true);
            fog.meanFreePath.Override(90f);
            fog.baseHeight.Override(0f);
            fog.maximumHeight.Override(18f);
            fog.maxFogDistance.Override(160f);
            fog.enableVolumetricFog.Override(true);
            fog.albedo.Override(new Color(0.78f, 0.80f, 0.86f));
            fog.anisotropy.Override(0.55f);
            fog.depthExtent.Override(60f);
            fog.globalLightProbeDimmer.Override(0.6f);

            // 자동 노출: 사람 눈·카메라처럼 밝은 홀에서 어두운 야드로 나가면 천천히 적응. 하한을 둬서 밤이 낮처럼 밝아지지 않게.
            var exp = Comp<Exposure>(profile);
            exp.mode.Override(ExposureMode.AutomaticHistogram);
            exp.meteringMode.Override(MeteringMode.CenterWeighted);
            exp.limitMin.Override(-1.0f);
            exp.limitMax.Override(7.0f);
            exp.compensation.Override(0.4f);
            exp.adaptationMode.Override(AdaptationMode.Progressive);
            exp.adaptationSpeedDarkToLight.Override(2.2f);
            exp.adaptationSpeedLightToDark.Override(1.0f);
            exp.histogramPercentages.Override(new Vector2(40f, 90f));

            var tm = Comp<Tonemapping>(profile);
            tm.mode.Override(TonemappingMode.ACES);

            var bloom = Comp<Bloom>(profile);
            bloom.threshold.Override(0f);
            bloom.intensity.Override(0.22f);
            bloom.scatter.Override(0.65f);

            var ao = Comp<ScreenSpaceAmbientOcclusion>(profile);
            ao.intensity.Override(1.4f);
            ao.radius.Override(1.2f);
            ao.directLightingStrength.Override(0.2f);

            var ssr = Comp<ScreenSpaceReflection>(profile);
            ssr.enabled.Override(true);

            var gi = Comp<GlobalIllumination>(profile);
            gi.enable.Override(true); // 화면 공간 GI: 투광등 빛이 바닥·벽에서 튀어 그늘을 채움

            var cs = Comp<ContactShadows>(profile);
            cs.enable.Override(true);
            cs.length.Override(0.25f);
            cs.opacity.Override(1f);
            cs.maxDistance.Override(40f);

            var shadows = Comp<HDShadowSettings>(profile);
            shadows.maxShadowDistance.Override(70f);
            shadows.cascadeShadowSplitCount.Override(3);

            // 모션 블러: 빠르게 움직이는 팔다리·몸과 카메라 회전에만. 카메라 순간 이동(타이틀→게임, 체크포인트 복귀)에서
            // 화면 전체가 번지지 않게 카메라 이동 속도는 거의 0으로, 회전 속도는 약하게 제한.
            // → 2026-11-24 조정자 지시(게임 화면 판독성)로 끔. 예전 값 intensity 0.35.
            var mb = Comp<MotionBlur>(profile);
            mb.intensity.Override(0f);
            mb.active = true; // 주의: active=false로 두면 덮어쓰기가 빠져 HDRP 기본 볼륨의 모션 블러(켜짐)가 적용된다 → 켠 채로 세기 0

            var vig = Comp<Vignette>(profile);
            vig.intensity.Override(0.22f);
            vig.smoothness.Override(0.45f);

            var grain = Comp<FilmGrain>(profile);
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(0.12f);
            grain.response.Override(0.85f);

            var ca = Comp<ChromaticAberration>(profile);
            ca.intensity.Override(0.04f);

            var adj = Comp<ColorAdjustments>(profile);
            adj.contrast.Override(6f);
            adj.saturation.Override(-6f);

            EditorUtility.SetDirty(profile);
            return profile;
        }

        public static void SetupVolume()
        {
            var profile = EnsureProfile();
            var go = GameObject.Find("Rendering_Volume");
            if (go == null) go = new GameObject("Rendering_Volume");
            var v = go.GetComponent<Volume>();
            if (v == null) v = go.AddComponent<Volume>();
            v.isGlobal = true;
            v.priority = 0f;
            v.sharedProfile = profile;
        }

        // ---------------------------------------------------------------- 반사 프로브

        /// <summary>구역별 반사 프로브(야드·홀·사무 구역·서버실). 시작할 때 한 번 찍는 실시간(OnEnable)이라 굽기 단계가 필요 없다.
        /// 상자 높이는 바닥 가운데에서 위로 레이를 쏴 천장까지(맞는 게 없으면 기본값).</summary>
        public static int SetupReflectionProbes()
        {
            var old = GameObject.Find("ReflectionProbes");
            if (old != null) Object.DestroyImmediate(old);
            var root = new GameObject("ReflectionProbes");
            Physics.SyncTransforms();
            int n = 0;
            // 이름, 바닥 중심, 가로·세로(m), 천장이 없을 때 높이
            n += Probe(root.transform, "Yard", new Vector3(-28f, 0f, 0f), new Vector2(30f, 48f), 10f);
            n += Probe(root.transform, "Hall", new Vector3(0f, 0f, 0f), new Vector2(32f, 24f), 9f);
            n += Probe(root.transform, "Offices", new Vector3(-5f, 0f, 15.5f), new Vector2(21f, 8f), 3.2f);
            n += Probe(root.transform, "ServerRoom", new Vector3(10.5f, 0f, 16f), new Vector2(9f, 7.5f), 3.2f);
            return n;
        }

        static int Probe(Transform parent, string name, Vector3 floor, Vector2 size, float defaultHeight)
        {
            float h = defaultHeight;
            if (Physics.Raycast(floor + Vector3.up * 1.5f, Vector3.up, out var hit, 30f, ~0, QueryTriggerInteraction.Ignore)) h = Mathf.Max(2.5f, hit.point.y - floor.y);
            var go = new GameObject("ReflectionProbe_" + name);
            go.transform.SetParent(parent, false);
            go.transform.position = floor + Vector3.up * (h * 0.5f);
            go.AddComponent<ReflectionProbe>();
            var hd = go.GetComponent<HDAdditionalReflectionData>();
            if (hd == null) hd = go.AddComponent<HDAdditionalReflectionData>();
            hd.mode = ProbeSettings.Mode.Realtime;
            hd.realtimeMode = ProbeSettings.RealtimeMode.OnEnable;
            hd.influenceVolume.shape = InfluenceShape.Box;
            hd.influenceVolume.boxSize = new Vector3(size.x, h, size.y);
            hd.influenceVolume.boxBlendDistancePositive = Vector3.one;
            hd.influenceVolume.boxBlendDistanceNegative = Vector3.one;
            // 프록시 볼륨을 따로 두지 않으면 영향 상자가 시차 보정 상자로 쓰인다. 촬영점은 상자 중심.
            EditorUtility.SetDirty(hd);
            return 1;
        }

        public static void SetupCameras()
        {
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
            {
                if (cam.gameObject.scene != EditorSceneManager.GetActiveScene()) continue;
                var hd = cam.GetComponent<HDAdditionalCameraData>();
                if (hd == null) hd = cam.gameObject.AddComponent<HDAdditionalCameraData>();
                hd.antialiasing = HDAdditionalCameraData.AntialiasingMode.TemporalAntialiasing;
                hd.TAAQuality = HDAdditionalCameraData.TAAQualityLevel.High;
                hd.clearColorMode = HDAdditionalCameraData.ClearColorMode.Sky;
                hd.dithering = true;
                hd.stopNaNs = true;
                cam.allowHDR = true;
                cam.allowMSAA = false;
                EditorUtility.SetDirty(cam);
                EditorUtility.SetDirty(hd);
            }
        }

        static void DisableLegacyFog()
        {
            RenderSettings.fog = false; // HDRP는 볼륨 Fog를 쓴다
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Skybox;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
