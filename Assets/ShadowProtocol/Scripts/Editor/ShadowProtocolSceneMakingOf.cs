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
                    pending = id == "container" && LoadSP2("container").name != "container_final"
                };
            }

            list.Add(LoadSP2("container") != null && LoadSP2("container").name == "container_final" ? ContainerV2Card() : Card("container", "07", "컨테이너"));
            var captainCtrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ShadowProtocolAnimBuilder.CaptainControllerPath);
            list.Add(CharacterCard("female_agent", "플레이어(여성 잠입 요원)", PlayerModelPath, playerCtrl, 1.705f, new[] { "Idle", "Walk", "Run", "CrouchIdle", "CrouchWalk", "Throw" }));
            list.Add(CharacterCard("heavyguard_v2", "중무장 경비병", GuardModelPath, guardCtrl, 1.903f, new[] { "Idle", "Walk", "Run", "LookAround", "HitFall", "Talk" },
                "\n리그 수리: VARCO 원본은 상체 뼈 이름이 뒤섞여(오른팔이 Left*, 왼팔이 Head 한 뼈) 왼팔을 오른팔 미러로 다시 세움"));
            list.Add(CharacterCard("supervisor_v2", "경비 반장", CaptainModelPath, captainCtrl, 1.804f, new[] { "Idle", "Walk", "Run", "LookAround", "HitFall", "Talk" }));
            foreach (var c in SP2Cards) if (c.id != "container" && c.id != "heavyguard" && c.id != "supervisor") list.Add(Card(c.id, c.num, c.title));
            foreach (var p in new[] { ("Prop_Shelf", "선반"), ("Prop_Crate", "나무 상자"), ("Prop_Bins", "수납함"), ("Prop_DataCore", "데이터 코어 단말기"), ("Prop_Wall", "벽 패널") })
                list.Add(new MakingOfGallery.Entry
                {
                    title = p.Item2, note = "VARCO 3D 생성 → Blender 정리\n공정 이미지는 자료를 받는 대로 추가. 지금은 게임 모델 턴테이블만.",
                    stages = new MakingOfGallery.Stage[0], model = LoadProp(p.Item1), yaw = 30f
                });
            gal.entries = list.ToArray();
            return gal;
        }

        const string CharacterArt = @"C:\Users\mbc\Documents\user\ShadowProtocol\ArtSource\Characters\03_renders_final";

        /// <summary>인물 최종본 카드: [VARCO 원본 → Blender 최종 전후 → 얼굴·장비 근접 → T포즈·피부 테스트 → 애니메이션 프레임 시트 → 게임 모델]</summary>
        static MakingOfGallery.Entry CharacterCard(string id, string title, string modelPath, RuntimeAnimatorController ctrl, float height, string[] clips, string extra = "")
        {
            string dir = MakingOfDir + "/characters";
            var stages = new List<MakingOfGallery.Stage>();
            void Img(string file, string caption)
            {
                var s = ImportSprite(Path.Combine(CharacterArt, file), $"{dir}/{file.Replace('\\', '_').Replace('@', '_')}");
                if (s != null) stages.Add(new MakingOfGallery.Stage { caption = caption, image = s });
            }
            Img($"{id}_before_after.png", "VARCO 원본 → Blender 최종본 전후 비교");
            Img($"{id}_closeup.png", "Blender 최종본 — 얼굴·장비 근접");
            Img("characters_tpose_front.png", "세 인물 T포즈(정면) — 같은 23뼈 Humanoid 리그");
            Img("characters_skin_test_side.png", "피부 테스트(팔·무릎 굽힘)");
            foreach (var c in clips) Img($"anims\\{id}@{c}.png", $"애니메이션 {c} — 리타깃·발 고정·루프 정리");
            return new MakingOfGallery.Entry
            {
                title = title,
                note = $"VARCO 3D 생성 → Blender 다듬기(키 {height:0.000}m, 정점 용접·형상 정리·텍스처 재도색·AO, 가중치 완화) → VARCO Animate 동작을 Blender에서 리타깃·발 고정·루프 정리 → Unity Humanoid" + extra,
                stages = stages.ToArray(),
                model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath),
                controller = ctrl,
                yaw = 30f
            };
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
            // Blender 최종본(ISO 20ft 치수로 새로 모델링, 03_renders_final)
            Img("03_renders_final", "container_before_after.png", "VARCO 원본 → Blender 최종본 전후 비교");
            Img("03_renders_final", "container_matcap_lods.png", "Blender 최종본 — LOD0/1/2 형태(매트캡)");
            Img("03_renders_final", "container_closeup_doors.png", "Blender 최종본 — 문 철물 근접");
            Img("03_renders_final", "container_closeup_corner.png", "Blender 최종본 — 모서리 주물 근접");
            Img("03_renders_final", "container_beauty_doors.png", "Blender 최종 렌더(문 쪽)");
            Img("03_renders_final", "container_beauty_front.png", "Blender 최종 렌더(앞 벽 쪽)");
            return new MakingOfGallery.Entry
            {
                title = "컨테이너(최종본)",
                note = "VARCO 프롬프트 → VARCO 생성 이미지 → VARCO 3D 원본(옆판 물결·철물 뭉개짐) → Blender에서 ISO 20ft 치수로 새로 모델링(골판·주물·문 철물, LOD 5550/2578/928 tris, PBR 5종) → 게임 안\n치수 6.058 × 2.438 × 2.591m, 야드 컨테이너 전부 이 모델로 교체",
                stages = stages.ToArray(),
                model = LoadSP2("container"),
                yaw = 30f,
                pending = false
            };
        }
    }
}
