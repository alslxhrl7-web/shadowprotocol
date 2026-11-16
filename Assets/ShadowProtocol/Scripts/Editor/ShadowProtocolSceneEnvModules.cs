using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadowProtocol.EditorTools
{
    /// <summary>
    /// 환경 모듈 교체. Blender 최종본(Models/EnvFinal/env_*.fbx, 메뉴 ShadowProtocol/Import Env Final로 가져옴)이 있으면
    /// 기본 도형으로 만든 벽·바닥·아스팔트·펜스·캣워크·가로등·트러스·안전선의 "겉모습"을 모듈 타일링으로 바꾼다.
    /// 충돌체와 NavMesh는 기존 박스를 그대로 쓴다(렌더러만 끈다). 캣워크만 모듈 폭에 맞춰 데크 충돌체를 줄인다(ShadowProtocolLevelLayout).
    /// 모듈 파일이 없거나 HoldEnvModules가 true면 아무것도 바꾸지 않는다.
    ///
    /// FBX 루트에 축 보정 회전(-90° X)이 있어 SpawnProp으로 홀더 아래에 두면 Unity에서 위 = +Y가 된다. 그때 축(실측):
    ///   env_floor / env_asphalt  4×4×0.2, 원점 윗면 중앙 → 4m 격자(90° 단위 랜덤 회전)
    ///   env_wall                 길이 X 4.0, 높이 3.0, 두께 Z 0.25, 원점 바닥 중앙 → 벽 박스마다 한 줄(높이·두께는 박스에 맞춰 스케일)
    ///   env_fence                원점 = 기둥 바닥, 다음 기둥이 로컬 -X 3.0m, 가시철선 암이 로컬 -Z(바깥) → 3m 간격, 마지막 칸 끝에 env_fence_post_end
    ///   env_fence_post_end       끝 기둥, 버팀대가 로컬 +X(패널 쪽) → 펜스 모듈과 같은 회전으로 줄 끝에
    ///   env_catwalk              길이 X 4.0, 폭 Z 1.39, 원점 바닥 중앙, 데크 윗면 +0.25 → 4m 간격
    ///   env_lamp                 높이 6.0, 원점 받침 바닥, 암 로컬 -X(1.59m) → 암을 야드 안쪽으로
    ///   env_truss                길이 X 6.0, 원점 = 트러스 중심, 위 +0.38 / 투광등 바닥 -0.69 → 6m 간격, 광원을 투광등 아래로
    ///   env_decal_line_yellow/white 4.0×0.125, 면 +2mm → 바닥 안전선·주차선
    /// </summary>
    public static partial class ShadowProtocolSceneBuilder
    {
        const string EnvRoot = "Assets/ShadowProtocol/Models/EnvFinal/";
        /// <summary>보류: Blender 고품질 최종본이 오기 전까지 환경 모듈을 씬에 쓰지 않는다. 2026-11-16 최종본 반영 → false.</summary>
        public static bool HoldEnvModules = false;
        /// <summary>BuildEnvironment2가 기록하는 펜스 변(시작, 끝)</summary>
        static readonly List<(Vector3 a, Vector3 b)> FenceSides = new List<(Vector3, Vector3)>();

        // 캣워크 모듈 배치(레벨 충돌체와 공유)
        const float CatwalkScaleZ = 1.0f;      // 최종본 폭 1.39m 그대로
        const float CatwalkZN = -9.23f;        // 북쪽 난간 = 기존 RailCollider 선
        const float CatwalkZS = -10.62f;       // 남쪽 난간
        const float CatwalkX0 = -12f, CatwalkX1 = 13f;
        const float CatwalkDeckTop = 0.25f;    // 모듈 원점(바닥) → 데크 윗면

        static GameObject LoadEnv(string n) => HoldEnvModules ? null : AssetDatabase.LoadAssetAtPath<GameObject>(EnvRoot + "env_" + n + ".fbx");

        static Transform EnvGroup(string name)
        {
            var env = GameObject.Find("Environment");
            var t = new GameObject(name).transform;
            if (env != null) t.SetParent(env.transform, false);
            return t;
        }

        /// <summary>모듈의 렌더러 경계(원점 기준, FBX 루트의 축 보정 회전 포함)</summary>
        static Bounds PrefabBounds(GameObject prefab)
        {
            var tmp = (GameObject)Object.Instantiate(prefab);
            tmp.transform.position = Vector3.zero; // 루트 회전(축 보정)은 그대로 둔다
            var rs = tmp.GetComponentsInChildren<Renderer>();
            var b = rs.Length > 0 ? rs[0].bounds : new Bounds(Vector3.zero, Vector3.one);
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            Object.DestroyImmediate(tmp);
            return b;
        }

        /// <summary>로컬 축 axis를 월드 방향 dir로 돌리는 수평 회전</summary>
        static Quaternion AxisTo(Vector3 axis, Vector3 dir)
        {
            dir.y = 0f;
            return Quaternion.LookRotation(dir.normalized, Vector3.up) * Quaternion.Inverse(Quaternion.LookRotation(axis, Vector3.up));
        }

        /// <summary>모듈이 있으면 환경 겉모습을 교체. 교체한 종류 수를 돌려준다.</summary>
        static int ApplyEnvironmentModules()
        {
            int kinds = 0;
            var wall = LoadEnv("wall");
            var floor = LoadEnv("floor");
            var asphalt = LoadEnv("asphalt");
            var fence = LoadEnv("fence");
            var fenceEnd = LoadEnv("fence_post_end");
            var catwalk = LoadEnv("catwalk");
            var lamp = LoadEnv("lamp");
            var truss = LoadEnv("truss");
            var decalY = LoadEnv("decal_line_yellow");
            var decalW = LoadEnv("decal_line_white");
            var counts = new List<string>();

            if (wall != null)
            {
                int n = 0;
                var walls = GameObject.Find("Level/Walls");
                var wb = PrefabBounds(wall);
                if (walls != null)
                    foreach (Transform t in walls.transform)
                    {
                        if (!(t.name.StartsWith("Wall_") || t.name.StartsWith("Lintel_") || t.name.StartsWith("Partition_"))) continue;
                        var r = t.GetComponent<MeshRenderer>();
                        if (r == null || !r.enabled) continue;
                        n += TileWallBox(t, wall, wb);
                    }
                counts.Add($"벽 {n}");
                kinds++;
            }
            if (floor != null) { var f = GameObject.Find("Level/Floor"); if (f != null) counts.Add("바닥 " + TileBoxTop(f.transform, floor, 11)); kinds++; }
            if (asphalt != null)
            {
                int n = 0;
                var y = GameObject.Find("Level/Floor_Yard"); if (y != null) n += TileBoxTop(y.transform, asphalt, 23);
                n += TileOutdoorGround(asphalt);
                counts.Add("아스팔트 " + n);
                kinds++;
            }
            if (fence != null)
            {
                var old = GameObject.Find("Environment/Fence");
                if (old != null) foreach (var r in old.GetComponentsInChildren<Renderer>()) r.enabled = false;
                var root = EnvGroup("FenceModules");
                int n = 0;
                foreach (var side in FenceSides) n += TileFence(root, fence, fenceEnd, side.a, side.b);
                counts.Add("펜스 " + n);
                kinds++;
            }
            if (catwalk != null)
            {
                var cw = GameObject.Find("Level/Catwalk");
                var deck = cw != null ? cw.transform.Find("Deck") : null;
                if (deck != null)
                {
                    foreach (Transform t in cw.transform)
                    {
                        if (t.name.StartsWith("Rail_") || t.name == "RailPost" || t.name == "Deck")
                        { var r = t.GetComponent<Renderer>(); if (r != null) r.enabled = false; }
                        // 받침 기둥은 모듈 중심선 아래로 옮겨 그대로 보인다(시각용, 충돌 없음)
                        if (t.name == "Support") { var p = t.position; p.z = (CatwalkZN + CatwalkZS) * 0.5f; t.position = p; }
                    }
                    float top = deck.position.y + deck.lossyScale.y * 0.5f;
                    float zc = (CatwalkZN + CatwalkZS) * 0.5f;
                    var root = EnvGroup("CatwalkModules");
                    var cb = PrefabBounds(catwalk);
                    var placed = TileCentered(root, catwalk, new Vector3(CatwalkX0, top - CatwalkDeckTop, zc), new Vector3(CatwalkX1, top - CatwalkDeckTop, zc), 4f, cb.center.x, CatwalkScaleZ);
                    counts.Add("캣워크 " + placed.Count);
                }
                kinds++;
            }
            if (lamp != null)
            {
                var lamps = GameObject.Find("Environment/StreetLamps");
                var lb = PrefabBounds(lamp);
                int n = 0;
                if (lamps != null)
                    foreach (Transform t in lamps.transform)
                    {
                        foreach (var r in t.GetComponentsInChildren<MeshRenderer>()) r.enabled = false;
                        // 암(로컬 -X)을 야드 중심 쪽으로(펜스 선에 수직)
                        Vector3 toYard = new Vector3(-28f, 0f, 0f) - t.position; toYard.y = 0f;
                        if (Mathf.Abs(toYard.x) > Mathf.Abs(toYard.z)) toYard.z = 0f; else toYard.x = 0f;
                        var rot = AxisTo(Vector3.left, toYard);
                        var m = SpawnProp(lamp, t, Vector3.zero, Quaternion.Inverse(t.rotation) * rot, Vector3.one);
                        m.name = "LampModule";
                        var light = t.GetComponentInChildren<Light>();
                        if (light != null) light.transform.position = t.position + rot * new Vector3(lb.min.x + 0.2f, lb.max.y - 0.35f, 0f);
                        n++;
                    }
                counts.Add("가로등 " + n);
                kinds++;
            }
            if (truss != null)
            {
                var work = GameObject.Find("Environment/WorkLights");
                if (work != null)
                {
                    var root = EnvGroup("TrussModules");
                    var tb = PrefabBounds(truss);
                    var beams = new List<Transform>();
                    var oldLights = new List<Light>();
                    foreach (Transform t in work.transform)
                    {
                        var l = t.GetComponent<Light>();
                        if (l != null) { oldLights.Add(l); continue; }
                        var r = t.GetComponent<MeshRenderer>();
                        if (r == null) continue;
                        r.enabled = false;
                        if (t.name == "TrussBeam") beams.Add(t);
                    }
                    Color lc = oldLights.Count > 0 ? oldLights[0].color : new Color(1f, 0.93f, 0.8f);
                    float range = oldLights.Count > 0 ? oldLights[0].range : 11f;
                    float inten = oldLights.Count > 0 ? oldLights[0].intensity : 1.5f;
                    int perBeamOld = beams.Count > 0 ? oldLights.Count / beams.Count : 4;
                    foreach (var l in oldLights) Object.DestroyImmediate(l.gameObject);
                    int n = 0;
                    foreach (var t in beams)
                    {
                        Vector3 c = t.position, s = t.lossyScale;
                        float top = c.y + s.y * 0.5f;
                        float y = top - tb.max.y; // 트러스 윗면을 기존 보 윗면에
                        // 양 끝 벽판이 벽 안쪽 면(±0.5m)에 닿도록
                        var placed = TileCentered(root, truss, new Vector3(c.x - s.x * 0.5f + 0.5f, y, c.z), new Vector3(c.x + s.x * 0.5f - 0.5f, y, c.z), 6f, tb.center.x, 1f);
                        n += placed.Count;
                        foreach (var p in placed)
                        {
                            // 카메라가 투광등·트러스를 뚫지 않게 충돌 상자(시각 모듈 크기)
                            var col = new GameObject("TrussCollider");
                            col.transform.SetParent(root, false);
                            col.transform.position = new Vector3(p.x, y + tb.center.y, p.z);
                            var bc = col.AddComponent<BoxCollider>();
                            bc.size = new Vector3(6f * 0.98f, tb.size.y, tb.size.z);
                            // 작업등 광원을 투광등 아래로. 총 밝기는 기존과 비슷하게 유지
                            var lg = new GameObject("Light");
                            lg.transform.SetParent(work.transform, false);
                            lg.transform.position = new Vector3(p.x, y + tb.min.y - 0.1f, p.z);
                            var l = lg.AddComponent<Light>();
                            l.type = LightType.Point; l.color = lc; l.range = range; l.shadows = LightShadows.None;
                            l.intensity = inten * perBeamOld / Mathf.Max(1, placed.Count);
                            l.renderMode = LightRenderMode.ForcePixel;
                        }
                    }
                    counts.Add("트러스 " + n);
                }
                kinds++;
            }
            if (decalY != null || decalW != null)
            {
                int n = 0;
                var decor = GameObject.Find("Decor");
                if (decor != null && decalY != null)
                    foreach (Transform t in decor.transform)
                        if (t.name.StartsWith("Stripe_")) n += TileDecal(t, decalY);
                var lot = GameObject.Find("Environment/ParkingLines");
                if (lot != null && decalW != null)
                    foreach (Transform t in lot.transform)
                        if (t.name == "Line" || t.name == "Edge") n += TileDecal(t, decalW);
                counts.Add("안전선 " + n);
                kinds++;
            }
            if (kinds > 0)
            {
                // 드로우콜: 같은 칸(셀)에 있는 작은 모듈들을 재질별 하위 메시로 합친다(렌더러 수 = 컬링·조명 패스 단위가 줄어든다).
                // 펜스는 인스턴싱만으로는 가로등 조명 패스·그림자 패스에서 잘 안 묶여(6개 시점 약 550 드로우) 12m 칸마다 합친다.
                // 대신 합친 메시만큼 빌드 용량이 늘어난다(펜스 약 53만 정점).
                if (AssetDatabase.IsValidFolder(MergedFolder)) AssetDatabase.DeleteAsset(MergedFolder);
                AssetDatabase.CreateFolder(System.IO.Path.GetDirectoryName(MergedFolder).Replace('\\', '/'), System.IO.Path.GetFileName(MergedFolder));
                var merged = new List<string>();
                var lvl = GameObject.Find("Level");
                merged.Add("지면 " + MergeUnder(GameObject.Find("Environment/GroundModules"), GameObject.Find("Environment/GroundModules"), 16f, "Ground"));
                if (lvl != null)
                {
                    var fl = new GameObject("FloorTiles").transform; fl.SetParent(lvl.transform, false);
                    int a = MergeUnder(GameObject.Find("Level/Floor"), fl.gameObject, 8f, "Floor");
                    int b = MergeUnder(GameObject.Find("Level/Floor_Yard"), fl.gameObject, 8f, "Yard");
                    merged.Add("바닥 " + (a + b));
                }
                var wallsGo = GameObject.Find("Level/Walls");
                if (wallsGo != null)
                {
                    var wm = new GameObject("WallModules").transform; wm.SetParent(wallsGo.transform, false);
                    merged.Add("벽 " + MergeUnder(wallsGo, wm.gameObject, 8f, "Wall", skip: wm));
                }
                merged.Add("캣워크 " + MergeUnder(GameObject.Find("Environment/CatwalkModules"), GameObject.Find("Environment/CatwalkModules"), 8f, "Catwalk"));
                merged.Add("트러스 " + MergeUnder(GameObject.Find("Environment/TrussModules"), GameObject.Find("Environment/TrussModules"), 12f, "Truss"));
                merged.Add("펜스 " + MergeUnder(GameObject.Find("Environment/FenceModules"), GameObject.Find("Environment/FenceModules"), 12f, "Fence"));
                Debug.Log($"[ShadowProtocol] 환경 모듈(Blender 최종본) 교체: {kinds}종 — {string.Join(", ", counts)} / 합친 렌더러: {string.Join(", ", merged)} (충돌체·NavMesh는 기존 박스 유지, 캣워크 데크만 모듈 폭)");
            }
            return kinds;
        }

        const string MergedFolder = "Assets/Scenes/ShadowProtocol_Demo/EnvMerged";

        /// <summary>source 아래 모듈 인스턴스(EnvFinal FBX에서 온 MeshRenderer)를 cell 크기 칸마다 하나의 메시(재질별 하위 메시)로 합쳐 parent 아래에 두고
        /// 원래 인스턴스는 지운다. 만든 렌더러 수를 돌려준다.</summary>
        static int MergeUnder(GameObject source, GameObject parent, float cell, string tag, Transform skip = null)
        {
            if (source == null || parent == null) return 0;
            var groups = new Dictionary<string, List<MeshRenderer>>();
            foreach (var r in source.GetComponentsInChildren<MeshRenderer>())
            {
                if (!r.enabled || (skip != null && r.transform.IsChildOf(skip))) continue;
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                if (!AssetDatabase.GetAssetPath(mf.sharedMesh).Contains("/Models/EnvFinal/")) continue;
                Vector3 c = r.bounds.center;
                string key = $"{Mathf.FloorToInt(c.x / cell)}_{Mathf.FloorToInt(c.z / cell)}_{(int)r.shadowCastingMode}";
                if (!groups.TryGetValue(key, out var list)) groups[key] = list = new List<MeshRenderer>();
                list.Add(r);
            }
            int made = 0;
            var toDelete = new HashSet<GameObject>();
            Matrix4x4 toParent = parent.transform.worldToLocalMatrix;
            foreach (var kv in groups)
            {
                var byMat = new Dictionary<Material, List<CombineInstance>>();
                var order = new List<Material>();
                foreach (var r in kv.Value)
                {
                    var mesh = r.GetComponent<MeshFilter>().sharedMesh;
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mesh.subMeshCount && i < mats.Length; i++)
                    {
                        if (!byMat.TryGetValue(mats[i], out var ci)) { byMat[mats[i]] = ci = new List<CombineInstance>(); order.Add(mats[i]); }
                        ci.Add(new CombineInstance { mesh = mesh, subMeshIndex = i, transform = toParent * r.localToWorldMatrix });
                    }
                    // 모듈 홀더(SpawnProp가 만든 부모)째 지운다
                    var holder = r.transform;
                    while (holder.parent != null && holder.parent.GetComponent<MeshRenderer>() == null && holder.parent.name != "PropVisual" && holder.parent.gameObject != source && holder.parent != parent.transform) holder = holder.parent;
                    toDelete.Add(holder.gameObject);
                }
                var parts = new CombineInstance[order.Count];
                for (int i = 0; i < order.Count; i++)
                {
                    var part = new Mesh { indexFormat = IndexFormat.UInt32 };
                    part.CombineMeshes(byMat[order[i]].ToArray(), true, true);
                    parts[i] = new CombineInstance { mesh = part, transform = Matrix4x4.identity };
                }
                int verts = 0;
                foreach (var p in parts) verts += p.mesh.vertexCount;
                // 인덱스 형식은 합치기 전에 정한다(합친 뒤 바꾸면 인덱스가 비워진다)
                var final = new Mesh { name = $"{tag}_{kv.Key}", indexFormat = verts < 65000 ? IndexFormat.UInt16 : IndexFormat.UInt32 };
                final.CombineMeshes(parts, false, false);
                final.RecalculateBounds();
                foreach (var p in parts) Object.DestroyImmediate(p.mesh);
                AssetDatabase.CreateAsset(final, $"{MergedFolder}/{final.name}.asset");
                var go = new GameObject($"{tag}_{kv.Key}");
                go.transform.SetParent(parent.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = final;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterials = order.ToArray();
                mr.shadowCastingMode = kv.Value[0].shadowCastingMode;
                made++;
            }
            foreach (var g in toDelete) if (g != null) Object.DestroyImmediate(g);
            return made;
        }

        /// <summary>a→b 선을 따라 원점이 가운데인 모듈을 길이 방향(로컬 X)으로 타일링. 개수는 pitch에 가장 가깝게, 남는 길이는 X 스케일로 흡수.
        /// centerX: 모듈 로컬에서 한 칸의 중심 X. 배치한 칸 중심(월드) 목록을 돌려준다.</summary>
        static List<Vector3> TileCentered(Transform parent, GameObject module, Vector3 a, Vector3 b, float pitch, float centerX, float scaleZ)
        {
            var centers = new List<Vector3>();
            float len = Vector3.Distance(a, b);
            if (len < 0.05f || pitch < 0.01f) return centers;
            int n = Mathf.Max(1, Mathf.RoundToInt(len / pitch));
            float sx = len / (n * pitch);
            Vector3 dir = (b - a).normalized;
            var rot = AxisTo(Vector3.right, dir);
            for (int i = 0; i < n; i++)
            {
                Vector3 p = a + dir * (len * (i + 0.5f) / n);
                var h = SpawnProp(module, parent, p - rot * new Vector3(centerX * sx, 0f, 0f), rot, new Vector3(sx, 1f, scaleZ));
                h.name = module.name + "_" + i;
                centers.Add(p);
            }
            return centers;
        }

        /// <summary>펜스 한 변: 모듈(원점 기둥, 다음 기둥 로컬 -X 3m)을 3m 간격으로 잇고 마지막 칸 끝에 끝 기둥. 가시철선 암(로컬 -Z)은 부지 바깥으로.</summary>
        static int TileFence(Transform parent, GameObject module, GameObject endPost, Vector3 a, Vector3 b)
        {
            const float pitch = 3f;
            Vector3 siteCenter = new Vector3(-12f, 0f, 0f);
            Vector3 dir = (b - a); dir.y = 0f;
            var rot = AxisTo(Vector3.left, dir);
            Vector3 outward = rot * Vector3.back;
            if (Vector3.Dot(outward, (a + b) * 0.5f - siteCenter) < 0f) { var tmp = a; a = b; b = tmp; dir = -dir; rot = AxisTo(Vector3.left, dir); }
            float len = dir.magnitude;
            dir /= Mathf.Max(0.001f, len);
            int n = Mathf.Max(1, Mathf.RoundToInt(len / pitch));
            float sx = len / (n * pitch);
            for (int i = 0; i < n; i++)
            {
                var h = SpawnProp(module, parent, a + dir * (i * pitch * sx), rot, new Vector3(sx, 1f, 1f));
                h.name = module.name + "_" + i;
            }
            if (endPost != null) SpawnProp(endPost, parent, b, rot, Vector3.one).name = endPost.name;
            return n + (endPost != null ? 1 : 0);
        }

        /// <summary>벽 박스 하나를 벽 모듈 한 줄로(길이 4m 간격, 높이·두께는 박스에 맞춰 스케일). 박스 렌더러는 끄고 충돌체는 유지.</summary>
        static int TileWallBox(Transform box, GameObject panel, Bounds mb)
        {
            Vector3 S = box.lossyScale;
            Vector3 m = mb.size;
            if (m.x < 0.01f || m.y < 0.01f || m.z < 0.01f) return 0;
            bool alongX = S.x >= S.z;
            float L = alongX ? S.x : S.z, T = alongX ? S.z : S.x, H = S.y;
            int n = Mathf.Max(1, Mathf.RoundToInt(L / m.x));
            float sx = L / (n * m.x), sy = H / m.y, sz = T / m.z;
            var c = UnitContainer(box);
            var rot = Quaternion.Euler(0f, alongX ? 0f : 90f, 0f);
            for (int i = 0; i < n; i++)
            {
                float along = -L * 0.5f + L * (i + 0.5f) / n;
                Vector3 local = (alongX ? new Vector3(along, 0f, 0f) : new Vector3(0f, 0f, along)) + Vector3.up * (-H * 0.5f - mb.min.y * sy);
                local -= rot * new Vector3(mb.center.x * sx, 0f, mb.center.z * sz);
                SpawnProp(panel, c, local, rot, new Vector3(sx, sy, sz)).name = panel.name + "_" + i;
            }
            box.GetComponent<MeshRenderer>().enabled = false;
            return n;
        }

        /// <summary>바닥 띠(얇은 박스)를 안전선 데칼로(길이 4m 간격). 박스 렌더러는 끈다.</summary>
        static int TileDecal(Transform box, GameObject decal)
        {
            var r = box.GetComponent<MeshRenderer>();
            if (r == null || !r.enabled) return 0;
            Vector3 S = box.lossyScale;
            bool alongX = S.x >= S.z;
            float L = alongX ? S.x : S.z, W = alongX ? S.z : S.x;
            int n = Mathf.Max(1, Mathf.RoundToInt(L / 4f));
            float sx = L / (n * 4f), sz = Mathf.Clamp(W / 0.10f, 0.8f, 1.6f);
            var c = UnitContainer(box);
            var rot = Quaternion.Euler(0f, alongX ? 0f : 90f, 0f);
            for (int i = 0; i < n; i++)
            {
                float along = -L * 0.5f + L * (i + 0.5f) / n;
                var h = SpawnProp(decal, c, (alongX ? new Vector3(along, 0f, 0f) : new Vector3(0f, 0f, along)) + Vector3.up * (-S.y * 0.5f), rot, new Vector3(sx, 1f, sz));
                foreach (var mr in h.GetComponentsInChildren<MeshRenderer>()) mr.shadowCastingMode = ShadowCastingMode.Off;
            }
            r.enabled = false;
            return n;
        }

        /// <summary>박스(바닥) 윗면에 슬래브 모듈을 격자로 타일링(90° 단위 랜덤 회전으로 반복감 줄임)하고 박스는 그림자만 남긴다(충돌체 유지)</summary>
        static int TileBoxTop(Transform box, GameObject slab, int seed)
        {
            Vector3 S = box.lossyScale;
            var mb = PrefabBounds(slab);
            Vector3 m = mb.size;
            if (m.x < 0.01f || m.z < 0.01f) return 0;
            int nx = Mathf.Max(1, Mathf.RoundToInt(S.x / m.x)), nz = Mathf.Max(1, Mathf.RoundToInt(S.z / m.z));
            float sx = S.x / (nx * m.x), sz = S.z / (nz * m.z);
            var c = UnitContainer(box);
            var rng = new System.Random(seed);
            for (int ix = 0; ix < nx; ix++)
                for (int iz = 0; iz < nz; iz++)
                {
                    int q = rng.Next(4);
                    bool swap = (q & 1) == 1;
                    SpawnProp(slab, c, new Vector3(-S.x * 0.5f + S.x * (ix + 0.5f) / nx - mb.center.x, S.y * 0.5f - mb.max.y, -S.z * 0.5f + S.z * (iz + 0.5f) / nz - mb.center.z),
                        Quaternion.Euler(0f, q * 90f, 0f), swap ? new Vector3(sz, 1f, sx) : new Vector3(sx, 1f, sz));
                }
            var r = box.GetComponent<MeshRenderer>();
            if (r != null) r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            return nx * nz;
        }

        /// <summary>바깥 지면(OutdoorGround 평면)을 아스팔트 모듈 4m 격자로. 실내·야드 바닥 아래 칸은 건너뛴다.</summary>
        static int TileOutdoorGround(GameObject slab)
        {
            var ground = GameObject.Find("Environment/OutdoorGround");
            if (ground == null) return 0;
            var mb = PrefabBounds(slab);
            float step = mb.size.x;
            if (step < 0.5f) return 0;
            float topY = ground.transform.position.y;
            var skip = new List<Rect>();
            foreach (var n in new[] { "Level/Floor", "Level/Floor_Yard" })
            {
                var f = GameObject.Find(n);
                if (f == null) continue;
                Vector3 p = f.transform.position, s = f.transform.lossyScale;
                skip.Add(new Rect(p.x - s.x * 0.5f, p.z - s.z * 0.5f, s.x, s.z));
            }
            // 야드 모서리(-40, -20)에 맞춘 격자, farClip(100m)·안개 안쪽만
            const float x0 = -64f, x1 = 44f, z0 = -48f, z1 = 52f;
            var root = EnvGroup("GroundModules");
            var rng = new System.Random(37);
            int count = 0;
            for (float x = x0; x < x1 - 0.01f; x += step)
                for (float z = z0; z < z1 - 0.01f; z += step)
                {
                    var center = new Vector2(x + step * 0.5f, z + step * 0.5f);
                    bool covered = false;
                    foreach (var r in skip) if (r.Contains(center)) { covered = true; break; }
                    if (covered) continue;
                    var h = SpawnProp(slab, root, new Vector3(center.x - mb.center.x, topY - mb.max.y, center.y - mb.center.z), Quaternion.Euler(0f, rng.Next(4) * 90f, 0f), Vector3.one);
                    h.name = "Ground_" + count++;
                    foreach (var mr in h.GetComponentsInChildren<MeshRenderer>()) mr.shadowCastingMode = ShadowCastingMode.Off;
                }
            ground.GetComponent<Renderer>().enabled = false;
            return count;
        }
    }
}
