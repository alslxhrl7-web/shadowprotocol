using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace ShadowProtocol.EditorTools
{
    /// <summary>
    /// 메뉴 ShadowProtocol/Build Demo Scene 으로 데모 씬 전체를 다시 만든다.
    /// </summary>
    public static partial class ShadowProtocolSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/ShadowProtocol_Demo.unity";
        const string MatFolder = "Assets/ShadowProtocol/Materials";
        const string NavFolder = "Assets/Scenes/ShadowProtocol_Demo";
        const string NavAssetPath = NavFolder + "/NavMesh-Level.asset";
        const string TexFolder = "Assets/ShadowProtocol/Textures";
        static readonly Color NightColor = new Color(0.04f, 0.05f, 0.09f);
        // 인물: Blender 최종본(2026-11-16). 플레이어 = 여성 잠입 요원, 경비 = 중무장 경비병(기본) / 경비 반장("super" 지정 초소)
        public const string PlayerModelPath = "Assets/ShadowProtocol/Models/CharactersFinal/female_agent_final.fbx";
        public const string GuardModelPath = "Assets/ShadowProtocol/Models/CharactersFinal/heavyguard_v2_final.fbx";
        public const string CaptainModelPath = "Assets/ShadowProtocol/Models/CharactersFinal/supervisor_v2_final.fbx";

        [MenuItem("ShadowProtocol/Build Demo Scene")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[ShadowProtocol] 플레이 모드에서는 씬을 만들 수 없습니다.");
                return;
            }

            EnsureFolder("Assets/Scenes");
            EnsureFolder("Assets/ShadowProtocol");
            EnsureFolder(MatFolder);
            EnsureFolder(NavFolder);

            // 절차적 사운드(없는 것만 생성)·VARCO 애니메이션 컨트롤러·Mesh LOD
            ShadowProtocolAudioBuilder.EnsureAudio();
            if (ShadowProtocolAnimBuilder.SourcesPresent()) { ShadowProtocolAnimBuilder.ConfigureImporters(); ShadowProtocolAnimBuilder.BuildControllers(); }
            EnsureMeshLods();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ---------- Materials ----------
            EnsureFolder(TexFolder);
            var tConcrete = EnsureTex("Concrete", 512, ConcreteAlbedo, null);
            var tConcreteN = EnsureTex("Concrete_N", 512, null, ConcreteHeight, 2.5f);
            var tWall = EnsureTex("WallSteel", 512, WallAlbedo, null);
            var tWallN = EnsureTex("WallSteel_N", 512, null, WallHeight, 3f);
            var tAsphalt = EnsureTex("Asphalt", 512, AsphaltAlbedo, null);
            var tAsphaltN = EnsureTex("Asphalt_N", 512, null, AsphaltHeight, 1.5f);
            var mFloor = MatTex("Floor", new Color(0.95f, 0.95f, 0.95f), tConcrete, tConcreteN, new Vector2(8f, 6f));
            var mWall = MatTex("Wall", new Color(0.95f, 0.95f, 0.95f), tWall, tWallN, new Vector2(8f, 1f));
            var mAsphalt = MatTex("Asphalt", Color.white, tAsphalt, tAsphaltN, new Vector2(30f, 30f));
            var mLotLine = Mat("LotLine", new Color(0.75f, 0.75f, 0.7f));
            var mDrain = Mat("Drain", new Color(0.06f, 0.06f, 0.07f));
            var mFence = Mat("FenceMetal", new Color(0.38f, 0.4f, 0.42f));
            var mLampHead = Mat("LampHead", new Color(1f, 0.78f, 0.5f), 2.5f);
            var mWorkLight = Mat("WorkLight", new Color(0.85f, 0.8f, 0.7f), 1.1f);
            var mFarBuilding = Mat("FarBuilding", new Color(0.1f, 0.11f, 0.14f));
            var mShelf = Mat("Shelf", new Color(0.55f, 0.36f, 0.2f));
            var mCrate = Mat("Crate", new Color(0.72f, 0.54f, 0.3f));
            var mLowCrate = Mat("LowCrate", new Color(0.3f, 0.45f, 0.62f));
            var mStripe = Mat("FloorStripe", new Color(0.85f, 0.7f, 0.15f));
            var mPlayer = Mat("Player", new Color(0.15f, 0.55f, 0.95f));
            var mPlayerVisor = Mat("PlayerVisor", new Color(0.6f, 0.95f, 1f), 1.2f);
            var mGuard = Mat("Guard", new Color(0.5f, 0.12f, 0.14f));
            var mGuardVisor = Mat("GuardVisor", new Color(1f, 0.85f, 0.3f), 1.5f);
            var mOrb = Mat("StatusOrb", new Color(0.35f, 0.95f, 0.45f), 1.5f);
            var mGoal = Mat("Goal", new Color(0.2f, 1f, 0.55f), 3f);
            var mStart = Mat("StartPad", new Color(0.06f, 0.16f, 0.30f), 0.8f); // HDRP: 바닥 도색처럼 어두운 파랑(밝은 판이 밤 화면에서 튀었음)
            var mCone = UnlitTransparentMat("VisionCone");
            var mRing = UnlitTransparentMat("NoiseRing");
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // ---------- Lighting ----------
            var lightGO = new GameObject("Directional Light");
            var sun = lightGO.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 0.45f;
            sun.color = new Color(0.62f, 0.72f, 1f);
            sun.shadows = LightShadows.Soft;
            lightGO.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.2f, 0.24f, 0.34f);
            RenderSettings.ambientEquatorColor = new Color(0.13f, 0.14f, 0.19f);
            RenderSettings.ambientGroundColor = new Color(0.06f, 0.06f, 0.08f);
            // 밤 안개(어두운 남색). 플레이 영역(카메라에서 ~20m)은 맑게, 먼 야외만 흐리게.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = NightColor;
            RenderSettings.fogStartDistance = 28f;
            RenderSettings.fogEndDistance = 70f;

            // ---------- Level (NavMesh source): 확장 맵(야드·메인 홀·캣워크·사무 구역·서버실) ----------
            var level = new GameObject("Level");
            var surface = level.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            var lt = level.transform;
            var layoutMats = new LayoutMats
            {
                floor = mFloor, wall = mWall, asphalt = mAsphalt, shelf = mShelf, crate = mCrate, lowCrate = mLowCrate,
                stripe = mStripe, start = mStart, lotLine = mLotLine, drain = mDrain, fence = mFence, lampHead = mLampHead,
                workLight = mWorkLight, farBuilding = mFarBuilding,
                beam = Mat("LaserBeam", new Color(1f, 0.1f, 0.08f), 3f),
                cap = Mat("PartitionCap", new Color(0.32f, 0.33f, 0.35f)),
                extract = Mat("ExtractMarker", new Color(0.25f, 1f, 0.5f), 2f)
            };
            BuildCompound(lt, layoutMats);
            Vector3 startPos = StartPos;
            Vector3 goalPos = GoalPos;

            // ---------- Goal ----------
            var goalGO = new GameObject("Goal_DataCore");
            goalGO.transform.position = goalPos;
            var goalTrigger = goalGO.AddComponent<BoxCollider>();
            goalTrigger.isTrigger = true;
            goalTrigger.center = new Vector3(0f, 1f, 0f);
            goalTrigger.size = new Vector3(1.6f, 2f, 1.6f);
            var goalVisual = Primitive(PrimitiveType.Cube, goalGO.transform, "Core", new Vector3(0f, 0.9f, 0f), Vector3.one * 0.8f, mGoal);
            goalVisual.transform.rotation = Quaternion.Euler(45f, 0f, 45f);
            var goalBase = Primitive(PrimitiveType.Cylinder, goalGO.transform, "Pedestal", new Vector3(0f, 0.1f, 0f), new Vector3(1.4f, 0.1f, 1.4f), mWall);
            goalBase.name = "Pedestal";
            var glowGO = new GameObject("Glow");
            glowGO.transform.SetParent(goalGO.transform, false);
            glowGO.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            var glow = glowGO.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(0.3f, 1f, 0.6f);
            glow.range = 6f;
            glow.intensity = 2.5f;
            var goal = goalGO.AddComponent<GoalZone>();
            goal.visual = goalVisual.transform;
            goal.glow = glow;
            var dataCore = LoadProp("Prop_DataCore");
            if (dataCore != null)
            {
                // 단말기는 받침대 자리에, 회전·발광 큐브는 작은 홀로그램으로 그 위에 띄운다.
                var term = SpawnProp(dataCore, goalGO.transform, Vector3.zero, Quaternion.Euler(0f, 225f, 0f), Vector3.one); // 정면이 남서쪽(접근 방향)
                term.name = "Terminal";
                goalBase.GetComponent<Renderer>().enabled = false;
                goalVisual.transform.localPosition = new Vector3(0f, 2.05f, 0f);
                goalVisual.transform.localScale = Vector3.one * 0.35f;
                glowGO.transform.localPosition = new Vector3(0f, 1.7f, 0f);
            }

            // ---------- Player ----------
            var playerGO = new GameObject("Player");
            playerGO.transform.position = startPos + Vector3.up * 0.05f;
            // 북동쪽(기지 방향)을 보고 시작: 카메라가 펜스 레일 뒤에 오지 않는다
            playerGO.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
            var cc = playerGO.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.stepOffset = 0.3f;
            cc.skinWidth = 0.04f;
            var pc = playerGO.AddComponent<PlayerController>();
            var pVisual = new GameObject("Visual").transform;
            pVisual.SetParent(playerGO.transform, false);
            Primitive(PrimitiveType.Capsule, pVisual, "Body", new Vector3(0f, 0.9f, 0f), new Vector3(0.7f, 0.9f, 0.7f), mPlayer);
            Primitive(PrimitiveType.Cube, pVisual, "Visor", new Vector3(0f, 1.5f, 0.3f), new Vector3(0.45f, 0.14f, 0.2f), mPlayerVisor);
            pc.visual = pVisual;
            AttachModel(playerGO.transform, PlayerModelPath, pVisual.GetComponentsInChildren<Renderer>(), 1.705f, pc);

            var ringGO = new GameObject("NoiseRing");
            ringGO.transform.SetParent(playerGO.transform, false);
            ringGO.transform.localPosition = new Vector3(0f, 0.06f, 0f);
            var lr = ringGO.AddComponent<LineRenderer>();
            lr.sharedMaterial = mRing;
            lr.widthMultiplier = 0.06f;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.alignment = LineAlignment.TransformZ;
            ringGO.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var ring = ringGO.AddComponent<PlayerNoiseRing>();
            ring.player = pc;
            goal.player = pc;
            // 어두운 조명에서도 플레이어가 잘 보이도록 약한 보조광
            var pLightGO = new GameObject("PlayerFillLight");
            pLightGO.transform.SetParent(playerGO.transform, false);
            pLightGO.transform.localPosition = new Vector3(0f, 2.2f, -0.6f);
            var pLight = pLightGO.AddComponent<Light>();
            pLight.type = LightType.Point;
            pLight.range = 4f;
            pLight.intensity = 0.9f;
            pLight.color = new Color(0.75f, 0.88f, 1f);
            pLight.shadows = LightShadows.None;
            pLight.renderMode = LightRenderMode.ForcePixel;

            // ---------- Camera ----------
            var camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            var cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = NightColor;
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f; // 맵 대각선 약 70m + 먼 건물 실루엣. 200m는 불필요한 원경까지 그렸다.
            camGO.AddComponent<AudioListener>();
            // 전술(탑다운) 시점: Tab/V로 전환. 확장 맵 전체를 따라가도록 초점 범위를 넓힌다.
            var follow = camGO.AddComponent<TopDownCamera>();
            follow.target = playerGO.transform;
            follow.offset = new Vector3(0f, 24f, -14f);
            follow.focusMin = new Vector2(-34f, -14f);
            follow.focusMax = new Vector2(10f, 15f);
            follow.enabled = false;
            // 기본: 3인칭 어깨너머 카메라
            var tpc = camGO.AddComponent<ThirdPersonCamera>();
            tpc.player = pc;
            tpc.topDown = follow;
            camGO.transform.position = playerGO.transform.position + new Vector3(0f, 2.8f, -4f);
            camGO.transform.rotation = Quaternion.Euler(18f, 0f, 0f);

            // ---------- Guards ----------
            var routes = new GameObject("PatrolRoutes").transform;
            var guardsRoot = new GameObject("Guards").transform;
            var gctx = new GuardBuildContext
            {
                routes = routes, parent = guardsRoot, player = pc, body = mGuard, visor = mGuardVisor,
                orb = mOrb, cone = mCone, font = font
            };
            BuildGuards2(gctx);

            // ---------- Game Manager ----------
            var gmGO = new GameObject("GameManager");
            var gm = gmGO.AddComponent<GameManager>();
            gm.player = pc;
            gm.surfaces = new[] { surface };
            gm.requireExtraction = true;
            var extraction = Object.FindAnyObjectByType<ExtractionZone>();
            if (extraction != null) extraction.player = pc;

            // ---------- 사운드·UI·도구·적응형 지휘·체크포인트·튜토리얼·성능 ----------
            BuildSystems(pc, tpc, gm, mRing);

            // ---------- HDRP: 재질·조명(물리 단위)·밤 볼륨·카메라 ----------
            ShadowProtocolHDRP.ApplyToScene();

            // ---------- Save + Bake ----------
            EditorSceneManager.SaveScene(scene, ScenePath);

            surface.BuildNavMesh();
            if (surface.navMeshData != null)
            {
                if (AssetDatabase.LoadAssetAtPath<NavMeshData>(NavAssetPath) != null)
                    AssetDatabase.DeleteAsset(NavAssetPath);
                surface.navMeshData.name = "NavMesh-Level";
                AssetDatabase.CreateAsset(surface.navMeshData, NavAssetPath);
                EditorUtility.SetDirty(surface);
            }
            else
            {
                Debug.LogError("[ShadowProtocol] NavMesh 베이크 결과가 비어 있습니다.");
            }

            // 가림 컬링(Occlusion Culling): 벽(OccluderStatic) 뒤의 물체를 그리지 않는다
            StaticOcclusionCulling.smallestOccluder = 2f;
            StaticOcclusionCulling.smallestHole = 0.25f;
            if (StaticOcclusionCulling.Compute())
                Debug.Log($"[ShadowProtocol] 가림 컬링 데이터 굽기 완료 ({StaticOcclusionCulling.umbraDataSize / 1024f:0.0} KB)");
            else
                Debug.LogWarning("[ShadowProtocol] 가림 컬링 데이터 굽기 실패");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();

            // ---------- Build Settings ----------
            var list = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();

            var tri = NavMesh.CalculateTriangulation();
            Debug.Log($"[ShadowProtocol] 데모 씬 생성 완료: {ScenePath} / NavMesh 정점 {tri.vertices.Length}, 삼각형 {tri.indices.Length / 3}");
        }

        // ---------------- Guard ----------------

        class GuardBuildContext
        {
            public Transform routes, parent;
            public PlayerController player;
            public Material body, visor, orb, cone;
            public Font font;
        }

        static GuardAI BuildGuard(GuardBuildContext ctx, string name, Vector3[] points, string model = null)
        {
            var route = new GameObject(name + "_Route").transform;
            route.SetParent(ctx.routes, false);
            var tps = new List<Transform>();
            for (int i = 0; i < points.Length; i++)
            {
                var p = new GameObject("P" + i).transform;
                p.SetParent(route, false);
                p.position = points[i];
                tps.Add(p);
            }

            var g = new GameObject(name);
            g.transform.SetParent(ctx.parent, false);
            g.transform.position = points[0];
            Vector3 face = points.Length > 1 ? points[1] - points[0] : Vector3.forward;
            face.y = 0f;
            if (face.sqrMagnitude > 0.01f) g.transform.rotation = Quaternion.LookRotation(face, Vector3.up);

            var col = g.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 0.95f, 0f);
            col.height = 1.9f;
            col.radius = 0.4f;

            var agent = g.AddComponent<NavMeshAgent>();
            agent.radius = 0.4f;
            agent.height = 1.9f;
            agent.speed = 2f;
            agent.angularSpeed = 420f;
            agent.acceleration = 14f;
            agent.stoppingDistance = 0.25f;
            agent.autoBraking = true;
            // 빌드에서 씬 로드 시 NavMeshSurface가 데이터를 등록하기 전에 에이전트가 켜지면 생성에 실패한다.
            // 꺼 두고 GuardAI.Start에서 NavBootstrap.EnableAgent로 켠다.
            agent.enabled = false;

            var ai = g.AddComponent<GuardAI>();
            // 사람다운 이동(몸이 향한 쪽으로 걷고, 크게 꺾을 땐 먼저 돌아섬, 가속·감속). 에이전트는 경로만 계산.
            g.AddComponent<GuardLocomotion>();
            ai.player = ctx.player;
            ai.patrolPoints = tps.ToArray();

            var gBody = Primitive(PrimitiveType.Capsule, g.transform, "Body", new Vector3(0f, 0.95f, 0f), new Vector3(0.8f, 0.95f, 0.8f), ctx.body);
            var gVisor = Primitive(PrimitiveType.Cube, g.transform, "Visor", new Vector3(0f, 1.6f, 0.33f), new Vector3(0.55f, 0.14f, 0.2f), ctx.visor);
            // 경비병마다 다른 모델(없으면 기본 경비병)
            // 모델: "super" = 경비 반장(중앙 순찰·사무동 감독관), 그 밖 = 중무장 경비병
            bool wantSuper = model == "super";
            string gModel = wantSuper ? CaptainModelPath : GuardModelPath;
            float gHeight = wantSuper ? 1.804f : 1.903f;
            AttachModel(g.transform, gModel, new[] { gBody.GetComponent<Renderer>(), gVisor.GetComponent<Renderer>() }, gHeight, null);
            var orb = Primitive(PrimitiveType.Sphere, g.transform, "StatusOrb", new Vector3(0f, 2.15f, 0f), Vector3.one * 0.22f, ctx.orb);
            orb.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

            var labelGO = new GameObject("StatusLabel");
            labelGO.transform.SetParent(g.transform, false);
            labelGO.transform.localPosition = new Vector3(0f, 2.75f, 0f);
            var tm = labelGO.AddComponent<TextMesh>();
            tm.text = "";
            tm.font = ctx.font;
            tm.fontSize = 96;
            tm.characterSize = 0.06f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontStyle = FontStyle.Bold;
            var tmr = labelGO.GetComponent<MeshRenderer>();
            if (ctx.font != null) tmr.sharedMaterial = ctx.font.material;
            tmr.shadowCastingMode = ShadowCastingMode.Off;

            var bar = Primitive(PrimitiveType.Cube, g.transform, "SuspicionBar", new Vector3(0f, 2.42f, 0f), new Vector3(1f, 0.08f, 0.02f), ctx.orb);
            bar.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

            var coneGO = new GameObject("VisionCone");
            coneGO.transform.SetParent(g.transform, false);
            coneGO.transform.localPosition = new Vector3(0f, 0.04f, 0f);
            coneGO.AddComponent<MeshFilter>();
            var cmr = coneGO.AddComponent<MeshRenderer>();
            cmr.sharedMaterial = ctx.cone;
            cmr.shadowCastingMode = ShadowCastingMode.Off;
            cmr.receiveShadows = false;
            var vc = coneGO.AddComponent<VisionCone>();
            vc.guard = ai;
            // 2026-11-23 사용자 지시: 바닥 시야 부채꼴 표시를 숨긴다(사실적인 화면). 표시 전용이라 감지 판정(GuardAI)은 그대로.
            // 다시 보이게 하려면 이 줄을 지우고 Build Demo Scene.
            coneGO.SetActive(false);

            var ind = g.AddComponent<GuardStatusIndicator>();
            ind.guard = ai;
            ind.label = tm;
            ind.orb = orb.GetComponent<Renderer>();
            ind.bar = bar.transform;
            ind.barRenderer = bar.GetComponent<Renderer>();
            return ai;
        }

        // ---------------- Helpers ----------------

        /// <summary>모델 FBX가 있으면 캐릭터 자식으로 넣고 캡슐 렌더러를 끈다. 없으면 null.</summary>
        public static GameObject AttachModel(Transform owner, string path, Renderer[] hide, float targetHeight, PlayerController pc)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (src == null)
            {
                Debug.Log($"[ShadowProtocol] 모델 없음({path}) - 캡슐 외형 유지");
                return null;
            }
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(src, owner);
            inst.name = "Model";
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.identity;
            inst.transform.localScale = Vector3.one;

            // 정면 맞추기: 왼→오른 엉덩이 본으로 오른쪽을 구하고, 발끝 방향으로 앞/뒤 부호를 정해 +Z에 맞춘다.
            var lLeg = FindDeep(inst.transform, "LeftUpLeg");
            var rLeg = FindDeep(inst.transform, "RightUpLeg");
            if (lLeg != null && rLeg != null)
            {
                Vector3 right = rLeg.position - lLeg.position;
                right.y = 0f;
                if (right.sqrMagnitude > 1e-6f)
                {
                    Vector3 fwd = Vector3.Cross(right.normalized, Vector3.up);
                    var lFoot = FindDeep(inst.transform, "LeftFoot");
                    var lToe = FindDeep(inst.transform, "LeftToeBase");
                    var rFoot = FindDeep(inst.transform, "RightFoot");
                    var rToe = FindDeep(inst.transform, "RightToeBase");
                    if (lFoot != null && lToe != null && rFoot != null && rToe != null)
                    {
                        Vector3 toes = (lToe.position - lFoot.position) + (rToe.position - rFoot.position);
                        if (Vector3.Dot(toes, fwd) < 0f) fwd = -fwd;
                    }
                    inst.transform.localRotation = Quaternion.Euler(0f, Vector3.SignedAngle(fwd, owner.forward, Vector3.up), 0f);
                }
            }

            // 키 보정(가져오기 단위가 크게 어긋난 경우만) + 발을 바닥에
            MeshRangeY(inst, out float minY, out float maxY);
            float h = maxY - minY;
            if (h > 0.01f)
            {
                float ratio = targetHeight / h;
                if (ratio < 0.8f || ratio > 1.25f)
                {
                    inst.transform.localScale = Vector3.one * ratio;
                    MeshRangeY(inst, out minY, out maxY);
                }
                inst.transform.position += Vector3.up * (owner.position.y - minY);
            }

            foreach (var r in hide) if (r != null) r.enabled = false;
            var pose = inst.AddComponent<CharacterProceduralPose>();
            pose.mover = owner;
            pose.player = pc;
            SetupAnimator(inst, owner, pc);
            return inst;
        }

        /// <summary>실제 정점 기준 월드 Y 범위(스킨 메시는 BakeMesh).</summary>
        static void MeshRangeY(GameObject go, out float minY, out float maxY)
        {
            minY = float.MaxValue;
            maxY = float.MinValue;
            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var baked = new Mesh();
                smr.BakeMesh(baked, true);
                var m = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
                foreach (var v in baked.vertices)
                {
                    float y = m.MultiplyPoint3x4(v).y;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
                Object.DestroyImmediate(baked);
            }
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                foreach (var v in mf.sharedMesh.vertices)
                {
                    float y = mf.transform.TransformPoint(v).y;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
            if (minY > maxY) { minY = maxY = go.transform.position.y; }
        }

        static Bounds WorldBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        static Transform FindDeep(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name || t.name.EndsWith(":" + name)) return t;
            return null;
        }

        // ---------------- Environment ----------------

        static void BuildEnvironment(Material asphalt, Material lotLine, Material drain, Material fence, Material lampHead, Material workLight, Material farBuilding)
        {
            var env = new GameObject("Environment").transform;

            // 야외 부지 바닥(충돌 없음, NavMesh 제외)
            var ground = Primitive(PrimitiveType.Plane, env, "OutdoorGround", new Vector3(0f, -0.02f, 6f), new Vector3(12f, 1f, 12f), asphalt);
            ground.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

            // 주차선(남쪽·북쪽)
            var lot = Group(env, "ParkingLines");
            foreach (float z in new[] { -15.5f, 15.5f })
            {
                for (int i = -5; i <= 5; i++)
                    Box(lot, "Line", new Vector3(i * 2.6f, -0.005f, z), new Vector3(0.1f, 0.01f, 4f), lotLine, false);
                float edge = z < 0 ? z + 2.2f : z - 2.2f;
                Box(lot, "Edge", new Vector3(0f, -0.005f, edge), new Vector3(28.8f, 0.01f, 0.12f), lotLine, false);
            }

            // 배수구
            var drains = Group(env, "Drains");
            foreach (var p in new[] { new Vector3(-18f, 0f, -11f), new Vector3(18f, 0f, -11f), new Vector3(-18f, 0f, 13f), new Vector3(18f, 0f, 13f), new Vector3(0f, 0f, -18f) })
            {
                Box(drains, "Drain", p + new Vector3(0f, -0.004f, 0f), new Vector3(0.7f, 0.01f, 0.7f), drain, false);
                for (int k = -2; k <= 2; k++)
                    Box(drains, "Grate", p + new Vector3(k * 0.13f, 0.002f, 0f), new Vector3(0.03f, 0.01f, 0.6f), fence, false);
            }

            // 철조망 펜스(기둥 + 가로선)
            var fenceRoot = Group(env, "Fence");
            float xMin = -24f, xMax = 24f, zMin = -20f, zMax = 26f;
            System.Action<Vector3, Vector3> side = (a, b) =>
            {
                float len = Vector3.Distance(a, b);
                int posts = Mathf.CeilToInt(len / 4f);
                for (int i = 0; i <= posts; i++)
                {
                    var pp = Vector3.Lerp(a, b, i / (float)posts);
                    Primitive(PrimitiveType.Cylinder, fenceRoot, "Post", pp + new Vector3(0f, 1.25f, 0f), new Vector3(0.09f, 1.25f, 0.09f), fence);
                }
                Vector3 mid = (a + b) * 0.5f;
                bool alongX = Mathf.Abs(b.x - a.x) > Mathf.Abs(b.z - a.z);
                foreach (float h in new[] { 0.5f, 1.3f, 2.1f, 2.45f })
                    Box(fenceRoot, "Wire", mid + new Vector3(0f, h, 0f), alongX ? new Vector3(len, 0.025f, 0.025f) : new Vector3(0.025f, 0.025f, len), fence, false);
            };
            side(new Vector3(xMin, 0f, zMin), new Vector3(xMax, 0f, zMin));
            side(new Vector3(xMin, 0f, zMax), new Vector3(xMax, 0f, zMax));
            side(new Vector3(xMin, 0f, zMin), new Vector3(xMin, 0f, zMax));
            side(new Vector3(xMax, 0f, zMin), new Vector3(xMax, 0f, zMax));

            // 가로등(약한 주황색 포인트 라이트)
            var lamps = Group(env, "StreetLamps");
            foreach (var p in new[] { new Vector3(-19f, 0f, -13f), new Vector3(19f, 0f, -13f), new Vector3(-19f, 0f, 17f), new Vector3(19f, 0f, 17f), new Vector3(0f, 0f, -18.5f), new Vector3(0f, 0f, 22f) })
            {
                var lamp = new GameObject("Lamp").transform;
                lamp.SetParent(lamps, false);
                lamp.position = p;
                Primitive(PrimitiveType.Cylinder, lamp, "Pole", new Vector3(0f, 2.5f, 0f), new Vector3(0.14f, 2.5f, 0.14f), fence);
                Primitive(PrimitiveType.Cube, lamp, "Head", new Vector3(0f, 5.05f, 0f), new Vector3(0.6f, 0.12f, 0.35f), lampHead).GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                var lgo = new GameObject("Light");
                lgo.transform.SetParent(lamp, false);
                lgo.transform.localPosition = new Vector3(0f, 4.7f, 0f);
                var l = lgo.AddComponent<Light>();
                l.type = LightType.Point;
                l.range = 14f;
                l.intensity = 1.6f;
                l.color = new Color(1f, 0.74f, 0.45f);
                l.shadows = LightShadows.None;
            }

            // 먼 건물 실루엣(안개 속으로 사라지며 깊이감)
            var far = Group(env, "FarBuildings");
            Box(far, "Bldg", new Vector3(-14f, 4f, 36f), new Vector3(16f, 8f, 8f), farBuilding, false);
            Box(far, "Bldg", new Vector3(8f, 6f, 40f), new Vector3(14f, 12f, 10f), farBuilding, false);
            Box(far, "Bldg", new Vector3(34f, 3.5f, 10f), new Vector3(8f, 7f, 20f), farBuilding, false);
            Box(far, "Bldg", new Vector3(-34f, 5f, 4f), new Vector3(8f, 10f, 18f), farBuilding, false);

            // 창고 천장 작업등(개별 픽셀 라이트, 그림자 없음)
            var work = Group(env, "WorkLights");
            // 천장 트러스 보 2줄(벽 위를 가로질러 작업등을 매단다). 시야를 가리지 않게 얇게, 그림자 없음.
            foreach (float bz in new[] { -6f, 5.5f })
                Box(work, "TrussBeam", new Vector3(0f, 3.12f, bz), new Vector3(31f, 0.1f, 0.16f), fence, false).GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            foreach (var p in new[] { new Vector3(-11f, 0f, -6f), new Vector3(-3.5f, 0f, -6f), new Vector3(4.5f, 0f, -6f), new Vector3(11f, 0f, -6f), new Vector3(-11f, 0f, 5.5f), new Vector3(-3.5f, 0f, 5.5f), new Vector3(4.5f, 0f, 5.5f), new Vector3(11f, 0f, 5.5f) })
            {
                var fx = new GameObject("WorkLight").transform;
                fx.SetParent(work, false);
                fx.position = p;
                Primitive(PrimitiveType.Cube, fx, "Fixture", new Vector3(0f, 3.04f, 0f), new Vector3(0.9f, 0.05f, 0.22f), workLight).GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                var lgo = new GameObject("Light");
                lgo.transform.SetParent(fx, false);
                lgo.transform.localPosition = new Vector3(0f, 2.75f, 0f);
                var l = lgo.AddComponent<Light>();
                l.type = LightType.Point;
                l.range = 8.5f;
                l.intensity = 1.35f;
                l.color = new Color(1f, 0.93f, 0.8f);
                l.shadows = LightShadows.None;
                l.renderMode = LightRenderMode.ForcePixel;
            }
        }

        // ---------------- Procedural textures ----------------

        static Texture2D EnsureTex(string name, int size, System.Func<float, float, Color> albedo, System.Func<float, float, float> height, float strength = 2f)
        {
            string path = TexFolder + "/" + name + ".png";
            bool normal = height != null;
            if (!File.Exists(path))
            {
                var px = new Color[size * size];
                if (!normal)
                {
                    for (int y = 0; y < size; y++)
                        for (int x = 0; x < size; x++)
                            px[y * size + x] = albedo((float)x / size, (float)y / size);
                }
                else
                {
                    var h = new float[size * size];
                    for (int y = 0; y < size; y++)
                        for (int x = 0; x < size; x++)
                            h[y * size + x] = height((float)x / size, (float)y / size);
                    for (int y = 0; y < size; y++)
                        for (int x = 0; x < size; x++)
                        {
                            float l = h[y * size + (x - 1 + size) % size], r = h[y * size + (x + 1) % size];
                            float d = h[((y - 1 + size) % size) * size + x], u = h[((y + 1) % size) * size + x];
                            var n = new Vector3((l - r) * strength, (d - u) * strength, 1f).normalized;
                            px[y * size + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
                        }
                }
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                tex.SetPixels(px);
                tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
                var ti = (TextureImporter)AssetImporter.GetAtPath(path);
                ti.wrapMode = TextureWrapMode.Repeat;
                ti.anisoLevel = 4;
                if (normal) ti.textureType = TextureImporterType.NormalMap;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Material MatTex(string name, Color c, Texture2D albedo, Texture2D normal, Vector2 tiling)
        {
            var m = Mat(name, c);
            m.mainTexture = albedo;
            m.mainTextureScale = tiling;
            if (normal != null && m.HasProperty("_BumpMap"))
            {
                m.SetTexture("_BumpMap", normal);
                m.EnableKeyword("_NORMALMAP");
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        // 타일 가능한 노이즈(네 모서리 샘플 블렌드)
        static float TN(float u, float v, float freq, float off)
        {
            float a = Mathf.PerlinNoise(u * freq + off, v * freq + off);
            float b = Mathf.PerlinNoise((u - 1f) * freq + off, v * freq + off);
            float c = Mathf.PerlinNoise(u * freq + off, (v - 1f) * freq + off);
            float d = Mathf.PerlinNoise((u - 1f) * freq + off, (v - 1f) * freq + off);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }

        static float Fbm(float u, float v, float freq, int oct, float off)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            for (int i = 0; i < oct; i++) { sum += TN(u, v, freq, off + i * 17.3f) * amp; norm += amp; amp *= 0.5f; freq *= 2f; }
            return sum / norm;
        }

        static float Hash(float u, float v)
        {
            float h = Mathf.Sin(u * 12.9898f * 512f + v * 78.233f * 512f) * 43758.5453f;
            return h - Mathf.Floor(h);
        }

        static float TileLine(float u, float v, float tiles, float w)
        {
            float tu = (u * tiles) % 1f, tv = (v * tiles) % 1f;
            return (tu < w || tu > 1f - w || tv < w || tv > 1f - w) ? 1f : 0f;
        }

        static Color ConcreteAlbedo(float u, float v)
        {
            float n = Fbm(u, v, 6f, 4, 1.3f);
            float stain = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 0.75f, TN(u, v, 3f, 7.1f)));
            float g = 0.42f + (n - 0.5f) * 0.14f - stain * 0.09f - TileLine(u, v, 4f, 0.006f) * 0.13f;
            if (Hash(u, v) > 0.985f) g -= 0.05f;
            return new Color(g, g * 1.01f, g * 1.04f);
        }

        static float ConcreteHeight(float u, float v) => Fbm(u, v, 24f, 3, 3.3f) * 0.35f - TileLine(u, v, 4f, 0.006f) * 0.5f;

        static Color WallAlbedo(float u, float v)
        {
            float rib = 0.5f + 0.5f * Mathf.Sin(u * Mathf.PI * 2f * 16f);
            var c = new Color(0.3f, 0.33f, 0.37f) * (0.88f + 0.12f * rib);
            c *= Mathf.Lerp(0.65f, 1f, Mathf.Clamp01(v * 3f));
            float rust = Mathf.InverseLerp(0.62f, 0.8f, TN(u, v * 0.5f, 5f, 4.4f));
            c = Color.Lerp(c, new Color(0.33f, 0.22f, 0.15f), rust * 0.55f);
            if ((u * 4f) % 1f < 0.01f) c *= 0.6f;
            c.a = 1f;
            return c;
        }

        static float WallHeight(float u, float v) => 0.5f + 0.5f * Mathf.Sin(u * Mathf.PI * 2f * 16f) - (((u * 4f) % 1f < 0.01f) ? 0.6f : 0f);

        static Color AsphaltAlbedo(float u, float v)
        {
            float g = 0.13f + (Fbm(u, v, 8f, 4, 9.1f) - 0.5f) * 0.07f;
            float s = Hash(u, v);
            if (s > 0.97f) g += 0.06f; else if (s < 0.02f) g -= 0.03f;
            return new Color(g, g, g * 1.05f);
        }

        static float AsphaltHeight(float u, float v) => Fbm(u, v, 32f, 2, 5.5f) * 0.4f + (Hash(u, v) > 0.97f ? 0.2f : 0f);

        // ---------------- SP2 props ----------------

        const string SP2Root = "Assets/ShadowProtocol/Models/SP2/";
        /// <summary>보류: true면 기존 VARCO 컨테이너 사용. 2026-11-16 Blender 최종본(container_final) 반영 → false.</summary>
        public static bool HoldContainerV2 = false;
        public const string ContainerFinalPath = "Assets/ShadowProtocol/Models/ContainerFinal/container_final.fbx";

        static GameObject LoadSP2(string n)
        {
            // 컨테이너: Blender 최종본(container_final, ISO 20ft, LOD0/1/2 → LODGroup)이 있으면 그것을 쓴다
            if (n == "container" && !HoldContainerV2)
            {
                var fin = AssetDatabase.LoadAssetAtPath<GameObject>(ContainerFinalPath);
                if (fin != null) return fin;
            }
            return AssetDatabase.LoadAssetAtPath<GameObject>(SP2Root + "SP2_" + n + ".fbx");
        }

        static void BuildSP2Props(Transform level, Material beamMat)
        {
            var inside = new GameObject("Props2").transform;      // NavMesh 장애물(레벨 자식)
            inside.SetParent(level, false);
            var outside = new GameObject("OutdoorProps").transform;
            var cams = new GameObject("SecurityCameras").transform;  // 장식(감지 로직 없음)

            // 경비실 코너(북서쪽)
            PlaceProp(inside, "desk", "GuardDesk", new Vector3(-13.9f, 0f, 10.1f), 180f, true, "+z", 11f);
            PlaceProp(cams, "camera", "Cam_GuardPost", new Vector3(-12.6f, 2.35f, 10.8f), 180f, false, "+z", 11f);
            PlaceProp(cams, "camera", "Cam_North", new Vector3(6f, 2.35f, 10.8f), 180f, false, "+z", 11f);
            PlaceProp(cams, "camera", "Cam_South", new Vector3(-5f, 2.35f, -10.8f), 0f, false, "-z", -11f);
            PlaceProp(cams, "camera", "Cam_East", new Vector3(14.8f, 2.35f, -5f), -90f, false, "+x", 15f);

            // 추가 엄폐물(벽면·구석 위주, 순찰 경로·통로 회피)
            PlaceProp(inside, "pallets", "Pallets_W", new Vector3(-14.5f, 0f, -1.6f), 0f, true, "-x", -15f);
            PlaceProp(inside, "pallets", "Pallets_SE", new Vector3(14.4f, 0f, -10.2f), 0f, true, "+x", 15f);
            PlaceProp(inside, "drums", "Drums_W", new Vector3(-14.4f, 0f, 5.5f), 0f, true, "-x", -15f);
            PlaceProp(inside, "drums", "Drums_S", new Vector3(-4f, 0f, -10.4f), 0f, true, "-z", -11f);
            PlaceProp(inside, "tarp", "Tarp_NW", new Vector3(-6.5f, 0f, 10.4f), 0f, true, "+z", 11f);
            PlaceProp(inside, "tarp", "Tarp_NE", new Vector3(7.5f, 0f, 10.4f), 0f, true, "+z", 11f);

            // 데이터 코어 주변: 사물함·서버 랙
            PlaceProp(inside, "lockers", "Lockers_North", new Vector3(10f, 0f, 10.7f), 180f, true, "+z", 11f);
            PlaceProp(inside, "lockers", "Lockers_Partition", new Vector3(1.8f, 0f, 8.5f), 90f, true, "-x", 1.5f);
            PlaceProp(inside, "server", "Server_1", new Vector3(14.6f, 0f, 6.5f), -90f, true, "+x", 15f);
            PlaceProp(inside, "server", "Server_2", new Vector3(14.6f, 0f, 7.35f), -90f, true, "+x", 15f);

            // 데이터 코어 접근로 양옆 레이저 트립와이어(장식, 판정 없음)
            // 서쪽 접근 통로(Crate_E4 북면 ~ 북쪽 사물함 앞면) 양쪽 가장자리에 붙인다. 장식이라 충돌체 없음.
            var la = PlaceProp(outside, "laser", "Laser_A", new Vector3(9.9f, 0f, 8f), 0f, false, "-z", 7.75f);
            var lb = PlaceProp(outside, "laser", "Laser_B", new Vector3(9.9f, 0f, 10.2f), 180f, false, "+z", 10.4f);
            if (la != null && lb != null)
            {
                var beamRoot = new GameObject("LaserBeam").transform;
                beamRoot.SetParent(outside, false);
                Vector3 a = la.transform.position + Vector3.up * 0.45f, b = lb.transform.position + Vector3.up * 0.45f;
                var beam = Box(beamRoot, "Beam", (a + b) * 0.5f, new Vector3(0.015f, 0.015f, Vector3.Distance(a, b) - 0.3f), beamMat, false);
                beam.transform.rotation = Quaternion.LookRotation(b - a);
                var br = beam.GetComponent<Renderer>(); br.shadowCastingMode = ShadowCastingMode.Off; br.receiveShadows = false;
                foreach (var dev in new[] { la, lb })
                {
                    var lg = new GameObject("RedGlow"); lg.transform.SetParent(dev.transform, false); lg.transform.localPosition = new Vector3(0f, 0.5f, 0f);
                    var l = lg.AddComponent<Light>(); l.type = LightType.Point; l.color = new Color(1f, 0.15f, 0.1f); l.range = 1.6f; l.intensity = 1.2f; l.shadows = LightShadows.None;
                }
            }

            // 야외 부지(펜스 안쪽): 컨테이너·지게차
            const float yard = -0.02f; // 야외 아스팔트 바닥 높이(OutdoorGround)
            PlaceProp(outside, "container", "Container_W", new Vector3(-19.5f, yard, 3f), 90f, true, null, 0f);
            PlaceProp(outside, "container", "Container_E", new Vector3(19.5f, yard, -2f), 90f, true, null, 0f);
            PlaceProp(outside, "forklift", "Forklift", new Vector3(-19.5f, yard, -4f), 0f, true, null, 0f);
        }

        /// <summary>소품 배치: pos.y = 바닥(또는 부착) 높이. flush(+x/-x/+z/-z)면 해당 방향 끝을 plane에 맞춤. collider면 메시 크기 BoxCollider.</summary>
        static GameObject PlaceProp(Transform parent, string model, string name, Vector3 pos, float yaw, bool collider, string flush, float plane)
        {
            var prefab = LoadSP2(model);
            if (prefab == null) return null;
            // 컨테이너 최종본(container_final)은 길이 축이 X로 이전 VARCO 모델과 같아 회전 보정이 필요 없다
            var h = SpawnProp(prefab, parent, pos, Quaternion.Euler(0f, yaw, 0f), Vector3.one);
            h.name = name;
            var wb = WorldMeshBounds(h.transform);
            Vector3 shift = new Vector3(0f, pos.y - wb.min.y, 0f);
            const float gap = 0.005f;
            switch (flush)
            {
                case "+x": shift.x = (plane - gap) - wb.max.x; break;
                case "-x": shift.x = (plane + gap) - wb.min.x; break;
                case "+z": shift.z = (plane - gap) - wb.max.z; break;
                case "-z": shift.z = (plane + gap) - wb.min.z; break;
            }
            h.transform.position += shift;
            if (collider)
            {
                var lb = LocalMeshBounds(h.transform);
                var bc = h.AddComponent<BoxCollider>();
                bc.center = lb.center;
                bc.size = lb.size;
            }
            return h;
        }

        static Bounds WorldMeshBounds(Transform root)
        {
            bool first = true; var b = new Bounds();
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                foreach (var v in mf.sharedMesh.vertices)
                {
                    var w = mf.transform.TransformPoint(v);
                    if (first) { b = new Bounds(w, Vector3.zero); first = false; } else b.Encapsulate(w);
                }
            }
            return b;
        }

        static Bounds LocalMeshBounds(Transform root)
        {
            bool first = true; var b = new Bounds();
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                foreach (var v in mf.sharedMesh.vertices)
                {
                    var l = root.InverseTransformPoint(mf.transform.TransformPoint(v));
                    if (first) { b = new Bounds(l, Vector3.zero); first = false; } else b.Encapsulate(l);
                }
            }
            return b;
        }

        // ---------------- Props ----------------

        const string PropRoot = "Assets/ShadowProtocol/Models/Props/";
        const float MaxAxisRatio = 1.4f;

        static GameObject LoadProp(string name) => AssetDatabase.LoadAssetAtPath<GameObject>(PropRoot + name + ".fbx");

        static void DressLevel(Transform walls, Transform cover, Material capMat)
        {
            var shelf = LoadProp("Prop_Shelf");
            var crate = LoadProp("Prop_Crate");
            var bins = LoadProp("Prop_Bins");
            var wall = LoadProp("Prop_Wall");
            foreach (Transform t in cover)
            {
                var r = t.GetComponent<MeshRenderer>();
                if (t.name.StartsWith("Shelf")) FillBox(t, shelf);
                else if (t.name.StartsWith("Crate") && r != null && r.sharedMaterial != null && r.sharedMaterial.name == "LowCrate") FillBox(t, bins);
                else if (t.name.StartsWith("Crate")) FillBox(t, crate);
            }
            foreach (Transform t in walls)
                if (t.name.StartsWith("Partition") && LoadEnv("wall") == null) DressPartition(t, wall, capMat); // 벽 최종본이 있으면 ApplyEnvironmentModules가 칸막이도 env_wall로
        }

        static Vector3 PrefabSize(GameObject prefab)
        {
            var tmp = (GameObject)Object.Instantiate(prefab);
            var rs = tmp.GetComponentsInChildren<Renderer>();
            var b = rs.Length > 0 ? rs[0].bounds : new Bounds(Vector3.zero, Vector3.one);
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            Object.DestroyImmediate(tmp);
            return b.size;
        }

        // 반복 개수: 길이축 스케일이 높이축 스케일에 가장 가까워지는 n
        static int BestCount(float length, float moduleWidth, float sy)
        {
            int best = 1; float bestErr = float.MaxValue;
            for (int n = 1; n <= 24; n++)
            {
                float err = Mathf.Abs(Mathf.Log(length / (moduleWidth * n) / sy));
                if (err < bestErr) { bestErr = err; best = n; }
            }
            return best;
        }

        // 박스 스케일을 상쇄해 월드 단위로 배치할 수 있는 자식 컨테이너(박스 중심 기준)
        static Transform UnitContainer(Transform box)
        {
            var s = box.localScale;
            var c = new GameObject("PropVisual").transform;
            c.SetParent(box, false);
            c.localScale = new Vector3(1f / s.x, 1f / s.y, 1f / s.z);
            return c;
        }

        /// <summary>홀더에 위치·회전·스케일을 주고 모델은 자식으로 둔다(FBX 루트의 축 보정 회전 유지).</summary>
        static GameObject SpawnProp(GameObject prefab, Transform parent, Vector3 localPos, Quaternion localRot, Vector3 localScale)
        {
            var holder = new GameObject(prefab.name).transform;
            holder.SetParent(parent, false);
            holder.localPosition = localPos;
            holder.localRotation = localRot;
            holder.localScale = localScale;
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, holder);
            StripColliders(inst);
            return holder.gameObject;
        }

        static void StripColliders(GameObject go)
        {
            foreach (var col in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(col);
        }

        static void FillBox(Transform box, GameObject prefab)
        {
            if (prefab == null) return;
            Vector3 S = box.localScale;
            Vector3 m = PrefabSize(prefab);
            bool alongX = S.x >= S.z;
            float L = alongX ? S.x : S.z, T = alongX ? S.z : S.x, H = S.y;
            float sy = H / m.y; // 높이는 박스와 정확히 일치(숨기 판정과 시각 일치)
            int n = BestCount(L, m.x, sy);
            float sx = Mathf.Clamp(L / (m.x * n), sy / MaxAxisRatio, sy * MaxAxisRatio);
            float sz = Mathf.Clamp(T / m.z, sy / MaxAxisRatio, sy * MaxAxisRatio);
            if (sz > sx * MaxAxisRatio) sz = sx * MaxAxisRatio;
            if (sx > sz * MaxAxisRatio) sz = sx / MaxAxisRatio;
            var c = UnitContainer(box);
            float module = L / n;
            for (int i = 0; i < n; i++)
            {
                float off = -L * 0.5f + module * (i + 0.5f);
                SpawnProp(prefab, c,
                    (alongX ? new Vector3(off, 0f, 0f) : new Vector3(0f, 0f, off)) + Vector3.down * (H * 0.5f),
                    alongX ? Quaternion.identity : Quaternion.Euler(0f, 90f, 0f),
                    new Vector3(sx, sy, sz));
            }
            box.GetComponent<MeshRenderer>().enabled = false;
        }

        // 칸막이: 벽 패널을 양면에 단수×길이 방향으로 타일링하고 윗면·끝면을 마감
        static void DressPartition(Transform box, GameObject panel, Material capMat)
        {
            if (panel == null) return;
            Vector3 S = box.localScale;
            Vector3 m = PrefabSize(panel);
            bool alongX = S.x >= S.z;
            float L = alongX ? S.x : S.z, T = alongX ? S.z : S.x, H = S.y;
            int rows = Mathf.Max(1, Mathf.RoundToInt(H / m.y));
            float rowH = H / rows;
            float sy = rowH / m.y;
            int n = BestCount(L, m.x, sy);
            float sx = L / (m.x * n);
            // 너무 짧아 패널이 지나치게 찌그러지는 벽 조각은 기본 벽 재질 그대로 둔다
            if (sx < sy / MaxAxisRatio || sx > sy * MaxAxisRatio) return;
            float module = L / n;
            var c = UnitContainer(box);
            foreach (int side in new[] { -1, 1 })
                for (int r = 0; r < rows; r++)
                    for (int i = 0; i < n; i++)
                    {
                        float along = -L * 0.5f + module * (i + 0.5f);
                        float across = side * (T * 0.5f - m.z * 0.5f);
                        // 패널 정면(+Z)이 바깥을 향하게
                        float yaw = alongX ? (side > 0 ? 0f : 180f) : (side > 0 ? 90f : -90f);
                        SpawnProp(panel, c,
                            (alongX ? new Vector3(along, 0f, across) : new Vector3(across, 0f, along)) + Vector3.up * (-H * 0.5f + r * rowH),
                            Quaternion.Euler(0f, yaw, 0f), new Vector3(sx, sy, 1f));
                    }
            Vector3 top = alongX ? new Vector3(L, 0.04f, T) : new Vector3(T, 0.04f, L);
            Box(c, "Cap", new Vector3(0f, H * 0.5f - 0.02f, 0f), top, capMat, false);
            foreach (int e in new[] { -1, 1 })
            {
                Vector3 pos = alongX ? new Vector3(e * (L * 0.5f - 0.02f), 0f, 0f) : new Vector3(0f, 0f, e * (L * 0.5f - 0.02f));
                Vector3 sc = alongX ? new Vector3(0.04f, H, T) : new Vector3(T, H, 0.04f);
                Box(c, "EndCap", pos, sc, capMat, false);
            }
            box.GetComponent<MeshRenderer>().enabled = false;
        }

        static Transform Group(Transform parent, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 scale, Material m, bool collider = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = m;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        static GameObject Primitive(PrimitiveType type, Transform parent, string name, Vector3 localPos, Vector3 localScale, Material m)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            go.GetComponent<Renderer>().sharedMaterial = m;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        static Shader LitShader()
        {
            // 빌더는 Standard 값(_Color·_MainTex·_EmissionColor)으로 재질을 만들고,
            // Build() 끝의 ShadowProtocolHDRP.ApplyToScene()이 HDRP/Lit로 한꺼번에 바꾼다(마스크 맵·nits 포함).
            Shader s = Shader.Find("Standard");
            if (s == null) s = Shader.Find("Diffuse");
            return s;
        }

        static Material Mat(string name, Color c, float emission = 0f)
        {
            string path = $"{MatFolder}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = LitShader();
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            else if (m.shader != shader)
            {
                m.shader = shader;
            }

            m.enableInstancing = true; // 정적 배칭에서 빠지는 반복 물체(경비·소품 등)를 인스턴싱으로 묶는다
            m.color = c;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.15f);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.15f);
            if (emission > 0f)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", c * emission);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                m.DisableKeyword("_EMISSION");
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material UnlitTransparentMat(string name)
        {
            string path = $"{MatFolder}/{name}.mat";
            var shader = Shader.Find("Sprites/Default");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            else m.shader = shader;
            m.enableInstancing = true;
            m.color = Color.white;
            m.renderQueue = 3000;
            EditorUtility.SetDirty(m);
            return m;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            string leaf = path.Substring(slash + 1);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
