using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadowProtocol.EditorTools
{
    /// <summary>
    /// 환경 모듈 교체. VARCO로 만든 환경 모듈 FBX가 Assets/ShadowProtocol/Models/Env/ 에 있으면
    /// 기본 도형으로 만든 바닥·펜스·캐트워크·가로등·트러스(벽은 모듈 대기)의 "겉모습"을 모듈 타일링으로 바꾼다.
    /// 충돌체와 NavMesh는 기존 박스를 그대로 쓴다(렌더러만 끈다). 캐트워크만 모듈 폭에 맞춰 데크 충돌체를 줄인다(ShadowProtocolLevelLayout).
    /// 모듈 파일이 없거나 HoldEnvModules가 true면 아무것도 바꾸지 않는다.
    ///
    /// Blender 기준(길이 +X, 정면 -Y)으로 내보낸 FBX는 Unity에서 길이 -X, 정면 +Z, 위 +Y로 들어온다.
    /// 타일링은 모듈의 로컬 경계(중심·윗면)와 반복 간격(pitch)으로 맞추므로 원점 위치가 달라도 된다.
    ///   Env_floor_slab.fbx      4×4×0.2, 원점 윗면 중앙 → 실내 바닥 윗면 격자(90° 단위 랜덤 회전)
    ///   Env_asphalt_slab.fbx    4×4×0.2, 원점 윗면 중앙 → 야드 바닥 + 바깥 지면
    ///   Env_fence_section.fbx   기둥 간격 3.0, 높이 3.12, 원점 왼쪽 기둥 바닥 → 펜스 선(이음부 기둥은 같은 자리에 겹쳐 하나로 보임)
    ///   Env_catwalk_section.fbx 길이 4.0, 폭 0.99(양쪽 난간), 원점 바닥판 윗면 중앙 → 캐트워크 가운데 구간(Z 1.4배)
    ///   Env_street_lamp.fbx     높이 6.0, 원점 바닥 중앙, 암 +Z → 가로등(암을 야드 안쪽으로)
    ///   Env_truss_worklight.fbx 길이 6.0, 원점 윗면 중앙, 가운데에 작업등 → 천장 트러스(작업등 광원을 모듈 램프 위치로)
    /// </summary>
    public static partial class ShadowProtocolSceneBuilder
    {
        const string EnvRoot = "Assets/ShadowProtocol/Models/Env/";
        /// <summary>보류: Blender 고품질 최종본이 오기 전까지 환경 모듈을 씬에 쓰지 않는다(최종본으로 교체 후 false).</summary>
        public static bool HoldEnvModules = true;
        /// <summary>BuildEnvironment2가 기록하는 펜스 변(시작, 끝)</summary>
        static readonly List<(Vector3 a, Vector3 b)> FenceSides = new List<(Vector3, Vector3)>();

        // 캐트워크 모듈 배치(레벨 충돌체와 공유)
        const float CatwalkScaleZ = 1.4f;      // 0.993m × 1.4 = 1.39m
        const float CatwalkZN = -9.23f;        // 북쪽 난간 = 기존 RailCollider 선
        const float CatwalkZS = -10.62f;       // 남쪽 난간
        const float CatwalkX0 = -12f, CatwalkX1 = 13f;

        static GameObject LoadEnv(string n) => HoldEnvModules ? null : AssetDatabase.LoadAssetAtPath<GameObject>(EnvRoot + "Env_" + n + ".fbx");

        static Transform EnvGroup(string name)
        {
            var env = GameObject.Find("Environment");
            var t = new GameObject(name).transform;
            if (env != null) t.SetParent(env.transform, false);
            return t;
        }

        /// <summary>모듈의 로컬 렌더러 경계(원점 기준)</summary>
        static Bounds PrefabBounds(GameObject prefab)
        {
            var tmp = (GameObject)Object.Instantiate(prefab);
            tmp.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var rs = tmp.GetComponentsInChildren<Renderer>();
            var b = rs.Length > 0 ? rs[0].bounds : new Bounds(Vector3.zero, Vector3.one);
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            Object.DestroyImmediate(tmp);
            return b;
        }

        /// <summary>모듈이 있으면 환경 겉모습을 교체. 교체한 종류 수를 돌려준다.</summary>
        static int ApplyEnvironmentModules()
        {
            int kinds = 0;
            var wall = LoadEnv("wall_panel");
            var floor = LoadEnv("floor_slab");
            var asphalt = LoadEnv("asphalt_slab");
            var fence = LoadEnv("fence_section");
            var catwalk = LoadEnv("catwalk_section");
            var lamp = LoadEnv("street_lamp");
            var truss = LoadEnv("truss_worklight");

            if (wall != null)
            {
                var walls = GameObject.Find("Level/Walls");
                if (walls != null)
                    foreach (Transform t in walls.transform)
                        if ((t.name.StartsWith("Wall_") || t.name.StartsWith("Lintel_")) && t.GetComponent<MeshRenderer>() != null && t.GetComponent<MeshRenderer>().enabled)
                            TileBoxFaces(t, wall);
                kinds++;
            }
            if (floor != null) { var f = GameObject.Find("Level/Floor"); if (f != null) TileBoxTop(f.transform, floor, 11); kinds++; }
            if (asphalt != null)
            {
                var y = GameObject.Find("Level/Floor_Yard"); if (y != null) TileBoxTop(y.transform, asphalt, 23);
                TileOutdoorGround(asphalt);
                kinds++;
            }
            if (fence != null)
            {
                var old = GameObject.Find("Environment/Fence");
                if (old != null) foreach (var r in old.GetComponentsInChildren<Renderer>()) r.enabled = false;
                var root = EnvGroup("FenceModules");
                var fb = PrefabBounds(fence);
                // 기둥 간격 3.0m로 반복: 모듈 양 끝 기둥이 이웃 모듈 기둥과 같은 자리에 겹쳐 하나로 보인다
                foreach (var side in FenceSides) TileLine(root, fence, side.a, side.b, 3f, fb.center.x, 1f);
                kinds++;
            }
            if (catwalk != null)
            {
                var cw = GameObject.Find("Level/Catwalk");
                if (cw != null)
                {
                    var deck = cw.transform.Find("Deck");
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
                        // 원점 = 바닥판 윗면 → 데크 윗면 높이에 둔다
                        TileLine(root, catwalk, new Vector3(CatwalkX0, top, zc), new Vector3(CatwalkX1, top, zc), 4f, cb.center.x, CatwalkScaleZ);
                    }
                }
                kinds++;
            }
            if (lamp != null)
            {
                var lamps = GameObject.Find("Environment/StreetLamps");
                var lb = PrefabBounds(lamp);
                if (lamps != null)
                    foreach (Transform t in lamps.transform)
                    {
                        foreach (var r in t.GetComponentsInChildren<MeshRenderer>()) r.enabled = false;
                        // 암(+Z)을 야드 중심 쪽으로(펜스 선에 수직)
                        Vector3 toYard = new Vector3(-28f, 0f, 0f) - t.position; toYard.y = 0f;
                        if (Mathf.Abs(toYard.x) > Mathf.Abs(toYard.z)) toYard.z = 0f; else toYard.x = 0f;
                        var rot = Quaternion.LookRotation(toYard.normalized, Vector3.up);
                        var m = SpawnProp(lamp, t, Vector3.zero, Quaternion.Inverse(t.rotation) * rot, Vector3.one);
                        m.name = "LampModule";
                        var light = t.GetComponentInChildren<Light>();
                        if (light != null) light.transform.position = t.position + rot * new Vector3(0f, lb.max.y - 0.5f, lb.max.z - 0.4f);
                    }
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
                    foreach (var t in beams)
                    {
                        Vector3 c = t.position, s = t.lossyScale;
                        float top = c.y + s.y * 0.5f;
                        var placed = TileLine(root, truss, new Vector3(c.x - s.x * 0.5f, top, c.z), new Vector3(c.x + s.x * 0.5f, top, c.z), 6f, tb.center.x, 1f);
                        // 작업등 광원을 모듈 램프(모듈 가운데 아래쪽) 위치로. 총 밝기는 기존과 비슷하게 유지
                        foreach (var p in placed)
                        {
                            var lg = new GameObject("Light");
                            lg.transform.SetParent(work.transform, false);
                            lg.transform.position = new Vector3(p.x, top + tb.min.y - 0.1f, p.z);
                            var l = lg.AddComponent<Light>();
                            l.type = LightType.Point; l.color = lc; l.range = range; l.shadows = LightShadows.None;
                            l.intensity = inten * perBeamOld / Mathf.Max(1, placed.Count);
                            l.renderMode = LightRenderMode.ForcePixel;
                        }
                    }
                }
                kinds++;
            }
            if (kinds > 0) Debug.Log($"[ShadowProtocol] 환경 모듈 교체: {kinds}종 (충돌체·NavMesh는 기존 박스 유지, 캐트워크 데크만 모듈 폭)");
            return kinds;
        }

        /// <summary>a→b 선을 따라 모듈을 길이 방향(로컬 X)으로 타일링. 개수는 pitch에 가장 가깝게, 남는 길이는 X 스케일로 흡수.
        /// centerX: 모듈 로컬에서 한 칸(pitch)의 중심 X. 배치한 칸 중심(월드) 목록을 돌려준다.</summary>
        static List<Vector3> TileLine(Transform parent, GameObject module, Vector3 a, Vector3 b, float pitch, float centerX, float scaleZ)
        {
            var centers = new List<Vector3>();
            float len = Vector3.Distance(a, b);
            if (len < 0.05f || pitch < 0.01f) return centers;
            int n = Mathf.Max(1, Mathf.RoundToInt(len / pitch));
            float sx = len / (n * pitch);
            Vector3 dir = (b - a).normalized;
            var rot = Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(0f, -90f, 0f); // 모듈 로컬 X를 진행 방향으로
            for (int i = 0; i < n; i++)
            {
                Vector3 p = a + dir * (len * (i + 0.5f) / n);
                var h = SpawnProp(module, parent, p - rot * new Vector3(centerX * sx, 0f, 0f), rot, new Vector3(sx, 1f, scaleZ));
                h.name = module.name + "_" + i;
                centers.Add(p);
            }
            return centers;
        }

        /// <summary>박스(벽)의 긴 변 양면에 모듈을 단수×길이로 타일링하고 박스 렌더러를 끈다(충돌체 유지). 벽 모듈 대기 중.</summary>
        static void TileBoxFaces(Transform box, GameObject panel)
        {
            Vector3 S = box.lossyScale;
            Vector3 m = PrefabSize(panel);
            bool alongX = S.x >= S.z;
            float L = alongX ? S.x : S.z, T = alongX ? S.z : S.x, H = S.y;
            if (m.x < 0.01f || m.y < 0.01f) return;
            int rows = Mathf.Max(1, Mathf.RoundToInt(H / m.y));
            float sy = H / (rows * m.y);
            int n = Mathf.Max(1, Mathf.RoundToInt(L / m.x));
            float sx = L / (n * m.x);
            var c = UnitContainer(box);
            foreach (int side in new[] { -1, 1 })
                for (int r = 0; r < rows; r++)
                    for (int i = 0; i < n; i++)
                    {
                        float along = -L * 0.5f + L * (i + 0.5f) / n;
                        float across = side * (T * 0.5f - m.z * 0.5f + 0.002f);
                        float yaw = alongX ? (side > 0 ? 0f : 180f) : (side > 0 ? 90f : -90f);
                        SpawnProp(panel, c,
                            (alongX ? new Vector3(along, 0f, across) : new Vector3(across, 0f, along)) + Vector3.up * (-H * 0.5f + r * H / rows),
                            Quaternion.Euler(0f, yaw, 0f), new Vector3(sx, sy, 1f));
                    }
            box.GetComponent<MeshRenderer>().enabled = false;
        }

        /// <summary>박스(바닥) 윗면에 슬래브 모듈을 격자로 타일링(90° 단위 랜덤 회전으로 반복감 줄임)하고 박스는 그림자만 남긴다(충돌체 유지)</summary>
        static void TileBoxTop(Transform box, GameObject slab, int seed)
        {
            Vector3 S = box.lossyScale;
            var mb = PrefabBounds(slab);
            Vector3 m = mb.size;
            if (m.x < 0.01f || m.z < 0.01f) return;
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
        }

        /// <summary>바깥 지면(OutdoorGround 평면)을 아스팔트 모듈 4m 격자로. 실내·야드 바닥 아래 칸은 건너뛴다.</summary>
        static void TileOutdoorGround(GameObject slab)
        {
            var ground = GameObject.Find("Environment/OutdoorGround");
            if (ground == null) return;
            var mb = PrefabBounds(slab);
            float step = mb.size.x;
            if (step < 0.5f) return;
            float topY = ground.transform.position.y;
            var skip = new List<Rect>();
            foreach (var n in new[] { "Level/Floor", "Level/Floor_Yard" })
            {
                var f = GameObject.Find(n);
                if (f == null) continue;
                Vector3 p = f.transform.position, s = f.transform.lossyScale;
                skip.Add(new Rect(p.x - s.x * 0.5f, p.z - s.z * 0.5f, s.x, s.z));
            }
            // 야드 모서리(-40, -20)에 맞춘 격자, 안개 끝(약 70m) 안쪽만
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
        }
    }
}
