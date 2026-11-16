using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadowProtocol.EditorTools
{
    /// <summary>
    /// Blender 환경 최종본 가져오기(claude-hand manifest 기준).
    /// ArtSource/Environment/05_final_export/*.claudehand.json(하위 _superseded 제외)을 읽어
    /// FBX를 Models/EnvFinal/&lt;name&gt;.fbx 로, 텍스처를 Models/EnvFinal/Textures/ 로 복사하고
    /// FBX 재질을 Built-in Standard 재질(BaseColor/Normal/MetallicSmoothness/Occlusion)로 remap 한다.
    /// 데칼(decal)은 Cutout, 렌즈(lens)는 Emission을 켠다. 스케일 1(1 unit = 1 m), 노멀은 파일 값.
    /// </summary>
    public static class ShadowProtocolEnvImport
    {
        public const string SourceDir = "ArtSource/Environment/05_final_export";
        public const string DestDir = "Assets/ShadowProtocol/Models/EnvFinal";
        public const string ContainerSourceDir = "ArtSource/Container/05_final_export";
        public const string ContainerDestDir = "Assets/ShadowProtocol/Models/ContainerFinal";
        static string TexDir = DestDir + "/Textures";
        static string MatDir = DestDir + "/Materials";

        [System.Serializable]
        class Manifest { public string format; public string model_file; public string name; public string transfer_id; }

        [MenuItem("ShadowProtocol/Import Env Final (Blender)")]
        public static void Import() => ImportFrom(SourceDir, DestDir);

        /// <summary>컨테이너 최종본(빈 객체 container_final + 자식 _LOD0/1/2 → Unity가 LODGroup을 만든다)</summary>
        [MenuItem("ShadowProtocol/Import Container Final (Blender)")]
        public static void ImportContainer() => ImportFrom(ContainerSourceDir, ContainerDestDir);

        public const string CharacterSourceDir = "ArtSource/Characters/05_final_export";
        public const string CharacterDestDir = "Assets/ShadowProtocol/Models/CharactersFinal";
        public const string CharacterAnimDestDir = "Assets/ShadowProtocol/Animations/Final";

        /// <summary>인물 최종본 3명(Humanoid, 23뼈) + 애니메이션 18개(anims/&lt;인물&gt;@&lt;클립&gt;.fbx, 아마추어만). 클립 설정·컨트롤러는 ShadowProtocolAnimBuilder.</summary>
        [MenuItem("ShadowProtocol/Import Characters Final (Blender)")]
        public static void ImportCharacters()
        {
            ImportFrom(CharacterSourceDir, CharacterDestDir);
            var report = new System.Text.StringBuilder();
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { CharacterDestDir }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                var imp = AssetImporter.GetAtPath(p) as ModelImporter;
                if (imp == null) continue;
                imp.isReadable = false;
                imp.animationType = ModelImporterAnimationType.Human;
                imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                imp.importAnimation = false;
                imp.importBlendShapes = false;
                imp.SaveAndReimport();
                var avatar = AvatarOf(p);
                report.AppendLine($"  {Path.GetFileName(p)}: 아바타 {(avatar != null && avatar.isValid && avatar.isHuman ? "정상(Humanoid)" : "오류")}");
            }
            // 애니메이션: 복사만(Humanoid 설정·아바타 복사는 ShadowProtocolAnimBuilder.ConfigureImporters)
            string root = Directory.GetParent(Application.dataPath).FullName;
            string animSrc = Path.Combine(root, CharacterSourceDir, "anims");
            Directory.CreateDirectory(Path.Combine(root, CharacterAnimDestDir));
            int n = 0;
            foreach (var f in Directory.GetFiles(animSrc, "*.fbx", SearchOption.TopDirectoryOnly))
            {
                File.Copy(f, Path.Combine(root, CharacterAnimDestDir, Path.GetFileName(f)), true);
                n++;
            }
            AssetDatabase.Refresh();
            Debug.Log($"[ShadowProtocol] 인물 최종본 아바타 확인, 애니메이션 {n}개 복사\n{report}");
        }

        public static Avatar AvatarOf(string modelPath)
        {
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(modelPath)) if (o is Avatar a) return a;
            return null;
        }

        static void ImportFrom(string sourceDir, string destDir)
        {
            TexDir = destDir + "/Textures";
            MatDir = destDir + "/Materials";
            string root = Directory.GetParent(Application.dataPath).FullName;
            string src = Path.Combine(root, sourceDir);
            if (!Directory.Exists(src)) { Debug.LogError("[ShadowProtocol] 최종본 폴더 없음: " + src); return; }
            Directory.CreateDirectory(Path.Combine(root, TexDir));
            Directory.CreateDirectory(Path.Combine(root, MatDir));

            // 1) 복사(manifest가 가리키는 FBX만, _superseded 하위 폴더는 보지 않는다)
            var models = new List<string>();
            foreach (var mf in Directory.GetFiles(src, "*.claudehand.json", SearchOption.TopDirectoryOnly))
            {
                var m = JsonUtility.FromJson<Manifest>(File.ReadAllText(mf));
                if (m == null || string.IsNullOrEmpty(m.model_file) || string.IsNullOrEmpty(m.name)) continue;
                string from = Path.Combine(src, m.model_file);
                if (!File.Exists(from)) { Debug.LogWarning("[ShadowProtocol] FBX 없음: " + from); continue; }
                string to = destDir + "/" + m.name + ".fbx";
                File.Copy(from, Path.Combine(root, to), true);
                models.Add(to);
            }
            foreach (var png in Directory.GetFiles(src, "*.png", SearchOption.TopDirectoryOnly))
                File.Copy(png, Path.Combine(root, TexDir, Path.GetFileName(png)), true);
            AssetDatabase.Refresh();

            // 2) 텍스처 설정
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { TexDir }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                var ti = AssetImporter.GetAtPath(p) as TextureImporter;
                if (ti == null) continue;
                string n = Path.GetFileNameWithoutExtension(p);
                bool changed = false;
                if (n.EndsWith("_Normal") && ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; changed = true; }
                bool linear = n.EndsWith("_MetallicSmoothness") || n.EndsWith("_Occlusion");
                if (linear && ti.sRGBTexture) { ti.sRGBTexture = false; changed = true; }
                if (n.EndsWith("_BaseColor") && n.Contains("decal") && !ti.alphaIsTransparency) { ti.alphaIsTransparency = true; changed = true; }
                if (ti.maxTextureSize < 2048) { ti.maxTextureSize = 2048; changed = true; }
                if (changed) ti.SaveAndReimport();
            }

            // 3) 모델 설정 + 재질 remap
            var report = new System.Text.StringBuilder();
            foreach (var path in models)
            {
                var imp = AssetImporter.GetAtPath(path) as ModelImporter;
                if (imp == null) continue;
                imp.globalScale = 1f;
                imp.useFileScale = true;
                imp.importNormals = ModelImporterNormals.Import;
                imp.importTangents = ModelImporterTangents.CalculateMikk;
                imp.importAnimation = false;
                imp.importCameras = false;
                imp.importLights = false;
                imp.isReadable = true; // 씬 생성 때 칸별로 메시를 합친다(ShadowProtocolSceneBuilder.MergeUnder)
                imp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                imp.materialLocation = ModelImporterMaterialLocation.InPrefab;
                imp.SaveAndReimport();

                var names = new List<string>();
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (o is Material mat && !names.Contains(mat.name)) names.Add(mat.name);
                foreach (var n in names)
                {
                    var m = BuildMaterial(n);
                    if (m != null) imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), n), m);
                }
                imp.SaveAndReimport();

                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var b = new Bounds();
                bool first = true;
                int tris = 0;
                foreach (var mfl in go.GetComponentsInChildren<MeshFilter>())
                {
                    if (mfl.sharedMesh == null) continue;
                    tris += mfl.sharedMesh.triangles.Length / 3;
                    var bb = TransformBounds(mfl.transform, mfl.sharedMesh.bounds, go.transform);
                    if (first) { b = bb; first = false; } else b.Encapsulate(bb);
                }
                report.AppendLine($"  {Path.GetFileName(path)}: 재질 [{string.Join(", ", names)}] 크기 {b.size.x:0.000}×{b.size.y:0.000}×{b.size.z:0.000} 중심 {b.center.x:0.000},{b.center.y:0.000},{b.center.z:0.000} 삼각형 {tris}");
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[ShadowProtocol] Blender 최종본 가져오기({destDir}) {models.Count}개\n{report}");
        }

        static Bounds TransformBounds(Transform t, Bounds local, Transform root)
        {
            var m = root.worldToLocalMatrix * t.localToWorldMatrix;
            var c = local.center; var e = local.extents;
            var b = new Bounds(m.MultiplyPoint3x4(c), Vector3.zero);
            for (int i = 0; i < 8; i++)
                b.Encapsulate(m.MultiplyPoint3x4(c + Vector3.Scale(e, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
            return b;
        }

        static Texture2D Tex(string baseName, string kind) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/{baseName}_{kind}.png");

        /// <summary>Blender 재질 이름(M_env_galv 등) → 텍스처 묶음 이름(env_mat_galv 또는 env_floor)</summary>
        static string TextureBase(string matName)
        {
            string n = matName;
            if (n.StartsWith("M_")) n = n.Substring(2);
            int dot = n.IndexOf('.');
            if (dot > 0) n = n.Substring(0, dot);
            // env_galv → env_mat_galv, container_wall → container_mat_wall(공용 재질), 없으면 env_floor 같은 자산 고유 이름
            int us = n.IndexOf('_');
            if (us > 0)
            {
                string shared = n.Substring(0, us) + "_mat_" + n.Substring(us + 1);
                if (Tex(shared, "BaseColor") != null) return shared;
            }
            if (Tex(n, "BaseColor") != null) return n;
            return null;
        }

        static Material BuildMaterial(string matName)
        {
            string tb = TextureBase(matName);
            if (tb == null) { Debug.LogWarning("[ShadowProtocol] 텍스처를 찾지 못한 재질: " + matName); return null; }
            string path = $"{MatDir}/{tb}.mat";
            var shader = Shader.Find("Standard");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
            m.shader = shader;
            m.enableInstancing = true;
            m.color = Color.white;
            m.SetTexture("_MainTex", Tex(tb, "BaseColor"));
            var nrm = Tex(tb, "Normal");
            m.SetTexture("_BumpMap", nrm);
            m.SetFloat("_BumpScale", 1f);
            if (nrm != null) m.EnableKeyword("_NORMALMAP"); else m.DisableKeyword("_NORMALMAP");
            var ms = Tex(tb, "MetallicSmoothness");
            m.SetTexture("_MetallicGlossMap", ms);
            m.SetFloat("_SmoothnessTextureChannel", 0f); // 금속 맵 알파 = 매끈함
            m.SetFloat("_GlossMapScale", 1f);
            if (ms != null) m.EnableKeyword("_METALLICGLOSSMAP"); else m.DisableKeyword("_METALLICGLOSSMAP");
            m.SetTexture("_OcclusionMap", Tex(tb, "Occlusion"));
            m.SetFloat("_OcclusionStrength", 1f);

            m.SetInt("_SrcBlend", (int)BlendMode.One);
            m.SetInt("_DstBlend", (int)BlendMode.Zero);
            m.SetInt("_ZWrite", 1);
            m.DisableKeyword("_ALPHABLEND_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            if (tb.Contains("decal"))
            {
                // 안전선 데칼: BaseColor 알파(도료 덮임)로 Cutout
                m.SetFloat("_Mode", 1f);
                m.SetOverrideTag("RenderType", "TransparentCutout");
                m.SetFloat("_Cutoff", 0.5f);
                m.EnableKeyword("_ALPHATEST_ON");
                m.renderQueue = (int)RenderQueue.AlphaTest;
            }
            else
            {
                m.SetFloat("_Mode", 0f);
                m.SetOverrideTag("RenderType", "");
                m.DisableKeyword("_ALPHATEST_ON");
                m.renderQueue = -1;
            }

            if (tb.Contains("lens"))
            {
                // 가로등·투광등 렌즈: 발광
                m.EnableKeyword("_EMISSION");
                m.SetTexture("_EmissionMap", Tex(tb, "BaseColor"));
                m.SetColor("_EmissionColor", new Color(1f, 0.95f, 0.85f) * 2.2f);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                m.DisableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", Color.black);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
