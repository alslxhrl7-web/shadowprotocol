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
        // 웅크려 걷기 6차(Mixamo 원본 → Blender 보정, 보폭 0.48m·150걸음/분): Blender 실측 1.20 m/s = 게임 웅크림 속도 1.20.
        public const float PlayerCrouchWalkNatural = 1.20f;
        public const float HeavyWalkNatural = 0.87f, HeavyRunNatural = 4.07f;
        public const float CaptainWalkNatural = 0.76f, CaptainRunNatural = 3.56f;
        public const float GuardWalkNatural = HeavyWalkNatural, GuardRunNatural = HeavyRunNatural;
        /// <summary>던지기: 와인드업 앞부분을 건너뛰고(정규화 시작) 빠르게 재생.</summary>
        public const float ThrowStartNormalized = 0.38f, ThrowSpeed = 1.5f;

        public static float RunNatural(string who) => who == Player ? PlayerRunNatural : who == Captain ? CaptainRunNatural : HeavyRunNatural;
        public static float WalkNatural(string who) => who == Player ? PlayerWalkNatural : who == Captain ? CaptainWalkNatural : HeavyWalkNatural;
        public static string ControllerPath(string who) => who == Player ? PlayerControllerPath : who == Captain ? CaptainControllerPath : GuardControllerPath;

        // 플레이어 웅크린 대기·걷기: Adobe Mixamo "Crouching Idle"·"Crouch Walk Forward" 원본을 Blender에서 보정
        // (원본 휴식 자세의 몸 비틀림 제거, 웅크림 깊이 맞춤, 팔 내림, 발 고정) → female_agent@CrouchIdle/CrouchWalk.fbx. 다른 최종 클립과 같은 방식으로 쓴다.
        // path/heightOffset/Mixamo 분기는 원본 Mixamo FBX를 직접 쓸 때를 위해 남겨 둔다(지금은 쓰지 않음).

        struct ClipDef
        {
            public string who, name; public bool loop; public int first, last; public float rotOffset; public string path;
            /// <summary>발 기준 높이(Feet)에서 더 올리는 값(m). 여성 요원 모델에서 발 기준이면 웅크린 대기 17.5cm·걷기 10cm 바닥에 묻혀(플레이 측정) 올린다.</summary>
            public float heightOffset;
            public ClipDef(string w, string n, bool l, string p = null, float h = 0f) { who = w; name = n; loop = l; first = -1; last = -1; rotOffset = 0f; path = p; heightOffset = h; }
            public string File => path ?? SourceDir + who + "@" + name + ".fbx";
            public bool Mixamo => path != null && path.Contains("/Mixamo/");
        }

        // (first, last, rotOffset) = (-1, -1, 0): 구간·방향이 클립에 이미 구워져 있다(다시 적용하면 이중 적용)
        static readonly ClipDef[] Defs =
        {
            new ClipDef(Player, "Idle", true), new ClipDef(Player, "Walk", true), new ClipDef(Player, "Run", true),
            // 웅크린 대기·걷기: 높이 보정 없음(Humanoid로 돌아왔지만 근육 범위를 넓혀 다리가 잘리지 않으므로 Blender 높이 그대로 — 2026-11-24 측정)
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
                bool generic = PlayerGeneric && d.who == Player;
                // 플레이어: Generic(뼈 회전을 그대로 재생). Humanoid 리타깃에서는 웅크림 몸통이 10° 덜 숙여지고 팔꿈치가 7° 더 펴지며
                // 골반이 1.7~2.8cm 달라져(2026-11-23 측정) Blender 자세를 그대로 쓰기 위해 바꿈. 경비 두 명은 Humanoid 그대로.
                var wantType = generic ? ModelImporterAnimationType.Generic : ModelImporterAnimationType.Human;
                if (imp.animationType != wantType) { imp.animationType = wantType; dirty = true; }
                // 아바타는 클립 파일의 아마추어(23뼈, T포즈)에서 만든다. 모델 아바타 복사는 계층(메시 자식·루트 이름)이 달라 Unity가 거부한다.
                var wantSetup = generic ? ModelImporterAvatarSetup.NoAvatar : ModelImporterAvatarSetup.CreateFromThisModel;
                if (imp.avatarSetup != wantSetup) { imp.avatarSetup = wantSetup; imp.sourceAvatar = null; dirty = true; }
                // 뼈 대응은 인물 모델과 같게(자동 매핑이 중무장 경비병 클립에서 Hips를 Root에 잘못 붙였다)
                // (Mixamo 클립은 mixamorig 뼈 이름이 달라 Unity 자동 매핑을 그대로 쓴다)
                var modelImp = d.Mixamo || generic ? null : AssetImporter.GetAtPath(ModelPath(d.who)) as ModelImporter;
                if (modelImp != null)
                {
                    var want = modelImp.humanDescription.human;
                    var hd = imp.humanDescription;
                    bool same = hd.human != null && hd.human.Length == want.Length;
                    if (same)
                        for (int i = 0; i < want.Length; i++)
                            if (hd.human[i].humanName != want[i].humanName || hd.human[i].boneName != want[i].boneName || !SameLimit(hd.human[i].limit, want[i].limit)) { same = false; break; }
                    if (!same) { hd.human = want; imp.humanDescription = hd; dirty = true; }

                    // 기준 자세(T자세): 클립 FBX의 휴식 자세는 T자세가 아니다(첫 동작 자세 — 팔 46~62°, 머리 56°, 무릎 28° 차이, 2026-11-24 측정).
                    // 클립 아바타가 그것을 T자세로 삼으면 근육 값이 그만큼 어긋나 Humanoid 재생에서 몸통이 덜 숙고 팔꿈치가 펴지고 골반이 4cm 뜬다.
                    // → 모델 아바타의 기준 자세를 뼈 이름으로 옮겨 넣는다. 플레이어(2026-11-24)에 이어 경비 두 명의 Blender 클립에도 적용(2026-11-25):
                    //   경비 클립 12개도 기준 자세가 모델과 위팔 21~70°·손 13~81°·Run 발 134~139° 어긋나 게임 자세가 Blender와 최대 26cm(발)·정강이 34° 달랐다.
                    //   (Mixamo·Generic 클립은 위에서 modelImp가 null이라 여기 오지 않는다)
                    if (d.who == Player || d.who == Heavy || d.who == Captain)
                    {
                        var skel = MatchSkeleton(modelImp.humanDescription.skeleton, imp.humanDescription.skeleton);
                        if (skel != null)
                        {
                            hd = imp.humanDescription;
                            hd.skeleton = skel;
                            imp.humanDescription = hd;
                            dirty = true;
                        }
                    }
                }
                if (imp.materialImportMode != ModelImporterMaterialImportMode.None) { imp.materialImportMode = ModelImporterMaterialImportMode.None; dirty = true; }
                if (imp.importCameras || imp.importLights || imp.importBlendShapes) { imp.importCameras = false; imp.importLights = false; imp.importBlendShapes = false; dirty = true; }
                if (dirty) { imp.SaveAndReimport(); imp = (ModelImporter)AssetImporter.GetAtPath(p); }

                var defs = imp.defaultClipAnimations;
                if (defs == null || defs.Length == 0) continue;
                var c = defs[0];
                var cur = imp.clipAnimations;
                if (cur != null && cur.Length == 1 && cur[0].name == d.name && cur[0].loopTime == d.loop &&
                    cur[0].loopPose == d.Mixamo && cur[0].keepOriginalOrientation == !d.Mixamo &&
                    cur[0].heightFromFeet == d.Mixamo && cur[0].keepOriginalPositionY == !d.Mixamo && Mathf.Approximately(cur[0].heightOffset, d.heightOffset) &&
                    Mathf.Approximately(cur[0].rotationOffset, d.rotOffset) &&
                    Mathf.Approximately(cur[0].firstFrame, c.firstFrame) && Mathf.Approximately(cur[0].lastFrame, c.lastFrame) &&
                    cur[0].lockRootPositionXZ && cur[0].lockRootHeightY && cur[0].lockRootRotation)
                    continue;

                c.name = d.name;
                c.loopTime = d.loop;
                // Blender 클립은 마지막 = 첫 프레임으로 이음매를 맞췄고, Mixamo는 표준대로 Loop Pose를 켠다
                c.loopPose = d.Mixamo;
                // first/last는 파일 전체(기본값), rotOffset 0
                c.rotationOffset = d.rotOffset;
                // 루트 모션은 쓰지 않는다(In Place): 회전·높이·수평 이동을 모두 포즈에 굽는다.
                // 회전 기준: Blender 클립은 Original(정면을 Blender에서 맞춤), Mixamo는 표준대로 Body Orientation(몸 방향 = 정면).
                c.lockRootRotation = true;
                c.keepOriginalOrientation = !d.Mixamo;
                // 높이: Blender 클립은 Original(바닥은 Blender에서 맞춤), Mixamo는 표준대로 발 기준(Feet) — 원본 높이로 두면 웅크린 대기가 11cm 뜬다
                c.lockRootHeightY = true;
                c.keepOriginalPositionY = !d.Mixamo;
                c.heightFromFeet = d.Mixamo;
                c.heightOffset = d.heightOffset;
                c.lockRootPositionXZ = true;
                c.keepOriginalPositionXZ = true;
                imp.clipAnimations = new[] { c };
                imp.SaveAndReimport();
            }
        }

        /// <summary>
        /// 플레이어를 Generic으로 재생(Blender 자세 그대로). false면 Humanoid.
        /// 2026-11-24 사용자 지시로 다시 Humanoid. 예전 Humanoid 차이(몸통 덜 숙임·팔꿈치 펴짐·골반 높음·손이 무릎에 박힘)는
        /// 클립 FBX의 휴식 자세가 T자세가 아니라(첫 동작 자세) 클립 아바타 기준이 어긋났던 것 → ConfigureImporters에서 모델 기준 자세를 클립에 옮겨 넣음(MatchSkeleton). 근육 범위는 원인 아님(넓혀도 결과 같음).
        /// </summary>
        public const bool PlayerGeneric = false;

        /// <summary>
        /// 클립의 기준 자세 목록(clip)을 모델 기준 자세(model)로 바꾼 새 목록. 클립 루트(= 아마추어)는 모델의 아마추어 항목(Hips를 가진 쪽, 루트 바로 아래)과 대응.
        /// 이미 같으면 null.
        /// </summary>
        static SkeletonBone[] MatchSkeleton(SkeletonBone[] model, SkeletonBone[] clip)
        {
            if (model == null || clip == null || model.Length == 0 || clip.Length == 0) return null;
            var byName = new System.Collections.Generic.Dictionary<string, SkeletonBone>();
            foreach (var s in model) byName[s.name] = s;
            // 모델의 아마추어 항목: 이름이 모델 루트에서 "(Clone)"을 뗀 것(female_agent_final)
            string armature = model[0].name.Replace("(Clone)", "");
            var res = (SkeletonBone[])clip.Clone();
            bool changed = false;
            for (int i = 0; i < res.Length; i++)
            {
                SkeletonBone src;
                string key = i == 0 ? armature : res[i].name;
                if (!byName.TryGetValue(key, out src)) continue;
                if (Quaternion.Angle(res[i].rotation, src.rotation) > 0.01f || (res[i].position - src.position).sqrMagnitude > 1e-8f || res[i].scale != src.scale)
                {
                    res[i].rotation = src.rotation;
                    res[i].position = src.position;
                    res[i].scale = src.scale;
                    changed = true;
                }
            }
            return changed ? res : null;
        }

        static bool SameLimit(HumanLimit a, HumanLimit b) =>
            a.useDefaultValues == b.useDefaultValues && (a.useDefaultValues || (a.min == b.min && a.max == b.max && Mathf.Approximately(a.axisLength, b.axisLength)));

        /// <summary>Humanoid 플레이어의 파쿠르 클립(Mixamo 원본 Humanoid FBX, ShadowProtocolMixamoBake가 가져옴).</summary>
        static readonly System.Collections.Generic.Dictionary<string, string> ParkourHumanoidClips = new System.Collections.Generic.Dictionary<string, string>
        {
            { "Vault", Dir + "MixamoParkour/vault_mixamo.fbx" },
            { "Slide", Dir + "MixamoParkour/slide_mixamo.fbx" },
        };

        /// <summary>
        /// 편집 모드 자세 측정: 플레이어 모델(아바타 포함)을 임시로 만들어 클립을 n프레임 샘플링(AnimationMode)하고 PoseProbe로 잰다.
        /// Foot IK·절차적 덧입힘은 들어가지 않는다(순수 리타깃 결과). Blender intended_pose와 비교용.
        /// </summary>
        public static string MeasureClipPose(string clipName, int samples)
        {
            var clip = Clip(Player, clipName);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath(Player));
            if (clip == null || prefab == null) return clipName + ": 클립/모델 없음";
            var go = (GameObject)Object.Instantiate(prefab);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.transform.SetPositionAndRotation(new Vector3(0f, -200f, 0f), Quaternion.identity);
            var an = go.GetComponent<Animator>();
            if (an == null) an = go.AddComponent<Animator>();
            if (PlayerGeneric) an.avatar = null;
            var smr = go.GetComponentInChildren<SkinnedMeshRenderer>();
            var probe = new ShadowProtocolPoseProbe(go.transform, an, smr);
            bool wasMode = AnimationMode.InAnimationMode();
            if (!wasMode) AnimationMode.StartAnimationMode();
            try
            {
                for (int i = 0; i < samples; i++)
                {
                    float t = samples > 1 ? clip.length * i / samples : 0f;
                    AnimationMode.BeginSampling();
                    AnimationMode.SampleAnimationClip(go, clip, t);
                    AnimationMode.EndSampling();
                    probe.Sample(true);
                }
            }
            finally
            {
                if (!wasMode) AnimationMode.StopAnimationMode();
                Object.DestroyImmediate(go);
            }
            return $"{clipName}({(PlayerGeneric ? "Generic" : "Humanoid")}): {probe.Report()}";
        }

        static AnimationClip ParkourClip(string name)
        {
            if (PlayerGeneric) return AssetDatabase.LoadAssetAtPath<AnimationClip>(PlayerGenericDir + name + ".anim");
            if (!ParkourHumanoidClips.TryGetValue(name, out var p)) return null;
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(p))
                if (o is AnimationClip ac && !ac.name.StartsWith("__preview")) return ac;
            return null;
        }
        public const string PlayerGenericDir = Dir + "PlayerGeneric/";
        public const string PlayerUpperMaskPath = Dir + "SP_PlayerUpperBody_Generic.mask";

        static AnimationClip FbxClip(ClipDef d)
        {
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(d.File))
                if (o is AnimationClip ac && !ac.name.StartsWith("__preview")) return ac;
            return null;
        }

        public static AnimationClip Clip(string who, string name)
        {
            foreach (var d in Defs)
            {
                if (d.name != name || d.who != who) continue;
                if (PlayerGeneric && who == Player)
                {
                    var g = AssetDatabase.LoadAssetAtPath<AnimationClip>(PlayerGenericDir + name + ".anim");
                    if (g != null) return g;
                }
                return FbxClip(d);
            }
            return null;
        }

        /// <summary>플레이어 모델에서 아마추어(Root 뼈를 가진 자식) 이름. 클립 FBX는 아마추어가 루트라 경로 앞에 이 이름을 붙여야 모델 루트의 Animator에 맞는다.</summary>
        static string PlayerArmatureName()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath(Player));
            if (model == null) return null;
            foreach (Transform t in model.transform) if (t.Find("Root") != null) return t.name;
            return null;
        }

        /// <summary>
        /// Generic 클립(아마추어 기준 경로)을 플레이어 모델 루트 기준 경로로 옮긴 .anim을 만든다(Animations/PlayerGeneric/).
        /// 커브 값은 그대로 복사하므로 Blender 뼈 회전·골반 위치가 그대로 재생된다.
        /// </summary>
        static void BuildPlayerGenericClips()
        {
            string arm = PlayerArmatureName();
            if (string.IsNullOrEmpty(arm)) { Debug.LogWarning("[ShadowProtocol] 플레이어 아마추어를 찾지 못해 Generic 클립을 만들지 못함"); return; }
            if (!AssetDatabase.IsValidFolder(PlayerGenericDir.TrimEnd('/'))) AssetDatabase.CreateFolder(Dir.TrimEnd('/'), "PlayerGeneric");
            foreach (var d in Defs)
            {
                if (d.who != Player) continue;
                var src = FbxClip(d);
                if (src == null) continue;
                string path = PlayerGenericDir + d.name + ".anim";
                var dst = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (dst == null) { dst = new AnimationClip(); AssetDatabase.CreateAsset(dst, path); }
                dst.ClearCurves();
                dst.frameRate = src.frameRate;
                foreach (var b in AnimationUtility.GetCurveBindings(src))
                {
                    var nb = b;
                    nb.path = string.IsNullOrEmpty(b.path) ? arm : arm + "/" + b.path;
                    AnimationUtility.SetEditorCurve(dst, nb, AnimationUtility.GetEditorCurve(src, b));
                }
                var s = AnimationUtility.GetAnimationClipSettings(src);
                s.loopTime = d.loop;
                s.loopBlend = false;
                AnimationUtility.SetAnimationClipSettings(dst, s);
                EditorUtility.SetDirty(dst);
            }
        }

        /// <summary>Generic 플레이어용 상체 마스크(던지기 레이어): 척추(Spine) 아래 뼈만 켠다.</summary>
        static AvatarMask PlayerUpperMask()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath(Player));
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(PlayerUpperMaskPath);
            if (mask == null) { mask = new AvatarMask(); AssetDatabase.CreateAsset(mask, PlayerUpperMaskPath); }
            var all = model.GetComponentsInChildren<Transform>(true);
            mask.transformCount = all.Length;
            for (int i = 0; i < all.Length; i++)
            {
                string p = AnimationUtility.CalculateTransformPath(all[i], model.transform);
                mask.SetTransformPath(i, p);
                mask.SetTransformActive(i, p.Contains("/Spine"));
            }
            EditorUtility.SetDirty(mask);
            return mask;
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
            if (PlayerGeneric) BuildPlayerGenericClips();
            BuildPlayer(PlayerGeneric ? PlayerUpperMask() : mask);
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
            // Foot IK: 깊은 웅크림은 Humanoid 근육 범위에 걸려 다리 각도가 잘려 발이 바닥에 약 9cm 묻히고 보폭이 커졌다(플레이 측정).
            // 클립에 저장된 발 IK 목표(원래 발 위치)로 발을 되돌린다.
            state.iKOnFeet = !PlayerGeneric; // Generic에서는 쓰지 않음(뼈 회전 그대로라 필요 없음)

            // 파쿠르(Mixamo "Vault Over Box"·"Running Slide" → ShadowProtocolMixamoBake로 구운 Generic 클립).
            // ParkourController가 트리거를 넣고, ParkourSpeed(재생 배율)·시작 지점(offset)을 이동 시간에 맞춘다.
            ctrl.AddParameter(new AnimatorControllerParameter { name = "ParkourSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
            foreach (var pk in new[] { "Vault", "Slide" })
            {
                var pclip = ParkourClip(pk);
                if (pclip == null) continue;
                ctrl.AddParameter(pk, AnimatorControllerParameterType.Trigger);
                var sm0 = ctrl.layers[0].stateMachine;
                var ps = sm0.AddState(pk);
                ps.motion = pclip;
                ps.speedParameterActive = true;
                ps.speedParameter = "ParkourSpeed";
                var inT = sm0.AddAnyStateTransition(ps);
                inT.AddCondition(AnimatorConditionMode.If, 0f, pk);
                inT.hasExitTime = false;
                inT.duration = 0.06f;
                inT.canTransitionToSelf = false;
                inT.offset = pk == "Vault" ? ShadowProtocolParkourTiming.VaultStart : ShadowProtocolParkourTiming.SlideStart;
                var outT = ps.AddTransition(state);
                outT.hasExitTime = true;
                outT.exitTime = pk == "Vault" ? ShadowProtocolParkourTiming.VaultEnd : ShadowProtocolParkourTiming.SlideEnd;
                outT.duration = 0.15f;
            }

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
