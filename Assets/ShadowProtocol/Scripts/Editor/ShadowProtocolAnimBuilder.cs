using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ShadowProtocol.EditorTools
{
    /// <summary>
    /// Blender 최종 애니메이션(Assets/ShadowProtocol/Animations/Final/&lt;인물&gt;@&lt;클립&gt;.fbx, 메뉴 Import Characters Final로 복사)을
    /// Humanoid 클립으로 설정하고(아바타는 같은 인물 최종 모델에서 복사) 인물별 Animator Controller를 만든다.
    /// 메뉴 ShadowProtocol/Setup Animations, 씬 빌드에서도 호출.
    /// 클립 구간(Walk 31–65 등)·정면 보정·루프 이음매·발 고정은 Blender에서 이미 구웠으므로 여기서는 자르거나 돌리지 않는다.
    /// </summary>
    public static class ShadowProtocolAnimBuilder
    {
        public const string SourceDir = "Assets/ShadowProtocol/Animations/Final/";
        public const string ModelDir = "Assets/ShadowProtocol/Models/CharactersFinal/";
        public const string Dir = "Assets/ShadowProtocol/Animations/";
        public const string PlayerControllerPath = Dir + "SP_PlayerAnimator.controller";
        public const string GuardControllerPath = Dir + "SP_GuardAnimator.controller";            // 중무장 경비병
        public const string CaptainControllerPath = Dir + "SP_GuardCaptainAnimator.controller";   // 경비 반장
        public const string UpperMaskPath = Dir + "SP_UpperBody.mask";

        public const string Player = "female_agent", Heavy = "heavyguard_v2", Captain = "supervisor_v2";
        public static string ModelPath(string who) => ModelDir + who + "_final.fbx";

        // 클립 고유 이동 속도(m/s): Blender에서 접지 구간의 발 이동으로 잰 값(BLENDER_PROGRESS D). 블렌드 임계값 = 이 속도라 발이 미끄러지지 않는다.
        public const float PlayerWalkNatural = 0.89f, PlayerRunNatural = 3.80f;
        // 웅크려 걷기 측정값(1.75)은 발을 끄는 동작이라 신뢰가 낮아 기존 값(1.0)을 유지
        public const float PlayerCrouchWalkNatural = 1.0f;
        public const float HeavyWalkNatural = 0.87f, HeavyRunNatural = 4.07f;
        public const float CaptainWalkNatural = 0.76f, CaptainRunNatural = 3.56f;
        public const float GuardWalkNatural = HeavyWalkNatural, GuardRunNatural = HeavyRunNatural;
        /// <summary>던지기: 와인드업 앞부분을 건너뛰고(정규화 시작) 빠르게 재생.</summary>
        public const float ThrowStartNormalized = 0.38f, ThrowSpeed = 1.5f;

        public static float RunNatural(string who) => who == Player ? PlayerRunNatural : who == Captain ? CaptainRunNatural : HeavyRunNatural;
        public static float WalkNatural(string who) => who == Player ? PlayerWalkNatural : who == Captain ? CaptainWalkNatural : HeavyWalkNatural;
        public static string ControllerPath(string who) => who == Player ? PlayerControllerPath : who == Captain ? CaptainControllerPath : GuardControllerPath;

        struct ClipDef
        {
            public string who, name; public bool loop; public int first, last; public float rotOffset;
            public ClipDef(string w, string n, bool l) { who = w; name = n; loop = l; first = -1; last = -1; rotOffset = 0f; }
            public string File => SourceDir + who + "@" + name + ".fbx";
        }

        // (first, last, rotOffset) = (-1, -1, 0): 구간·방향이 클립에 이미 구워져 있다(다시 적용하면 이중 적용)
        static readonly ClipDef[] Defs =
        {
            new ClipDef(Player, "Idle", true), new ClipDef(Player, "Walk", true), new ClipDef(Player, "Run", true),
            new ClipDef(Player, "CrouchIdle", true), new ClipDef(Player, "CrouchWalk", true), new ClipDef(Player, "Throw", false),
            new ClipDef(Heavy, "Idle", true), new ClipDef(Heavy, "Walk", true), new ClipDef(Heavy, "Run", true),
            new ClipDef(Heavy, "LookAround", true), new ClipDef(Heavy, "HitFall", false), new ClipDef(Heavy, "Talk", true),
            new ClipDef(Captain, "Idle", true), new ClipDef(Captain, "Walk", true), new ClipDef(Captain, "Run", true),
            new ClipDef(Captain, "LookAround", true), new ClipDef(Captain, "HitFall", false), new ClipDef(Captain, "Talk", true),
        };

        [MenuItem("ShadowProtocol/Setup Animations")]
        public static void Setup()
        {
            ConfigureImporters();
            BuildControllers();
            Debug.Log("[ShadowProtocol] 애니메이션 설정 완료: " + PlayerControllerPath + ", " + GuardControllerPath + ", " + CaptainControllerPath);
        }

        public static bool SourcesPresent()
        {
            foreach (var d in Defs)
                if (AssetImporter.GetAtPath(d.File) == null) return false;
            foreach (var w in new[] { Player, Heavy, Captain })
                if (AssetImporter.GetAtPath(ModelPath(w)) == null) return false;
            return true;
        }

        public static void ConfigureImporters()
        {
            foreach (var d in Defs)
            {
                string p = d.File;
                var imp = AssetImporter.GetAtPath(p) as ModelImporter;
                if (imp == null) { Debug.LogWarning("[ShadowProtocol] 애니메이션 없음: " + p); continue; }
                bool dirty = false;
                if (imp.animationType != ModelImporterAnimationType.Human) { imp.animationType = ModelImporterAnimationType.Human; dirty = true; }
                // 아바타는 클립 파일의 아마추어(23뼈, T포즈)에서 만든다. 모델 아바타 복사는 계층(메시 자식·루트 이름)이 달라 Unity가 거부한다.
                if (imp.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel) { imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel; imp.sourceAvatar = null; dirty = true; }
                // 뼈 대응은 인물 모델과 같게(자동 매핑이 중무장 경비병 클립에서 Hips를 Root에 잘못 붙였다)
                var modelImp = AssetImporter.GetAtPath(ModelPath(d.who)) as ModelImporter;
                if (modelImp != null)
                {
                    var want = modelImp.humanDescription.human;
                    var hd = imp.humanDescription;
                    bool same = hd.human != null && hd.human.Length == want.Length;
                    if (same)
                        for (int i = 0; i < want.Length; i++)
                            if (hd.human[i].humanName != want[i].humanName || hd.human[i].boneName != want[i].boneName) { same = false; break; }
                    if (!same) { hd.human = want; imp.humanDescription = hd; dirty = true; }
                }
                if (imp.materialImportMode != ModelImporterMaterialImportMode.None) { imp.materialImportMode = ModelImporterMaterialImportMode.None; dirty = true; }
                if (imp.importCameras || imp.importLights || imp.importBlendShapes) { imp.importCameras = false; imp.importLights = false; imp.importBlendShapes = false; dirty = true; }
                if (dirty) { imp.SaveAndReimport(); imp = (ModelImporter)AssetImporter.GetAtPath(p); }

                var defs = imp.defaultClipAnimations;
                if (defs == null || defs.Length == 0) continue;
                var c = defs[0];
                var cur = imp.clipAnimations;
                if (cur != null && cur.Length == 1 && cur[0].name == d.name && cur[0].loopTime == d.loop &&
                    Mathf.Approximately(cur[0].rotationOffset, d.rotOffset) &&
                    Mathf.Approximately(cur[0].firstFrame, c.firstFrame) && Mathf.Approximately(cur[0].lastFrame, c.lastFrame) &&
                    cur[0].lockRootPositionXZ && cur[0].lockRootHeightY && cur[0].lockRootRotation)
                    continue;

                c.name = d.name;
                c.loopTime = d.loop;
                c.loopPose = false; // 루프 이음매는 Blender에서 마지막 = 첫 프레임으로 맞춤
                // first/last는 파일 전체(기본값), rotOffset 0
                c.rotationOffset = d.rotOffset;
                // 루트 모션은 쓰지 않는다(In Place): 회전·높이·수평 이동을 모두 포즈에 굽는다.
                c.lockRootRotation = true;
                c.keepOriginalOrientation = true;
                c.lockRootHeightY = true;
                c.keepOriginalPositionY = true;
                c.heightFromFeet = false;
                c.lockRootPositionXZ = true;
                c.keepOriginalPositionXZ = true;
                imp.clipAnimations = new[] { c };
                imp.SaveAndReimport();
            }
        }

        public static AnimationClip Clip(string who, string name)
        {
            foreach (var d in Defs)
            {
                if (d.name != name || d.who != who) continue;
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(d.File))
                    if (o is AnimationClip ac && !ac.name.StartsWith("__preview")) return ac;
            }
            return null;
        }

        static AvatarMask UpperMask()
        {
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(UpperMaskPath);
            if (mask == null)
            {
                mask = new AvatarMask();
                AssetDatabase.CreateAsset(mask, UpperMaskPath);
            }
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
            {
                var part = (AvatarMaskBodyPart)i;
                bool on = part == AvatarMaskBodyPart.Body || part == AvatarMaskBodyPart.Head ||
                          part == AvatarMaskBodyPart.LeftArm || part == AvatarMaskBodyPart.RightArm ||
                          part == AvatarMaskBodyPart.LeftFingers || part == AvatarMaskBodyPart.RightFingers;
                mask.SetHumanoidBodyPartActive(part, on);
            }
            EditorUtility.SetDirty(mask);
            return mask;
        }

        public static void BuildControllers()
        {
            var mask = UpperMask();
            BuildPlayer(mask);
            BuildGuard(mask, Heavy, GuardControllerPath, HeavyWalkNatural, HeavyRunNatural);
            BuildGuard(mask, Captain, CaptainControllerPath, CaptainWalkNatural, CaptainRunNatural);
            AssetDatabase.SaveAssets();
        }

        static AnimatorController NewController(string path)
        {
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) != null) AssetDatabase.DeleteAsset(path);
            return AnimatorController.CreateAnimatorControllerAtPath(path);
        }

        static void BuildPlayer(AvatarMask mask)
        {
            var ctrl = NewController(PlayerControllerPath);
            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("Crouch", AnimatorControllerParameterType.Float);
            ctrl.AddParameter(new AnimatorControllerParameter { name = "MoveScale", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
            ctrl.AddParameter("Throw", AnimatorControllerParameterType.Trigger);

            var state = ctrl.CreateBlendTreeInController("Locomotion", out BlendTree root, 0);
            root.blendType = BlendTreeType.Simple1D;
            root.blendParameter = "Crouch";
            root.useAutomaticThresholds = false;
            var stand = root.CreateBlendTreeChild(0f);
            stand.name = "Stand";
            stand.blendType = BlendTreeType.Simple1D;
            stand.blendParameter = "Speed";
            stand.useAutomaticThresholds = false;
            stand.AddChild(Clip(Player, "Idle"), 0f);
            stand.AddChild(Clip(Player, "Walk"), PlayerWalkNatural);
            stand.AddChild(Clip(Player, "Run"), PlayerRunNatural);
            var crouch = root.CreateBlendTreeChild(1f);
            crouch.name = "Crouch";
            crouch.blendType = BlendTreeType.Simple1D;
            crouch.blendParameter = "Speed";
            crouch.useAutomaticThresholds = false;
            crouch.AddChild(Clip(Player, "CrouchIdle"), 0f);
            crouch.AddChild(Clip(Player, "CrouchWalk"), PlayerCrouchWalkNatural);
            state.speedParameterActive = true;
            state.speedParameter = "MoveScale";

            // 상체 레이어: 던지기(가중치는 스크립트가 던질 때만 올린다)
            ctrl.AddLayer("UpperBody");
            var layers = ctrl.layers;
            layers[1].avatarMask = mask;
            layers[1].defaultWeight = 0f;
            layers[1].blendingMode = AnimatorLayerBlendingMode.Override;
            ctrl.layers = layers;
            var sm = ctrl.layers[1].stateMachine;
            var empty = sm.AddState("Empty");
            empty.writeDefaultValues = false;
            var thr = sm.AddState("Throw");
            thr.motion = Clip(Player, "Throw");
            thr.speed = ThrowSpeed;
            sm.defaultState = empty;
            var t1 = empty.AddTransition(thr);
            t1.AddCondition(AnimatorConditionMode.If, 0f, "Throw");
            t1.hasExitTime = false;
            t1.duration = 0.08f;
            t1.offset = ThrowStartNormalized;
            var any = sm.AddAnyStateTransition(thr);
            any.AddCondition(AnimatorConditionMode.If, 0f, "Throw");
            any.hasExitTime = false;
            any.duration = 0.08f;
            any.offset = ThrowStartNormalized;
            any.canTransitionToSelf = true;
            var t2 = thr.AddTransition(empty);
            t2.hasExitTime = true;
            t2.exitTime = 0.9f;
            t2.duration = 0.2f;
            EditorUtility.SetDirty(ctrl);
        }

        static void BuildGuard(AvatarMask mask, string who, string path, float walk, float run)
        {
            var ctrl = NewController(path);
            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter(new AnimatorControllerParameter { name = "MoveScale", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
            ctrl.AddParameter("Searching", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Down", AnimatorControllerParameterType.Bool);

            var sm = ctrl.layers[0].stateMachine;
            var loco = ctrl.CreateBlendTreeInController("Locomotion", out BlendTree lt, 0);
            lt.blendType = BlendTreeType.Simple1D;
            lt.blendParameter = "Speed";
            lt.useAutomaticThresholds = false;
            lt.AddChild(Clip(who, "Idle"), 0f);
            lt.AddChild(Clip(who, "Walk"), walk);
            lt.AddChild(Clip(who, "Run"), run);
            loco.speedParameterActive = true;
            loco.speedParameter = "MoveScale";

            // 수색: 멈춰 있을 때는 두리번거리기, 움직이면 걷기/달리기
            var search = ctrl.CreateBlendTreeInController("Search", out BlendTree st, 0);
            st.blendType = BlendTreeType.Simple1D;
            st.blendParameter = "Speed";
            st.useAutomaticThresholds = false;
            st.AddChild(Clip(who, "LookAround"), 0f);
            st.AddChild(Clip(who, "Walk"), walk);
            st.AddChild(Clip(who, "Run"), run);
            search.speedParameterActive = true;
            search.speedParameter = "MoveScale";

            var fall = sm.AddState("HitFall");
            fall.motion = Clip(who, "HitFall");
            sm.defaultState = loco;

            var a = loco.AddTransition(search);
            a.AddCondition(AnimatorConditionMode.If, 0f, "Searching");
            a.hasExitTime = false; a.duration = 0.3f;
            var b = search.AddTransition(loco);
            b.AddCondition(AnimatorConditionMode.IfNot, 0f, "Searching");
            b.hasExitTime = false; b.duration = 0.3f;
            var ko = sm.AddAnyStateTransition(fall);
            ko.AddCondition(AnimatorConditionMode.If, 0f, "Down");
            ko.hasExitTime = false; ko.duration = 0.1f; ko.canTransitionToSelf = false;
            var up = fall.AddTransition(loco);
            up.AddCondition(AnimatorConditionMode.IfNot, 0f, "Down");
            up.hasExitTime = false; up.duration = 0.4f;

            // 무전 몸짓(상체). 가중치는 스크립트가 무전할 때만 올린다.
            ctrl.AddLayer("Radio");
            var layers = ctrl.layers;
            layers[1].avatarMask = mask;
            layers[1].defaultWeight = 0f;
            layers[1].blendingMode = AnimatorLayerBlendingMode.Override;
            ctrl.layers = layers;
            var rsm = ctrl.layers[1].stateMachine;
            var talk = rsm.AddState("Talk");
            talk.motion = Clip(who, "Talk");
            rsm.defaultState = talk;
            EditorUtility.SetDirty(ctrl);
        }
    }
}
