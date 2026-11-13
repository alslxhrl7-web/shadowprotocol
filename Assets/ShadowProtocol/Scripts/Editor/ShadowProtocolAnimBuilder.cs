using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ShadowProtocol.EditorTools
{
    /// <summary>
    /// VARCO Animate 애니메이션 FBX(Assets/ShadowProtocol/Animations/Source)를 Humanoid 클립으로 설정하고
    /// 플레이어용·경비용 Animator Controller를 만든다. 메뉴 ShadowProtocol/Setup Animations, 씬 빌드에서도 호출.
    /// </summary>
    public static class ShadowProtocolAnimBuilder
    {
        public const string SourceDir = "Assets/ShadowProtocol/Animations/Source/";
        public const string Dir = "Assets/ShadowProtocol/Animations/";
        public const string PlayerControllerPath = Dir + "SP_PlayerAnimator.controller";
        public const string GuardControllerPath = Dir + "SP_GuardAnimator.controller";
        public const string UpperMaskPath = Dir + "SP_UpperBody.mask";

        // 클립 고유 이동 속도(m/s). 제자리 클립의 디딤발이 뒤로 미끄러지는 속도를 표본 추출해 잰 값(플레이어 체형 기준).
        // 경비 체형은 약 0.93배.
        public const float PlayerWalkNatural = 1.0f, PlayerRunNatural = 3.2f, PlayerCrouchWalkNatural = 1.0f;
        public const float GuardWalkNatural = 0.93f, GuardRunNatural = 3.0f;
        /// <summary>던지기: 와인드업 앞부분을 건너뛰고(정규화 시작) 빠르게 재생. 손이 앞으로 나가는 순간(원본 2.2s)</summary>
        public const float ThrowStartNormalized = 0.38f, ThrowSpeed = 1.5f;

        struct ClipDef
        {
            public string file, name; public bool loop; public int first, last; public float rotOffset;
            public ClipDef(string f, string n, bool l, int a, int b, float r) { file = f; name = n; loop = l; first = a; last = b; rotOffset = r; }
        }

        // first/last: 한 걸음 주기(왼발이 가장 앞에 닿는 프레임부터 다음 같은 순간까지)만 잘라 블렌드 시 발 위상이 맞게 한다.
        // rotOffset: 클립마다 몸이 바라보는 방향이 달라(달리기는 +X) 정면(+Z)으로 맞춘다.
        static readonly ClipDef[] Defs =
        {
            new ClipDef("anim_idle_looping", "Idle", true, -1, -1, 0f),
            new ClipDef("anim_walk_looping", "Walk", true, 31, 65, 0f),
            new ClipDef("anim_run", "Run", true, 7, 46, 88f),
            new ClipDef("anim_crouch_walk_looping", "CrouchWalk", true, 100, 139, 0f),
            new ClipDef("anim_crouch_idle_looping", "CrouchIdle", true, -1, -1, 17f),
            new ClipDef("anim_walk_look_around", "LookAround", true, -1, -1, 25f),
            new ClipDef("anim_throw", "Throw", false, -1, -1, 0f),
            new ClipDef("anim_hit_fall", "HitFall", false, -1, -1, 58f),
            new ClipDef("anim_talk_looping", "Talk", true, -1, -1, 0f),
        };

        [MenuItem("ShadowProtocol/Setup Animations")]
        public static void Setup()
        {
            ConfigureImporters();
            BuildControllers();
            Debug.Log("[ShadowProtocol] 애니메이션 설정 완료: " + PlayerControllerPath + ", " + GuardControllerPath);
        }

        public static bool SourcesPresent()
        {
            foreach (var d in Defs)
                if (AssetImporter.GetAtPath(SourceDir + d.file + ".fbx") == null) return false;
            return true;
        }

        public static void ConfigureImporters()
        {
            foreach (var d in Defs)
            {
                string p = SourceDir + d.file + ".fbx";
                var imp = AssetImporter.GetAtPath(p) as ModelImporter;
                if (imp == null) { Debug.LogWarning("[ShadowProtocol] 애니메이션 없음: " + p); continue; }
                bool dirty = false;
                if (imp.animationType != ModelImporterAnimationType.Human) { imp.animationType = ModelImporterAnimationType.Human; dirty = true; }
                if (imp.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel) { imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel; dirty = true; }
                if (imp.materialImportMode != ModelImporterMaterialImportMode.None) { imp.materialImportMode = ModelImporterMaterialImportMode.None; dirty = true; }
                if (imp.importCameras || imp.importLights || imp.importBlendShapes) { imp.importCameras = false; imp.importLights = false; imp.importBlendShapes = false; dirty = true; }
                if (dirty) { imp.SaveAndReimport(); imp = (ModelImporter)AssetImporter.GetAtPath(p); }

                var defs = imp.defaultClipAnimations;
                if (defs == null || defs.Length == 0) continue;
                var c = defs[0];
                var cur = imp.clipAnimations;
                if (cur != null && cur.Length == 1 && cur[0].name == d.name && cur[0].loopTime == d.loop &&
                    Mathf.Approximately(cur[0].rotationOffset, d.rotOffset) &&
                    (d.first < 0 || (Mathf.Approximately(cur[0].firstFrame, d.first) && Mathf.Approximately(cur[0].lastFrame, d.last))) &&
                    cur[0].lockRootPositionXZ && cur[0].lockRootHeightY && cur[0].lockRootRotation)
                    continue;

                c.name = d.name;
                c.loopTime = d.loop;
                c.loopPose = d.loop;
                if (d.first >= 0) { c.firstFrame = d.first; c.lastFrame = d.last; }
                // 루트 모션은 쓰지 않는다(In Place): 회전·높이·수평 이동을 모두 포즈에 굽는다.
                c.lockRootRotation = true;
                c.keepOriginalOrientation = true;
                c.rotationOffset = d.rotOffset;
                c.lockRootHeightY = true;
                c.keepOriginalPositionY = true;
                c.heightFromFeet = false;
                c.lockRootPositionXZ = true;
                c.keepOriginalPositionXZ = true;
                imp.clipAnimations = new[] { c };
                imp.SaveAndReimport();
            }
        }

        public static AnimationClip Clip(string name)
        {
            foreach (var d in Defs)
            {
                if (d.name != name) continue;
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(SourceDir + d.file + ".fbx"))
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
            BuildGuard(mask);
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
            stand.AddChild(Clip("Idle"), 0f);
            stand.AddChild(Clip("Walk"), PlayerWalkNatural);
            stand.AddChild(Clip("Run"), PlayerRunNatural);
            var crouch = root.CreateBlendTreeChild(1f);
            crouch.name = "Crouch";
            crouch.blendType = BlendTreeType.Simple1D;
            crouch.blendParameter = "Speed";
            crouch.useAutomaticThresholds = false;
            crouch.AddChild(Clip("CrouchIdle"), 0f);
            crouch.AddChild(Clip("CrouchWalk"), PlayerCrouchWalkNatural);
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
            thr.motion = Clip("Throw");
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

        static void BuildGuard(AvatarMask mask)
        {
            var ctrl = NewController(GuardControllerPath);
            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter(new AnimatorControllerParameter { name = "MoveScale", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
            ctrl.AddParameter("Searching", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Down", AnimatorControllerParameterType.Bool);

            var sm = ctrl.layers[0].stateMachine;
            var loco = ctrl.CreateBlendTreeInController("Locomotion", out BlendTree lt, 0);
            lt.blendType = BlendTreeType.Simple1D;
            lt.blendParameter = "Speed";
            lt.useAutomaticThresholds = false;
            lt.AddChild(Clip("Idle"), 0f);
            lt.AddChild(Clip("Walk"), GuardWalkNatural);
            lt.AddChild(Clip("Run"), GuardRunNatural);
            loco.speedParameterActive = true;
            loco.speedParameter = "MoveScale";

            // 수색: 멈춰 있을 때는 두리번거리기, 움직이면 걷기/달리기
            var search = ctrl.CreateBlendTreeInController("Search", out BlendTree st, 0);
            st.blendType = BlendTreeType.Simple1D;
            st.blendParameter = "Speed";
            st.useAutomaticThresholds = false;
            st.AddChild(Clip("LookAround"), 0f);
            st.AddChild(Clip("Walk"), GuardWalkNatural);
            st.AddChild(Clip("Run"), GuardRunNatural);
            search.speedParameterActive = true;
            search.speedParameter = "MoveScale";

            var fall = sm.AddState("HitFall");
            fall.motion = Clip("HitFall");
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
            talk.motion = Clip("Talk");
            rsm.defaultState = talk;
            EditorUtility.SetDirty(ctrl);
        }
    }
}
