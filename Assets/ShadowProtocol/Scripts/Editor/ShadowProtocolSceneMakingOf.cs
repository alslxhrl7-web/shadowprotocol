using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ShadowProtocol.EditorTools
{
    /// <summary>
    /// C7 제작 공정 갤러리: 공정 이미지(VARCO 생성 이미지, Blender 정리 전후 렌더)를 Assets/ShadowProtocol/MakingOf/에 Sprite로 두고
    /// 모델별 카드(공정 이미지 + 실제 게임 모델 턴테이블)를 만든다.
    /// 원본 이미지는 작업 폴더(scratchpad)에 있을 때만 복사한다(이미 복사돼 있으면 그대로 사용).
    /// </summary>
    public static partial class ShadowProtocolSceneBuilder
    {
        const string MakingOfDir = "Assets/ShadowProtocol/MakingOf";
        const string ScratchRoot = @"C:\Users\mbc\AppData\Local\Temp\claude\85d2457cc802f9ea\2508db9e-117f-4192-96e8-8f9d9491a4f5\scratchpad";

        static readonly (string id, string num, string title)[] SP2Cards =
        {
            ("container", "07", "컨테이너"), ("heavyguard", "11", "중무장 경비병"), ("supervisor", "12", "감독관"),
            ("forklift", "01", "지게차"), ("camera", "02", "감시 카메라"), ("drums", "03", "드럼통"), ("pallets", "04", "팔레트"),
            ("desk", "05", "책상"), ("lockers", "06", "사물함"), ("server", "08", "서버 랙"), ("tarp", "09", "방수포 화물"), ("laser", "10", "레이저 장치")
        };

        static Sprite ImportSprite(string src, string dst)
        {
            if (!File.Exists(dst) && File.Exists(src))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(dst));
                File.Copy(src, dst);
                AssetDatabase.ImportAsset(dst);
            }
            var imp = AssetImporter.GetAtPath(dst) as TextureImporter;
            if (imp == null) return null;
            if (imp.textureType != TextureImporterType.Sprite || imp.maxTextureSize != 1024)
            {
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.mipmapEnabled = false;
                imp.maxTextureSize = 1024;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(dst);
        }

        static MakingOfGallery BuildMakingOf()
        {
            EnsureFolder(MakingOfDir);
            var go = new GameObject("MakingOf");
            var gal = go.AddComponent<MakingOfGallery>();
            gal.clayMat = Mat("TurntableClay", new Color(0.62f, 0.63f, 0.65f));
            var guardCtrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ShadowProtocolAnimBuilder.GuardControllerPath);
            var playerCtrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ShadowProtocolAnimBuilder.PlayerControllerPath);
            var list = new List<MakingOfGallery.Entry>();
            const string flow = "공정: VARCO 프롬프트 → VARCO 생성 이미지 → VARCO 원본 메시 → Blender 정리 → 게임 안.  (프롬프트·원본 메시 렌더는 자료를 받는 대로 추가)";

            MakingOfGallery.Entry Card(string id, string num, string title)
            {
                string dir = MakingOfDir + "/" + id;
                var stages = new List<MakingOfGallery.Stage>();
                var s1 = ImportSprite(Path.Combine(ScratchRoot, "varco_sp2_img", $"{num}_{id}.png"), $"{dir}/{num}_{id}_varco_image.png");
                var s2 = ImportSprite(Path.Combine(ScratchRoot, "sp2_final_check", $"{num}_{id}_matcap_side.png"), $"{dir}/{num}_{id}_blender_shape.png");
                var s3 = ImportSprite(Path.Combine(ScratchRoot, "sp2_final_check", $"{num}_{id}_textured.png"), $"{dir}/{num}_{id}_blender_textured.png");
                if (s1 != null) stages.Add(new MakingOfGallery.Stage { caption = "VARCO 3D 생성 이미지", image = s1 });
                if (s2 != null) stages.Add(new MakingOfGallery.Stage { caption = "Blender 정리 후 — 형태(매트캡)", image = s2 });
                if (s3 != null) stages.Add(new MakingOfGallery.Stage { caption = "Blender 정리 후 — 텍스처", image = s3 });
                bool character = id == "heavyguard" || id == "supervisor";
                return new MakingOfGallery.Entry
                {
                    title = title,
                    note = (character ? "VARCO 3D 생성 → Blender 정리·리깅 → VARCO Animate 애니메이션(Humanoid 리타겟)\n" : "VARCO 3D 생성 → Blender 정리(축·스케일·재질·LOD)\n") + flow,
                    stages = stages.ToArray(),
                    model = LoadSP2(id),
                    controller = character ? guardCtrl : null,
                    yaw = 30f,
                    pending = id == "container" && LoadSP2("container").name != "SP2_container_v2"
                };
            }

            list.Add(LoadSP2("container") != null && LoadSP2("container").name == "SP2_container_v2" ? ContainerV2Card() : Card("container", "07", "컨테이너"));
            list.Add(new MakingOfGallery.Entry
            {
                title = "플레이어(잠입 요원)", note = "VARCO 3D 생성 → Blender 정리·리깅 → VARCO Animate 애니메이션 9종(Humanoid 리타겟)\n공정 이미지는 자료를 받는 대로 추가. 지금은 게임 모델 턴테이블만.",
                stages = new MakingOfGallery.Stage[0], model = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerModelPath), controller = playerCtrl, yaw = 30f
            });
            list.Add(new MakingOfGallery.Entry
            {
                title = "경비병", note = "VARCO 3D 생성 → Blender 정리·리깅 → VARCO Animate 애니메이션(Humanoid 리타겟)\n공정 이미지는 자료를 받는 대로 추가. 지금은 게임 모델 턴테이블만.",
                stages = new MakingOfGallery.Stage[0], model = AssetDatabase.LoadAssetAtPath<GameObject>(GuardModelPath), controller = guardCtrl, yaw = 30f
            });
            foreach (var c in SP2Cards) if (c.id != "container") list.Add(Card(c.id, c.num, c.title));
            foreach (var p in new[] { ("Prop_Shelf", "선반"), ("Prop_Crate", "나무 상자"), ("Prop_Bins", "수납함"), ("Prop_DataCore", "데이터 코어 단말기"), ("Prop_Wall", "벽 패널") })
                list.Add(new MakingOfGallery.Entry
                {
                    title = p.Item2, note = "VARCO 3D 생성 → Blender 정리\n공정 이미지는 자료를 받는 대로 추가. 지금은 게임 모델 턴테이블만.",
                    stages = new MakingOfGallery.Stage[0], model = LoadProp(p.Item1), yaw = 30f
                });
            gal.entries = list.ToArray();
            return gal;
        }

        const string ContainerArt = @"C:\Users\mbc\Documents\user\ShadowProtocol\ArtSource\Container";

        /// <summary>컨테이너 v2 카드: [프롬프트 → VARCO 이미지 → VARCO 원본 → Blender 정리 → 게임]</summary>
        static MakingOfGallery.Entry ContainerV2Card()
        {
            string dir = MakingOfDir + "/container_v2";
            var stages = new List<MakingOfGallery.Stage>();
            void Img(string sub, string file, string caption)
            {
                var s = ImportSprite(Path.Combine(ContainerArt, sub, file), $"{dir}/{file}");
                if (s != null) stages.Add(new MakingOfGallery.Stage { caption = caption, image = s });
            }
            string promptPath = Path.Combine(ContainerArt, "01_varco_source", "varco_prompt.txt");
            if (File.Exists(promptPath))
            {
                var lines = File.ReadAllLines(promptPath);
                string head = lines.Length > 0 ? lines[0] : "";
                string body = lines.Length > 1 ? string.Join("\n", lines, 1, lines.Length - 1).Trim() : "";
                stages.Add(new MakingOfGallery.Stage { caption = "VARCO 프롬프트", text = $"<b><color=#59D9FF>{head}</color></b>\n\n<i>{body}</i>" });
            }
            Img("01_varco_source", "varco_generated_image.png", "VARCO 생성 이미지(GenerateImage)");
            Img("03_renders", "varco_raw_matcap.png", "VARCO 3D 원본 메시(Generate3D) — 매트캡");
            Img("03_renders", "refined_matcap.png", "Blender 정리 — 형태 다듬기(매트캡)");
            Img("03_renders", "wireframe_lod.png", "Blender 정리 — LOD0/1/2 와이어프레임");
            Img("03_renders", "texture_maps_sheet.png", "Blender 정리 — PBR 텍스처 맵(BaseColor·Normal·MetallicSmoothness·Occlusion)");
            Img("03_renders", "before_after.png", "VARCO 원본 → Blender 정리 전후 비교");
            Img("03_renders", "final_beauty_front.png", "Blender 최종 렌더(정면)");
            for (int i = 1; i <= 8; i++) Img("03_renders", $"turntable_{i:00}.png", $"Blender 턴테이블 {i}/8");
            return new MakingOfGallery.Entry
            {
                title = "컨테이너 v2",
                note = "VARCO 프롬프트 → VARCO 생성 이미지 → VARCO 3D 원본 → Blender 정리(형태·LOD 3단계·PBR 텍스처) → 게임 안\n치수 2.435 × 5.688 × 2.746m, 야드 컨테이너 전부 이 모델로 교체",
                stages = stages.ToArray(),
                model = LoadSP2("container"),
                yaw = 30f,
                pending = false
            };
        }
    }
}
