using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadowProtocol.EditorTools
{
    /// <summary>
    /// 씬 빌드 2단계: 사운드·UI·도구·적응형 지휘·체크포인트·튜토리얼·카메라 반투명·성능 설정.
    /// </summary>
    public static partial class ShadowProtocolSceneBuilder
    {
        const string FadeFolder = MatFolder + "/Fade";

        static AudioClip Clip(string n) => AssetDatabase.LoadAssetAtPath<AudioClip>(ShadowProtocolAudioBuilder.Folder + "/" + n + ".wav");

        static AudioClip[] Clips(string prefix)
        {
            var list = new List<AudioClip>();
            for (int i = 0; i < 8; i++)
            {
                var c = Clip(prefix + i);
                if (c != null) list.Add(c);
            }
            return list.ToArray();
        }

        /// <summary>모델에 Animator Controller와 파라미터 드라이버를 붙인다(Humanoid 리타겟)</summary>
        static void SetupAnimator(GameObject inst, Transform owner, PlayerController pc)
        {
            var animr = inst.GetComponent<Animator>();
            if (animr == null) return;
            // 인물별 컨트롤러(같은 동작이라도 인물마다 리타깃한 클립·걷기/달리기 속도가 다르다)
            string src = AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(inst));
            string who = pc != null ? ShadowProtocolAnimBuilder.Player : src.Contains(ShadowProtocolAnimBuilder.Captain) ? ShadowProtocolAnimBuilder.Captain : ShadowProtocolAnimBuilder.Heavy;
            var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ShadowProtocolAnimBuilder.ControllerPath(who));
            if (ctrl == null) return;
            animr.runtimeAnimatorController = ctrl;
            animr.applyRootMotion = false;
            animr.cullingMode = pc != null ? AnimatorCullingMode.AlwaysAnimate : AnimatorCullingMode.CullUpdateTransforms;
            var drv = inst.AddComponent<CharacterAnimDriver>();
            drv.mover = owner;
            drv.player = pc;
            drv.runNatural = ShadowProtocolAnimBuilder.RunNatural(who);
            drv.crouchWalkNatural = ShadowProtocolAnimBuilder.PlayerCrouchWalkNatural;
            // 던지기: 원본 2.2s에 손이 나간다 → (2.2 - 시작 오프셋) / 재생 속도
            var thrClip = ShadowProtocolAnimBuilder.Clip(ShadowProtocolAnimBuilder.Player, "Throw");
            float clipLen = thrClip != null ? thrClip.length : 3.4f;
            drv.throwReleaseDelay = Mathf.Max(0.1f, (2.2f - ShadowProtocolAnimBuilder.ThrowStartNormalized * clipLen) / ShadowProtocolAnimBuilder.ThrowSpeed);
            drv.throwDuration = (0.9f - ShadowProtocolAnimBuilder.ThrowStartNormalized) * clipLen / ShadowProtocolAnimBuilder.ThrowSpeed + 0.2f;
        }

        static Transform RoutePoint(Transform parent, string name, Vector3 pos, float yaw)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.position = pos;
            t.rotation = Quaternion.Euler(0f, yaw, 0f);
            return t;
        }

        static GuardAI GuardNamed(string n)
        {
            var go = GameObject.Find("Guards/" + n);
            return go != null ? go.GetComponent<GuardAI>() : null;
        }

        static void BuildSystems(PlayerController pc, ThirdPersonCamera tpc, GameManager gm, Material ringMat)
        {
            // ---------- 사운드 ----------
            var amGO = new GameObject("AudioManager");
            var am = amGO.AddComponent<AudioManager>();
            am.musicAmbient = Clip("music_ambient"); am.musicTension = Clip("music_tension"); am.musicAlert = Clip("music_alert"); am.siren = Clip("siren_loop");
            am.stepConcrete = Clips("step_concrete_"); am.stepMetal = Clips("step_metal_"); am.stepAsphalt = Clips("step_asphalt_");
            am.guardSuspicious = Clip("guard_suspicious"); am.guardAlert = Clip("guard_alert"); am.radioBeep = Clip("radio_beep");
            am.dataJingle = Clip("jingle_data"); am.winSting = Clip("sting_win"); am.failSting = Clip("sting_fail");
            am.bottleBreak = Clip("bottle_break"); am.canLand = Clip("can_land"); am.knockout = Clip("knockout"); am.uiClick = Clip("ui_click");
            am.pickup = Clip("pickup"); am.throwWhoosh = Clip("throw_whoosh"); am.checkpoint = Clip("checkpoint");
            pc.gameObject.AddComponent<FootstepAudio>().player = pc;

            var callSigns = new Dictionary<string, string>
            {
                { "Guard_Yard", "야드 경비" }, { "Guard_West", "홀 서쪽 경비" }, { "Guard_Center", "홀 감독관" },
                { "Guard_Vault", "홀 동쪽 경비" }, { "Guard_Door", "서버실 정문 경비" }, { "Guard_Office", "사무동 감독관" }
            };
            foreach (var g in Object.FindObjectsByType<GuardAI>(FindObjectsSortMode.None))
            {
                if (callSigns.TryGetValue(g.name, out string cs)) g.callSign = cs;
                g.gameObject.AddComponent<FootstepAudio>().guard = g;
                g.gameObject.AddComponent<GuardAudio>();
            }

            // ---------- 플레이어 도구 ----------
            var tools = pc.gameObject.AddComponent<PlayerTools>();
            tools.player = pc;
            tools.cam = tpc;
            tools.bottleMat = Mat("Bottle", new Color(0.15f, 0.45f, 0.25f));
            tools.canMat = Mat("Can", new Color(0.7f, 0.72f, 0.75f));
            var arcMat = UnlitTransparentMat("ThrowArc");
            var arcGO = new GameObject("ThrowArc");
            arcGO.transform.SetParent(pc.transform, false);
            var arc = arcGO.AddComponent<LineRenderer>();
            arc.sharedMaterial = arcMat;
            arc.useWorldSpace = true;
            arc.widthMultiplier = 0.045f;
            arc.startColor = new Color(1f, 0.95f, 0.5f, 0.9f);
            arc.endColor = new Color(1f, 0.6f, 0.2f, 0.6f);
            arc.shadowCastingMode = ShadowCastingMode.Off;
            arc.receiveShadows = false;
            arc.enabled = false;
            tools.arc = arc;
            var ringGO = new GameObject("ThrowLanding");
            var ring = ringGO.AddComponent<LineRenderer>();
            ring.sharedMaterial = arcMat;
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.widthMultiplier = 0.05f;
            ring.startColor = ring.endColor = new Color(1f, 0.75f, 0.3f, 0.85f);
            ring.shadowCastingMode = ShadowCastingMode.Off;
            ring.receiveShadows = false;
            const int seg = 32;
            ring.positionCount = seg;
            for (int i = 0; i < seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                ring.SetPosition(i, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)));
            }
            ring.enabled = false;
            tools.landingRing = ring;
            tpc.tools = tools;
            gm.tools = tools;

            // 바닥의 병/캔
            var pickups = new GameObject("Throwables").transform;
            var pickupDefs = new (Vector3 p, ThrowKind k)[]
            {
                (new Vector3(-33.6f, 0f, -12.6f), ThrowKind.Can), (new Vector3(-22.4f, 0f, -2.2f), ThrowKind.Bottle),
                (new Vector3(-13.2f, 0f, 6.6f), ThrowKind.Bottle), (new Vector3(-9f, 0f, 13.2f), ThrowKind.Can),
                (new Vector3(2.6f, 0f, 16.6f), ThrowKind.Bottle), (new Vector3(-29f, 0f, 14.6f), ThrowKind.Can)
            };
            foreach (var d in pickupDefs)
            {
                var go = new GameObject("Pickup_" + d.k);
                go.transform.SetParent(pickups, false);
                go.transform.position = d.p;
                var vis = Primitive(PrimitiveType.Cylinder, go.transform, "Visual", new Vector3(0f, 0.1f, 0f),
                    d.k == ThrowKind.Bottle ? new Vector3(0.09f, 0.13f, 0.09f) : new Vector3(0.08f, 0.06f, 0.08f), d.k == ThrowKind.Bottle ? tools.bottleMat : tools.canMat);
                vis.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                var glow = Primitive(PrimitiveType.Cylinder, go.transform, "Glint", new Vector3(0f, 0.005f, 0f), new Vector3(0.4f, 0.003f, 0.4f), Mat("PickupGlint", new Color(0.8f, 0.65f, 0.3f), 0.45f));
                glow.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                var pk = go.AddComponent<ThrowablePickup>();
                pk.kind = d.k;
                pk.visual = vis.transform;
            }

            // ---------- 적응형 지휘 ----------
            var dirGO = new GameObject("GuardDirector");
            var dir = dirGO.AddComponent<GuardDirector>();
            dir.player = pc;
            var markers = new List<RouteMarker>();
            System.Action<string, string, Vector3, Vector3> marker = (id, label, c, s) =>
            {
                var t = new GameObject("Route_" + id).transform;
                t.SetParent(dirGO.transform, false);
                t.position = c;
                var rm = t.gameObject.AddComponent<RouteMarker>();
                rm.routeId = id; rm.label = label; rm.size = s;
                markers.Add(rm);
            };
            marker("Vent", "환기구", new Vector3(-15.5f, 0.6f, -7.9f), new Vector3(3f, 1.4f, 1.4f));
            marker("Front", "정면 하역문", new Vector3(-15.5f, 1.5f, -1f), new Vector3(2.6f, 3f, 4.2f));
            marker("Office", "사무동 출입문", new Vector3(-15.5f, 1.2f, 12.9f), new Vector3(2.6f, 2.4f, 1.9f));
            marker("Catwalk", "캣워크", new Vector3(0.5f, DeckY + 0.8f, -10.1f), new Vector3(22f, 1.6f, 1.9f));
            marker("Duct", "덕트", new Vector3(5.5f, 0.6f, 18.6f), new Vector3(2.6f, 1.4f, 1.3f));
            dir.routes = markers.ToArray();

            var covers = new List<Transform>();
            var coverRoot = GameObject.Find("Level/Cover");
            if (coverRoot != null) foreach (Transform t in coverRoot.transform) covers.Add(t);
            var props = GameObject.Find("Level/Props2");
            if (props != null)
                foreach (Transform t in props.transform)
                    if (t.name.StartsWith("Container") || t.name.StartsWith("Forklift") || t.name.StartsWith("Pallets") || t.name.StartsWith("Drums") || t.name.StartsWith("Tarp") || t.name.StartsWith("Lockers") || t.name.StartsWith("Server"))
                        covers.Add(t);
            dir.covers = covers.ToArray();

            var aroot = new GameObject("AdaptiveRoutes").transform;
            aroot.SetParent(dirGO.transform, false);
            var ad = new List<GuardDirector.Adaptation>();
            ad.Add(new GuardDirector.Adaptation
            {
                routeId = "Catwalk", routeLabel = "캣워크", guard = GuardNamed("Guard_Vault"), post = false, scanAngle = 45f, action = "순찰하기 시작했다",
                route = new[] { RoutePoint(aroot, "Catwalk_P0", new Vector3(4f, DeckY, -10.1f), 90f), RoutePoint(aroot, "Catwalk_P1", new Vector3(12.5f, DeckY, -10.1f), 270f) }
            });
            ad.Add(new GuardDirector.Adaptation
            {
                routeId = "Vent", routeLabel = "환기구", guard = GuardNamed("Guard_West"), post = true, scanAngle = 40f,
                route = new[] { RoutePoint(aroot, "Vent_Post", new Vector3(-11.2f, 0f, -8.2f), 270f) }
            });
            ad.Add(new GuardDirector.Adaptation
            {
                routeId = "Duct", routeLabel = "덕트", guard = GuardNamed("Guard_Door"), post = true, scanAngle = 35f,
                route = new[] { RoutePoint(aroot, "Duct_Post", new Vector3(7.8f, 0f, 17.2f), 300f) }
            });
            ad.Add(new GuardDirector.Adaptation
            {
                routeId = "Office", routeLabel = "사무동 출입문", guard = GuardNamed("Guard_Yard"), post = true, scanAngle = 50f,
                route = new[] { RoutePoint(aroot, "Office_Post", new Vector3(-18.6f, 0f, 15.4f), 225f) }
            });
            ad.Add(new GuardDirector.Adaptation
            {
                routeId = "Front", routeLabel = "정면 하역문", guard = GuardNamed("Guard_Center"), post = false, scanAngle = 45f, action = "순찰 경로에 넣었다",
                route = new[]
                {
                    RoutePoint(aroot, "Front_P0", new Vector3(3f, 0f, 4f), 0f), RoutePoint(aroot, "Front_P1", new Vector3(-11.5f, 0f, 0.4f), 270f),
                    RoutePoint(aroot, "Front_P2", new Vector3(3f, 0f, -9.5f), 0f), RoutePoint(aroot, "Front_P3", new Vector3(9.5f, 0f, -9.5f), 0f)
                }
            });
            dir.adaptations = ad.ToArray();

            // ---------- 체크포인트 ----------
            var cpRoot = new GameObject("Checkpoints").transform;
            System.Action<int, string, Vector3, Vector3> cp = (i, label, c, s) =>
            {
                var t = new GameObject("Checkpoint_" + i).transform;
                t.SetParent(cpRoot, false);
                t.position = c;
                var z = t.gameObject.AddComponent<CheckpointZone>();
                z.index = i; z.label = label; z.size = s; z.player = pc;
            };
            cp(1, "메인 홀", new Vector3(-9f, 1.5f, -2f), new Vector3(12f, 3f, 18f));
            cp(2, "사무동", new Vector3(-5f, 1.5f, 16f), new Vector3(20f, 3f, 7f));
            cp(3, "서버실", new Vector3(8f, 1.5f, 15f), new Vector3(5f, 3f, 7f));

            // ---------- 튜토리얼·UI ----------
            var tut = new GameObject("TutorialDirector").AddComponent<TutorialDirector>();
            tut.player = pc;
            tut.tools = tools;
            var ui = new GameObject("GameUI").AddComponent<GameUI>();
            ui.player = pc;
            ui.tools = tools;
            ui.goalPosition = GoalPos;
            ui.extractPosition = StartPos;
            ui.gallery = BuildMakingOf();

            // ---------- 카메라 반투명 소품 ----------
            EnsureFolder(FadeFolder);
            int fades = 0;
            if (props != null)
                foreach (Transform t in props.transform)
                    if (t.name.StartsWith("Container") || t.name.StartsWith("Forklift") || t.name.StartsWith("Lockers") || t.name.StartsWith("Server"))
                        fades += AddFadeable(t.gameObject, true) ? 1 : 0;
            if (coverRoot != null)
                foreach (Transform t in coverRoot.transform)
                    if (t.name.StartsWith("Shelf")) fades += AddFadeable(t.gameObject, true) ? 1 : 0;
            var playerModel = pc.transform.Find("Model");
            if (playerModel != null) AddFadeable(playerModel.gameObject, false);
            Debug.Log($"[ShadowProtocol] 카메라 반투명 소품 {fades}개");

            BuildPuzzles(pc, dir);
            ApplyEnvironmentModules(); // VARCO 환경 모듈이 있으면 벽·바닥·펜스 등의 겉모습 교체(없으면 기본 도형 유지)
            ApplyPerformanceSettings();
        }

        /// <summary>Standard 재질의 Fade 모드 복사본(같은 텍스처)을 만들어 CameraFadeable에 등록</summary>
        static bool AddFadeable(GameObject go, bool passThrough)
        {
            var slots = new List<CameraFadeable.Slot>();
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || r is LineRenderer) continue;
                var mats = r.sharedMaterials;
                var fade = new Material[mats.Length];
                bool ok = true;
                for (int i = 0; i < mats.Length; i++)
                {
                    fade[i] = FadeCopy(mats[i]);
                    if (fade[i] == null) ok = false;
                }
                if (!ok) continue;
                slots.Add(new CameraFadeable.Slot { renderer = r, opaque = mats, fade = fade });
            }
            if (slots.Count == 0) return false;
            var f = go.AddComponent<CameraFadeable>();
            f.slots = slots.ToArray();
            f.cameraPassThrough = passThrough;
            if (!passThrough) f.fadedAlpha = 0.35f;
            return true;
        }

        static Material FadeCopy(Material src)
        {
            if (src == null || src.shader == null || !src.shader.name.StartsWith("Standard")) return null;
            string path = $"{FadeFolder}/{src.name}_Fade.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(src);
                AssetDatabase.CreateAsset(m, path);
            }
            else m.CopyPropertiesFromMaterial(src);
            m.shader = src.shader;
            m.SetFloat("_Mode", 2f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>성능: 정적 배칭 표시, 소품·캐릭터 LOD(원거리 컬링), 픽셀 조명 정리</summary>
        static void ApplyPerformanceSettings()
        {
            int statics = 0;
            foreach (var rootName in new[] { "Level", "Environment", "Decor", "DecorProps", "SecurityCameras" })
            {
                var root = GameObject.Find(rootName);
                if (root == null) continue;
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.GetComponentInParent<CameraFadeable>() != null) continue;
                    if (t.GetComponent<Light>() != null) continue;
                    // 펜스 모듈(같은 메시 반복)은 정적 배칭 대신 GPU 인스턴싱으로 묶는다
                    bool instanced = IsUnder(t, "FenceModules");
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject, instanced ? StaticEditorFlags.OccludeeStatic : StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
                    statics++;
                }
            }

            // 벽: 가리는 물체(Occluder)로 표시 → Build()에서 StaticOcclusionCulling.Compute()로 굽는다
            int occluders = 0;
            var walls = GameObject.Find("Level/Walls");
            if (walls != null)
                foreach (var r in walls.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (!r.enabled || r.GetComponentInParent<CameraFadeable>() != null) continue;
                    var go = r.gameObject;
                    GameObjectUtility.SetStaticEditorFlags(go, GameObjectUtility.GetStaticEditorFlags(go) | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.BatchingStatic);
                    occluders++;
                }

            // 그림자: 계단식 4→2, 거리 150→45m(창고 안은 대부분 30m 이내, 그림자 내는 건 달빛 하나뿐)
            QualitySettings.shadowCascades = 2;
            QualitySettings.shadowDistance = 45f;
            QualitySettings.shadowCascade2Split = 0.25f;
            // 물체마다 받는 픽셀 조명 4→2(나머지는 정점 조명). 홀은 강제 픽셀 작업등 3개, 야드·사무·서버실은 화면 비교에서 차이 거의 없음
            QualitySettings.pixelLightCount = 2;

            // 소품: 화면에서 1.2% 미만이면 그리지 않는다(멀리 있는 작은 소품)
            int lods = 0;
            foreach (var rootName in new[] { "Level/Props2", "DecorProps", "SecurityCameras" })
            {
                var root = GameObject.Find(rootName);
                if (root == null) continue;
                foreach (Transform t in root.transform) { AddCullLod(t.gameObject, t.name.StartsWith("Container") ? 0.006f : 0.012f); lods++; }
            }
            foreach (var g in Object.FindObjectsByType<GuardAI>(FindObjectsSortMode.None))
            {
                var model = g.transform.Find("Model");
                if (model != null) { AddCullLod(model.gameObject, 0.008f); lods++; }
            }

            // 조명: 홀 작업등 8개 중 3개만 강제 픽셀 조명(엇갈리게 배치), 나머지·사무동 벽등은 Auto(가까운 것만 픽셀)
            int pixel = 0;
            var work = GameObject.Find("Environment/WorkLights");
            if (work != null)
            {
                var ls = work.GetComponentsInChildren<Light>();
                // 엇갈리게 고른 3개. 기본 도형 8개(x -10,-2,5,12 × 남·북): 남 -2 / 북 -10 / 북 5
                // 트러스 모듈 10개(x -12,-6,0,6,12 × 남·북): 남 -6 / 남 6 / 북 0
                var pick = ls.Length == 10 ? new[] { 1, 3, 7 } : new[] { 1, 4, 6 };
                for (int i = 0; i < ls.Length; i++)
                    ls[i].renderMode = System.Array.IndexOf(pick, i) >= 0 ? LightRenderMode.ForcePixel : LightRenderMode.Auto;
            }
            var annex = GameObject.Find("Environment/AnnexLights");
            if (annex != null) foreach (var l in annex.GetComponentsInChildren<Light>()) l.renderMode = LightRenderMode.Auto;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.renderMode == LightRenderMode.ForcePixel) pixel++;

            // 바닥에 붙은 납작한 장식(높이 5cm 미만: 안전선·주차선·배수구 덮개·시작 패드 등)은 그림자를 내지 않는다(그림자 캐스터 패스 절약)
            int flat = 0;
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                if (r.enabled && r.shadowCastingMode == ShadowCastingMode.On && r.bounds.size.y < 0.05f) { r.shadowCastingMode = ShadowCastingMode.Off; flat++; }

            // 작은 장식(주차선·배수구, 장식용 보안 카메라, 장식 소품)은 SmallDetail 층에 두고 카메라에서 30m 넘으면 그리지 않는다
            // (Camera.layerCullDistances는 저장되지 않아 ThirdPersonCamera.Start에서 설정)
            int small = 0;
            int layer = EnsureLayer(SmallDetailLayer);
            if (layer >= 0)
                foreach (var rootName in new[] { "Environment/ParkingLines", "SecurityCameras", "DecorProps" })
                {
                    var root = GameObject.Find(rootName);
                    if (root == null) continue;
                    foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    {
                        t.gameObject.layer = layer;
                        if (t.GetComponent<Renderer>() != null) small++;
                    }
                }
            Debug.Log($"[ShadowProtocol] 성능 설정: 정적 오브젝트 {statics}, 가림 벽 {occluders}, LOD 컬링 {lods}, ForcePixel 조명 {pixel}, 픽셀 조명 수 {QualitySettings.pixelLightCount}, 그림자 {QualitySettings.shadowCascades}단 {QualitySettings.shadowDistance}m, 그림자 끈 납작 장식 {flat}, 30m 컬링 작은 장식 {small}");
        }

        public const string SmallDetailLayer = ThirdPersonCamera.SmallDetailLayer;

        /// <summary>프로젝트 층(TagManager)에 이름이 없으면 빈 사용자 층(8~31)에 만든다. 층 번호를 돌려준다.</summary>
        static int EnsureLayer(string name)
        {
            int existing = LayerMask.NameToLayer(name);
            if (existing >= 0) return existing;
            var tm = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tm.FindProperty("layers");
            for (int i = 8; i < layers.arraySize; i++)
            {
                var p = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(p.stringValue)) { p.stringValue = name; tm.ApplyModifiedProperties(); return i; }
            }
            Debug.LogWarning("[ShadowProtocol] 빈 층이 없어 " + name + " 층을 만들지 못함");
            return -1;
        }

        static bool IsUnder(Transform t, string ancestorName)
        {
            for (var p = t; p != null; p = p.parent) if (p.name == ancestorName) return true;
            return false;
        }

        static void AddCullLod(GameObject go, float cullHeight)
        {
            if (go.GetComponentInChildren<LODGroup>() != null) return; // FBX에 LOD0/1/2가 있으면(컨테이너 v2) 그 LODGroup을 쓴다
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return;
            var lg = go.AddComponent<LODGroup>();
            lg.SetLODs(new[] { new LOD(cullHeight, rs) });
            lg.RecalculateBounds();
        }

        /// <summary>VARCO 소품·캐릭터 FBX에 Mesh LOD(원거리 단순화 메시) 생성을 켠다</summary>
        public static void EnsureMeshLods()
        {
            var paths = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/ShadowProtocol/Models" })) paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            int changed = 0;
            foreach (var p in paths)
            {
                var imp = AssetImporter.GetAtPath(p) as ModelImporter;
                if (imp == null || imp.generateMeshLods) continue;
                if (p.EndsWith("SP2_container_v2.fbx")) continue; // Blender에서 만든 LOD0/1/2(LODGroup)를 쓴다
                if (p.Contains("/Models/EnvFinal/") || p.Contains("/Models/ContainerFinal/") || p.Contains("/Models/CharactersFinal/")) continue; // Blender 최종본은 원래 메시(컨테이너는 자체 LOD0/1/2) 그대로
                imp.generateMeshLods = true;
                imp.SaveAndReimport();
                changed++;
            }
            if (changed > 0) Debug.Log($"[ShadowProtocol] Mesh LOD 생성 켬: {changed}개 모델");
        }
    }
}
