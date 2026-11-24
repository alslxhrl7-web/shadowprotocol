using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace ShadowProtocol.EditorTools
{
    /// <summary>
    /// Mixamo 파쿠르 클립(ArtSource/Mixamo/Running Slide.fbx, Vault Over Box.fbx, 2026-11-23 사용자 제공)을
    /// 플레이어(Generic 재생) 뼈대용 Generic 클립으로 굽는다.
    /// 방법: Mixamo FBX를 Humanoid로 가져와 여성 요원 모델의 Humanoid 아바타에 재생(PlayableGraph, 수동 평가) →
    /// 30fps로 모든 뼈의 로컬 회전 + 골반 위치를 읽어 Animations/PlayerGeneric/{Slide,Vault}.anim에 기록.
    /// 앞으로 가는 이동(루트 모션 XZ)은 버리고(이동은 ParkourController가), 높이는 포즈에 남긴다(넘을 때 몸이 뜸).
    /// </summary>
    public static class ShadowProtocolMixamoBake
    {
        const string SrcDir = "ArtSource/Mixamo/";
        const string ImportDir = "Assets/ShadowProtocol/Animations/MixamoParkour";
        public const string OutDir = "Assets/ShadowProtocol/Animations/PlayerGeneric/";
        const float Fps = 30f;

        public static readonly (string file, string name)[] Clips =
        {
            ("Running Slide.fbx", "Slide"),
            ("Vault Over Box.fbx", "Vault"),
        };

        [MenuItem("ShadowProtocol/Bake Mixamo Parkour (Player)")]
        public static void BakeMenu() => Debug.Log(BakeAllWithMotion());

        public static string BakeAll()
        {
            var sb = new StringBuilder();
            foreach (var c in Clips) sb.AppendLine(Bake(c.file, c.name));
            AssetDatabase.SaveAssets();
            return sb.ToString();
        }

        public static bool SourcesPresent() => Clips.All(c => File.Exists(Path.Combine(ProjectRoot, SrcDir + c.file)));

        static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

        static string Bake(string file, string outName)
        {
            string src = Path.Combine(ProjectRoot, SrcDir + file);
            if (!File.Exists(src)) return $"[Mixamo] 없음: {SrcDir}{file}";
            if (!AssetDatabase.IsValidFolder(ImportDir)) AssetDatabase.CreateFolder("Assets/ShadowProtocol/Animations", "MixamoParkour");
            string fbx = $"{ImportDir}/{outName.ToLowerInvariant()}_mixamo.fbx";
            string full = Path.Combine(ProjectRoot, fbx);
            if (!File.Exists(full) || File.GetLastWriteTimeUtc(full) < File.GetLastWriteTimeUtc(src)) File.Copy(src, full, true);
            AssetDatabase.ImportAsset(fbx, ImportAssetOptions.ForceUpdate);

            // Humanoid로(아바타는 이 파일 mixamorig에서). 회전은 몸 방향 기준으로 포즈에 굽고, 높이는 포즈에 남기고, XZ는 루트 모션(재생 시 버려짐)
            var imp = (ModelImporter)AssetImporter.GetAtPath(fbx);
            imp.animationType = ModelImporterAnimationType.Human;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.materialImportMode = ModelImporterMaterialImportMode.None;
            imp.importAnimation = true;
            var defs = imp.defaultClipAnimations;
            foreach (var d in defs)
            {
                d.loopTime = false;
                d.lockRootRotation = true;
                d.keepOriginalOrientation = false;
                d.lockRootHeightY = true;
                d.keepOriginalPositionY = false;
                d.heightFromFeet = true;
                d.lockRootPositionXZ = false;
            }
            imp.clipAnimations = defs;
            imp.SaveAndReimport();

            var clip = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview"));
            if (clip == null) return $"[Mixamo] 클립 없음: {fbx}";

            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ShadowProtocolSceneBuilder.PlayerModelPath);
            var avatar = AssetDatabase.LoadAllAssetsAtPath(ShadowProtocolSceneBuilder.PlayerModelPath).OfType<Avatar>().FirstOrDefault();
            if (modelAsset == null || avatar == null || !avatar.isHuman) return "[Mixamo] 플레이어 모델의 Humanoid 아바타를 찾지 못함";

            var go = (GameObject)Object.Instantiate(modelAsset);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var anim = go.GetComponent<Animator>();
            if (anim == null) anim = go.AddComponent<Animator>();
            anim.avatar = avatar;
            anim.applyRootMotion = false;
            anim.runtimeAnimatorController = null;

            // Animation 창과 같은 방식(AnimationMode)으로 샘플(PlayableGraph 수동 평가는 Humanoid 포즈가 적용되지 않았음)
            AnimationMode.StartAnimationMode();

            var bones = go.GetComponentsInChildren<Transform>(true).Where(t => t != go.transform && !(t.GetComponent<Renderer>() != null && t.childCount == 0)).ToArray();
            Transform hips = anim.GetBoneTransform(HumanBodyBones.Hips);
            int frames = Mathf.Max(2, Mathf.CeilToInt(clip.length * Fps) + 1);
            var rot = new AnimationCurve[bones.Length, 4];
            for (int i = 0; i < bones.Length; i++) for (int k = 0; k < 4; k++) rot[i, k] = new AnimationCurve();
            var hp = new[] { new AnimationCurve(), new AnimationCurve(), new AnimationCurve() };
            var prevQ = new Quaternion[bones.Length];
            float hipsMin = float.MaxValue, hipsMax = float.MinValue; int hipsMaxFrame = 0, hipsMinFrame = 0;
            Vector3 hips0 = Vector3.zero;
            forward = new AnimationCurve();
            hipHeight = new AnimationCurve();
            var table = new StringBuilder();

            for (int f = 0; f < frames; f++)
            {
                float t = Mathf.Min(f / Fps, clip.length);
                AnimationMode.BeginSampling();
                AnimationMode.SampleAnimationClip(go, clip, t);
                AnimationMode.EndSampling();
                for (int i = 0; i < bones.Length; i++)
                {
                    var q = bones[i].localRotation;
                    if (f > 0 && Quaternion.Dot(q, prevQ[i]) < 0f) q = new Quaternion(-q.x, -q.y, -q.z, -q.w); // 부호 연속
                    prevQ[i] = q;
                    rot[i, 0].AddKey(t, q.x); rot[i, 1].AddKey(t, q.y); rot[i, 2].AddKey(t, q.z); rot[i, 3].AddKey(t, q.w);
                }
                if (hips != null)
                {
                    // 앞으로 간 거리(월드 +Z)는 따로 곡선으로 빼고, 골반은 제자리(XZ는 첫 프레임 위치)에 높이만 남긴다
                    Vector3 w = hips.position;
                    if (f == 0) hips0 = w;
                    forward.AddKey(t, w.z - hips0.z);
                    hipHeight.AddKey(t, w.y);
                    Vector3 inPlace = new Vector3(hips0.x + (w.x - hips0.x) * 0.3f, w.y, hips0.z); // 옆 흔들림은 30%만 남김
                    Vector3 lp = hips.parent != null ? hips.parent.InverseTransformPoint(inPlace) : inPlace;
                    hp[0].AddKey(t, lp.x); hp[1].AddKey(t, lp.y); hp[2].AddKey(t, lp.z);
                    if (w.y > hipsMax) { hipsMax = w.y; hipsMaxFrame = f; }
                    if (w.y < hipsMin) { hipsMin = w.y; hipsMinFrame = f; }
                    if (f % 3 == 0) table.Append($"{t:0.0}s 높이 {w.y:0.00} 앞 {w.z - hips0.z:0.00} | ");
                }
            }
            AnimationMode.StopAnimationMode();
            lastTable = table.ToString();

            string outPath = OutDir + outName + ".anim";
            var dst = AssetDatabase.LoadAssetAtPath<AnimationClip>(outPath);
            if (dst == null) { dst = new AnimationClip(); AssetDatabase.CreateAsset(dst, outPath); }
            dst.ClearCurves();
            dst.frameRate = Fps;
            string[] rp = { "m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z", "m_LocalRotation.w" };
            for (int i = 0; i < bones.Length; i++)
            {
                string path = AnimationUtility.CalculateTransformPath(bones[i], go.transform);
                for (int k = 0; k < 4; k++)
                    AnimationUtility.SetEditorCurve(dst, EditorCurveBinding.FloatCurve(path, typeof(Transform), rp[k]), rot[i, k]);
            }
            if (hips != null)
            {
                string hpath = AnimationUtility.CalculateTransformPath(hips, go.transform);
                string[] pp = { "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z" };
                for (int k = 0; k < 3; k++) AnimationUtility.SetEditorCurve(dst, EditorCurveBinding.FloatCurve(hpath, typeof(Transform), pp[k]), hp[k]);
            }
            dst.EnsureQuaternionContinuity();
            var s = AnimationUtility.GetAnimationClipSettings(dst);
            s.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(dst, s);
            EditorUtility.SetDirty(dst);
            Object.DestroyImmediate(go);

            length = clip.length;
            apex = hipsMaxFrame / Fps;
            return $"[Mixamo] {outName}: 원본 {clip.length:0.00}s → {outPath} ({frames}프레임, 뼈 {bones.Length}) 골반 높이 최소 {hipsMin:0.00}m@{hipsMinFrame / Fps:0.00}s 최대 {hipsMax:0.00}m@{apex:0.00}s, 앞으로 총 {forward.Evaluate(clip.length):0.00}m\n  {lastTable}";
        }

        static AnimationCurve forward, hipHeight;
        static float length, apex;
        static string lastTable;
        public const string MotionDataPath = OutDir + "ParkourMotion.asset";

        /// <summary>두 클립을 굽고 이동 곡선을 ParkourMotion.asset에 저장(구간은 기존 값 유지, 처음이면 기본값)</summary>
        public static string BakeAllWithMotion()
        {
            var data = AssetDatabase.LoadAssetAtPath<ParkourMotionData>(MotionDataPath);
            bool fresh = data == null;
            if (fresh) { data = ScriptableObject.CreateInstance<ParkourMotionData>(); AssetDatabase.CreateAsset(data, MotionDataPath); }
            var sb = new StringBuilder();
            sb.AppendLine(Bake(Clips[0].file, Clips[0].name));
            data.slideLength = length; data.slideForward = forward; data.slideHipHeight = hipHeight;
            sb.AppendLine(Bake(Clips[1].file, Clips[1].name));
            data.vaultLength = length; data.vaultForward = forward; data.vaultHipHeight = hipHeight; data.vaultApex = apex;
            if (fresh) { data.vaultStart = 0f; data.vaultEnd = data.vaultLength; data.slideStart = 0f; data.slideEnd = data.slideLength; }
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            return sb.ToString();
        }
    }
}
