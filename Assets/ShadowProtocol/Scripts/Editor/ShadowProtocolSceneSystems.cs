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
            var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(pc != null ? ShadowProtocolAnimBuilder.PlayerControllerPath : ShadowProtocolAnimBuilder.GuardControllerPath);
            if (ctrl == null) return;
            animr.runtimeAnimatorController = ctrl;
            animr.applyRootMotion = false;
            animr.cullingMode = pc != null ? AnimatorCullingMode.AlwaysAnimate : AnimatorCullingMode.CullUpdateTransforms;
            var drv = inst.AddComponent<CharacterAnimDriver>();
            drv.mover = owner;
            drv.player = pc;
            drv.runNatural = pc != null ? ShadowProtocolAnimBuilder.PlayerRunNatural : ShadowProtocolAnimBuilder.GuardRunNatural;
            drv.crouchWalkNatural = ShadowProtocolAnimBuilder.PlayerCrouchWalkNatural;
            // 던지기: 원본 2.2s에 손이 나간다 → (2.2 - 시작 오프셋) / 재생 속도
            float clipLen = 3.4f;
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
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
                    statics++;
                }
            }

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

            // 조명: 홀 작업등 8개 중 6개만 픽셀 조명, 사무동 벽등은 Auto(가장 가까운 것만 픽셀)
            int pixel = 0;
            var work = GameObject.Find("Environment/WorkLights");
            if (work != null)
            {
                int i = 0;
                foreach (var l in work.GetComponentsInChildren<Light>())
                {
                    l.renderMode = (i == 0 || i == 3) ? LightRenderMode.Auto : LightRenderMode.ForcePixel;
                    i++;
                }
            }
            var annex = GameObject.Find("Environment/AnnexLights");
            if (annex != null) foreach (var l in annex.GetComponentsInChildren<Light>()) l.renderMode = LightRenderMode.Auto;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.renderMode == LightRenderMode.ForcePixel) pixel++;
            Debug.Log($"[ShadowProtocol] 성능 설정: 정적 오브젝트 {statics}, LOD 컬링 {lods}, ForcePixel 조명 {pixel}");
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
                imp.generateMeshLods = true;
                imp.SaveAndReimport();
                changed++;
            }
            if (changed > 0) Debug.Log($"[ShadowProtocol] Mesh LOD 생성 켬: {changed}개 모델");
        }
    }
}
