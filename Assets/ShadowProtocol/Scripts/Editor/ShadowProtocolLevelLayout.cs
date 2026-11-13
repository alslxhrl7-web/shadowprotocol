using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadowProtocol.EditorTools
{
    /// <summary>
    /// 확장 맵 레이아웃(약 2.7배).
    /// (a) 하역 야드  x -40..-16, z -20..20  : 시작·탈출 지점, 컨테이너 미로
    /// (b) 메인 홀    x -16..16,  z -12..12  : 선반 통로, 남쪽 캣워크(높이 3.2m)와 양쪽 경사로
    /// (c) 사무 구역  x -16..5.5, z 12..20   : 복도 + 경비실·사무실·창고, 창고→서버실 덕트(웅크려야 통과)
    /// (d) 서버실     x 5.5..16,  z 12..20   : 데이터 코어(목표), 레이저 장치
    /// 진입 루트: 정면(홀 하역문→서버실 정문), 우회(사무 구역 서쪽 문→복도→서버실 옆문), 높은 루트(캣워크),
    ///           낮은 루트(홀 서쪽 환기구, 창고 덕트).
    /// </summary>
    public static partial class ShadowProtocolSceneBuilder
    {
        static readonly Vector3 StartPos = new Vector3(-35.5f, 0f, -15f);
        static readonly Vector3 GoalPos = new Vector3(13.2f, 0f, 17.4f);
        const float HallH = 6f, AnnexH = 3.5f, RoomH = 3f, DeckY = 3.2f;

        class LayoutMats
        {
            public Material floor, wall, asphalt, shelf, crate, lowCrate, stripe, start, lotLine, drain, fence, lampHead, workLight, farBuilding, beam, cap, extract;
        }

        static GameObject WallX(Transform p, string name, float x1, float x2, float z, float h, Material m, float t = 1f, float y0 = 0f)
            => Box(p, name, new Vector3((x1 + x2) * 0.5f, y0 + h * 0.5f, z), new Vector3(Mathf.Abs(x2 - x1), h, t), m);

        static GameObject WallZ(Transform p, string name, float x, float z1, float z2, float h, Material m, float t = 1f, float y0 = 0f)
            => Box(p, name, new Vector3(x, y0 + h * 0.5f, (z1 + z2) * 0.5f), new Vector3(t, h, Mathf.Abs(z2 - z1)), m);

        static GameObject Ramp(Transform p, string name, Vector3 bottom, Vector3 top, float width, Material m)
        {
            Vector3 d = top - bottom;
            var go = Box(p, name, Vector3.zero, new Vector3(width, 0.15f, d.magnitude), m);
            go.transform.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
            go.transform.position = (bottom + top) * 0.5f - go.transform.up * 0.075f;
            return go;
        }

        static void Zone(Transform p, string name, Vector3 center, Vector3 size, float brightness)
        {
            var go = new GameObject("LightZone_" + name);
            go.transform.SetParent(p, false);
            go.transform.position = center;
            var z = go.AddComponent<LightZone>();
            z.size = size;
            z.brightness = brightness;
            z.label = name;
        }

        static Light PointLight(Transform p, string name, Vector3 pos, Color c, float range, float intensity, bool forcePixel)
        {
            var go = new GameObject(name);
            go.transform.SetParent(p, false);
            go.transform.position = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = c;
            l.range = range;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
            l.renderMode = forcePixel ? LightRenderMode.ForcePixel : LightRenderMode.Auto;
            return l;
        }

        static void Invisible(GameObject go) { go.GetComponent<MeshRenderer>().enabled = false; }

        static void BuildCompound(Transform lt, LayoutMats M)
        {
            // ---------- 바닥 ----------
            M.floor.mainTextureScale = new Vector2(8f, 8.1f);
            Box(lt, "Floor", new Vector3(0f, -0.5f, 4.25f), new Vector3(32f, 1f, 32.5f), M.floor);
            Box(lt, "Floor_Yard", new Vector3(-28f, -0.5f, 0f), new Vector3(24f, 1f, 40f), M.asphalt);

            // ---------- 벽 ----------
            var walls = Group(lt, "Walls");
            // (b) 메인 홀
            WallX(walls, "Wall_S", -16f, 16f, -11.5f, HallH, M.wall);
            WallZ(walls, "Wall_E_Hall", 15.5f, -12f, 12f, HallH, M.wall);
            WallZ(walls, "Wall_W_S", -15.5f, -12f, -8.5f, HallH, M.wall);
            WallZ(walls, "Lintel_Vent", -15.5f, -8.5f, -7.3f, HallH - 1.2f, M.wall, 1f, 1.2f);       // 환기구(높이 1.2m, 웅크려야 통과)
            WallZ(walls, "Wall_W_M", -15.5f, -7.3f, -3f, HallH, M.wall);
            WallZ(walls, "Lintel_LoadingDoor", -15.5f, -3f, 1f, HallH - 3.5f, M.wall, 1f, 3.5f);   // 하역문(정면)
            WallZ(walls, "Wall_W_N", -15.5f, 1f, 12f, HallH, M.wall);
            // 문 폭은 1.8m 이상(NavMesh 에이전트 반경 0.5m에서도 연결되도록)
            WallX(walls, "Wall_N_W", -16f, -6.2f, 11.5f, HallH, M.wall);
            WallX(walls, "Lintel_OfficeDoor", -6.2f, -4.4f, 11.5f, HallH - 2.4f, M.wall, 1f, 2.4f);  // 홀→사무 복도
            WallX(walls, "Wall_N_M", -4.4f, 9f, 11.5f, HallH, M.wall);
            WallX(walls, "Lintel_VaultDoor", 9f, 11f, 11.5f, HallH - 2.6f, M.wall, 1f, 2.6f);      // 홀→서버실 정문
            WallX(walls, "Wall_N_E", 11f, 16f, 11.5f, HallH, M.wall);
            Box(walls, "Partition_S", new Vector3(1f, 1.5f, -8.5f), new Vector3(1f, 3f, 5f), M.wall);
            Box(walls, "Partition_M", new Vector3(1f, 1.5f, -2f), new Vector3(1f, 3f, 4f), M.wall);
            Box(walls, "Partition_N", new Vector3(1f, 1.5f, 8f), new Vector3(1f, 3f, 6f), M.wall);
            // (c) 사무 구역 / (d) 서버실
            WallX(walls, "Wall_Annex_N", -16f, 16f, 20f, AnnexH, M.wall);
            WallZ(walls, "Wall_E_Annex", 15.5f, 12f, 20.5f, AnnexH, M.wall);
            WallZ(walls, "Wall_Office_W", -15.5f, 13.8f, 20.5f, AnnexH, M.wall);
            WallZ(walls, "Lintel_OfficeWestDoor", -15.5f, 12f, 13.8f, AnnexH - 2.4f, M.wall, 1f, 2.4f); // 야드→사무 복도(우회)
            WallX(walls, "Partition_Corr_1", -15f, -12.2f, 14.25f, RoomH, M.wall, 0.5f);   // 경비실 문 -12.2..-10.4
            WallX(walls, "Partition_Corr_2", -10.4f, -4.2f, 14.25f, RoomH, M.wall, 0.5f);  // 사무실 문 -4.2..-2.4
            WallX(walls, "Partition_Corr_3", -2.4f, 1.8f, 14.25f, RoomH, M.wall, 0.5f);   // 창고 문 1.8..3.6
            WallX(walls, "Partition_Corr_4", 3.6f, 5f, 14.25f, RoomH, M.wall, 0.5f);
            WallZ(walls, "Partition_Room_1", -7f, 14.5f, 19.5f, RoomH, M.wall, 0.5f);
            WallZ(walls, "Partition_Room_2", 0.5f, 14.5f, 19.5f, RoomH, M.wall, 0.5f);
            WallZ(walls, "Wall_Vault_W1", 5.5f, 13.8f, 18f, AnnexH, M.wall);
            WallZ(walls, "Wall_Vault_W2", 5.5f, 19.2f, 19.5f, AnnexH, M.wall);
            WallZ(walls, "Lintel_VaultSideDoor", 5.5f, 12f, 13.8f, AnnexH - 2.4f, M.wall, 1f, 2.4f);   // 복도→서버실 옆문
            WallZ(walls, "Lintel_Duct", 5.5f, 18f, 19.2f, AnnexH - 1.2f, M.wall, 1f, 1.2f);            // 창고→서버실 덕트(웅크려야 통과)
            // (a) 야드 경계(보이지 않는 충돌벽, 펜스는 시각용으로 따로)
            Invisible(WallZ(walls, "Bound_W", -40.5f, -21f, 21f, 3f, M.wall));
            Invisible(WallX(walls, "Bound_S", -41f, -15f, -20.5f, 3f, M.wall));
            Invisible(WallX(walls, "Bound_N", -41f, -15f, 20.5f, 3f, M.wall));
            Invisible(WallZ(walls, "Bound_SE", -15.5f, -21f, -12f, 3f, M.wall));

            // ---------- 캣워크(높은 루트) ----------
            var cw = Group(lt, "Catwalk");
            // 데크 z -11..-9.2(폭 1.8m), 경사로 폭 2m(NavMesh 에이전트 반경 0.5m를 고려)
            if (LoadEnv("catwalk_section") != null)
            {
                // VARCO 캐트워크 모듈(폭 0.99m, 양쪽 난간)을 Z 1.4배(1.39m)로 쓴다. 가운데 구간(x -12..13) 데크는 모듈 폭에 맞추고
                // 북쪽 난간 = 기존 RailCollider 선(z -9.23), 남쪽 난간(z -10.62)에는 보이지 않는 충돌벽. 경사로가 붙는 양 끝 착지부는 기존 폭(1.8m).
                Box(cw, "Deck", new Vector3(0.5f, DeckY - 0.075f, (CatwalkZS + -9.2f) * 0.5f), new Vector3(25f, 0.15f, -9.2f - CatwalkZS), M.fence);
                Box(cw, "Deck_W", new Vector3(-13f, DeckY - 0.075f, -10.1f), new Vector3(2f, 0.15f, 1.8f), M.fence);
                Box(cw, "Deck_E", new Vector3(14f, DeckY - 0.075f, -10.1f), new Vector3(2f, 0.15f, 1.8f), M.fence);
                Invisible(Box(cw, "RailCollider_S", new Vector3(0.5f, DeckY + 0.5f, CatwalkZS + 0.03f), new Vector3(25f, 1f, 0.06f), M.fence));
                foreach (float endX in new[] { -12f, 13f })
                    Invisible(Box(cw, "RailCollider_End", new Vector3(endX, DeckY + 0.5f, (CatwalkZS - 11f) * 0.5f), new Vector3(0.06f, 1f, CatwalkZS + 11f), M.fence));
            }
            else
                Box(cw, "Deck", new Vector3(0.5f, DeckY - 0.075f, -10.1f), new Vector3(29f, 0.15f, 1.8f), M.fence);
            Ramp(cw, "Ramp_W", new Vector3(-13f, 0f, -2.5f), new Vector3(-13f, DeckY, -9.2f), 2f, M.fence);
            Ramp(cw, "Ramp_E", new Vector3(14f, 0f, -2.5f), new Vector3(14f, DeckY, -9.2f), 2f, M.fence);
            Invisible(Box(cw, "RailCollider", new Vector3(0.5f, DeckY + 0.5f, -9.23f), new Vector3(25f, 1f, 0.06f), M.fence));
            Box(cw, "Rail_Top", new Vector3(0.5f, DeckY + 1.0f, -9.23f), new Vector3(25f, 0.05f, 0.05f), M.fence, false);
            Box(cw, "Rail_Mid", new Vector3(0.5f, DeckY + 0.5f, -9.23f), new Vector3(25f, 0.04f, 0.04f), M.fence, false);
            for (float x = -12f; x <= 13.01f; x += 2.5f)
                Box(cw, "RailPost", new Vector3(x, DeckY + 0.5f, -9.23f), new Vector3(0.05f, 1f, 0.05f), M.fence, false);
            for (float x = -11f; x <= 13f; x += 6f)
                Box(cw, "Support", new Vector3(x, (DeckY - 0.15f) * 0.5f, -9.35f), new Vector3(0.15f, DeckY - 0.15f, 0.15f), M.fence, false);

            // ---------- 엄폐물(그레이박스 → 소품으로 치장) ----------
            var cover = Group(lt, "Cover");
            Box(cover, "Shelf_A", new Vector3(-9f, 1.1f, 2f), new Vector3(1f, 2.2f, 8f), M.shelf);
            Box(cover, "Shelf_B", new Vector3(-4f, 1.1f, -1f), new Vector3(1f, 2.2f, 8f), M.shelf);
            Box(cover, "Crate_W1", new Vector3(-12f, 0.6f, 1f), new Vector3(1.5f, 1.2f, 1.5f), M.lowCrate);
            Box(cover, "Crate_W2", new Vector3(-11f, 0.6f, -4.5f), new Vector3(1.5f, 1.2f, 1.5f), M.lowCrate);
            Box(cover, "Crate_W3", new Vector3(-11.5f, 0.75f, 8.5f), new Vector3(1.5f, 1.5f, 1.5f), M.crate);
            Box(cover, "Shelf_Start", new Vector3(-10f, 1.1f, -6.75f), new Vector3(1f, 2.2f, 4.5f), M.shelf);
            Box(cover, "Crate_E1", new Vector3(5f, 0.75f, -7f), new Vector3(1.5f, 1.5f, 1.5f), M.crate);
            Box(cover, "Crate_E2", new Vector3(5f, 0.75f, 6f), new Vector3(2f, 1.5f, 2f), M.crate);
            Box(cover, "Crate_E3", new Vector3(9f, 0.6f, -5f), new Vector3(1.2f, 1.2f, 1.2f), M.lowCrate);
            Box(cover, "Crate_E4", new Vector3(10f, 0.75f, 7f), new Vector3(1.5f, 1.5f, 1.5f), M.crate);
            Box(cover, "Shelf_C", new Vector3(9.5f, 1.1f, 1f), new Vector3(5f, 2.2f, 1f), M.shelf);
            Box(cover, "Shelf_D", new Vector3(12f, 1.1f, -6f), new Vector3(1f, 2.2f, 5f), M.shelf);
            Box(cover, "Crate_V1", new Vector3(8.6f, 0.6f, 15.2f), new Vector3(1.5f, 1.2f, 1.5f), M.lowCrate); // 서버실 낮은 엄폐
            Box(cover, "Crate_O1", new Vector3(-5.8f, 0.6f, 18.8f), new Vector3(1.2f, 1.2f, 1.2f), M.lowCrate); // 사무실 낮은 엄폐(문에서 떨어진 구석)

            DressLevel(walls, cover, M.cap);
            BuildSP2Props2(lt, M.beam);

            // ---------- 장식(충돌 없음) ----------
            var decor = new GameObject("Decor").transform;
            Box(decor, "Stripe_GapN", new Vector3(1f, 0.005f, 2.5f), new Vector3(0.15f, 0.01f, 5f), M.stripe, false);
            Box(decor, "Stripe_GapS", new Vector3(1f, 0.005f, -5f), new Vector3(0.15f, 0.01f, 2f), M.stripe, false);
            Box(decor, "Stripe_LoadingDoor", new Vector3(-14.4f, 0.005f, -1f), new Vector3(0.15f, 0.01f, 4f), M.stripe, false);
            Box(decor, "Stripe_VaultDoor", new Vector3(10f, 0.005f, 10.4f), new Vector3(2f, 0.01f, 0.15f), M.stripe, false);
            var startPad = Box(decor, "StartPad", StartPos + new Vector3(0f, 0.01f, 0f), new Vector3(2f, 0.02f, 2f), M.start, false);
            startPad.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            startPad.GetComponent<Renderer>().receiveShadows = false;

            // ---------- 탈출 지점 ----------
            var ex = new GameObject("Extraction_Point");
            ex.transform.position = StartPos;
            var ez = ex.AddComponent<ExtractionZone>();
            ez.radius = 2.2f;
            var marker = Box(ex.transform, "Marker", new Vector3(0f, 0.03f, 0f), new Vector3(3.2f, 0.02f, 3.2f), M.extract, false);
            marker.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            marker.GetComponent<Renderer>().receiveShadows = false;
            ez.marker = marker.GetComponent<Renderer>();
            ez.beacon = PointLight(ex.transform, "Beacon", StartPos + Vector3.up * 2.5f, new Color(0.3f, 1f, 0.5f), 7f, 2.2f, false);

            BuildEnvironment2(M);
        }

        // 바깥 부지·펜스·가로등·조명·밝기 구역
        static void BuildEnvironment2(LayoutMats M)
        {
            var env = new GameObject("Environment").transform;
            var ground = Primitive(PrimitiveType.Plane, env, "OutdoorGround", new Vector3(-10f, -0.05f, 0f), new Vector3(14f, 1f, 14f), M.asphalt);
            ground.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

            // 펜스(시각용). 야드 쪽은 보이지 않는 경계벽과 같은 선.
            var fence = Group(env, "Fence");
            FenceSides.Clear();
            System.Action<Vector3, Vector3> side = (a, b) =>
            {
                FenceSides.Add((a, b)); // 환경 모듈(Env_fence_section) 교체용
                float len = Vector3.Distance(a, b);
                int posts = Mathf.CeilToInt(len / 4f);
                for (int i = 0; i <= posts; i++)
                    Primitive(PrimitiveType.Cylinder, fence, "Post", Vector3.Lerp(a, b, i / (float)posts) + new Vector3(0f, 1.25f, 0f), new Vector3(0.09f, 1.25f, 0.09f), M.fence);
                Vector3 mid = (a + b) * 0.5f;
                bool alongX = Mathf.Abs(b.x - a.x) > Mathf.Abs(b.z - a.z);
                foreach (float h in new[] { 0.5f, 1.3f, 2.1f, 2.45f })
                    Box(fence, "Wire", mid + new Vector3(0f, h, 0f), alongX ? new Vector3(len, 0.025f, 0.025f) : new Vector3(0.025f, 0.025f, len), M.fence, false);
            };
            side(new Vector3(-40.4f, 0f, -20.4f), new Vector3(-40.4f, 0f, 20.4f));
            side(new Vector3(-40.4f, 0f, -20.4f), new Vector3(-16f, 0f, -20.4f));
            side(new Vector3(-40.4f, 0f, 20.4f), new Vector3(-16f, 0f, 20.4f));
            side(new Vector3(-16f, 0f, -20.4f), new Vector3(-16f, 0f, -12f));
            side(new Vector3(-16f, 0f, -20.4f), new Vector3(19f, 0f, -20.4f));
            side(new Vector3(19f, 0f, -20.4f), new Vector3(19f, 0f, 22f));
            side(new Vector3(-16f, 0f, 22f), new Vector3(19f, 0f, 22f));

            // 주차선·배수구(야드)
            var lot = Group(env, "ParkingLines");
            for (int i = 0; i < 4; i++)
                Box(lot, "Line", new Vector3(-26.5f + i * 2.8f, 0.005f, -17.8f), new Vector3(0.1f, 0.01f, 3.6f), M.lotLine, false);
            Box(lot, "Edge", new Vector3(-22.3f, 0.005f, -15.9f), new Vector3(9f, 0.01f, 0.12f), M.lotLine, false);
            foreach (var p in new[] { new Vector3(-28f, 0f, -11.5f), new Vector3(-20f, 0f, 7.5f), new Vector3(-34f, 0f, 18f) })
            {
                Box(lot, "Drain", p + new Vector3(0f, 0.004f, 0f), new Vector3(0.7f, 0.01f, 0.7f), M.drain, false);
                for (int k = -2; k <= 2; k++)
                    Box(lot, "Grate", p + new Vector3(k * 0.13f, 0.008f, 0f), new Vector3(0.03f, 0.01f, 0.6f), M.fence, false);
            }

            // 가로등(야드) + 밝은 구역
            var lamps = Group(env, "StreetLamps");
            var zones = Group(env, "LightZones");
            foreach (var p in new[] { new Vector3(-39.6f, 0f, -9f), new Vector3(-39.6f, 0f, 9f), new Vector3(-17f, 0f, -19.6f), new Vector3(-17f, 0f, 19.6f), new Vector3(-29f, 0f, -19.6f) })
            {
                var lamp = new GameObject("Lamp").transform;
                lamp.SetParent(lamps, false);
                lamp.position = p;
                Primitive(PrimitiveType.Cylinder, lamp, "Pole", new Vector3(0f, 2.5f, 0f), new Vector3(0.14f, 2.5f, 0.14f), M.fence);
                Primitive(PrimitiveType.Cube, lamp, "Head", new Vector3(0f, 5.05f, 0f), new Vector3(0.6f, 0.12f, 0.35f), M.lampHead).GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                PointLight(lamp, "Light", p + Vector3.up * 4.7f, new Color(1f, 0.74f, 0.45f), 13f, 1.8f, false);
                Zone(zones, "YardLamp", p + new Vector3(0f, 1.5f, 0f), new Vector3(9f, 3f, 9f), 1f);
            }

            // 먼 건물 실루엣
            var far = Group(env, "FarBuildings");
            Box(far, "Bldg", new Vector3(-58f, 5f, 0f), new Vector3(10f, 10f, 30f), M.farBuilding, false);
            Box(far, "Bldg", new Vector3(-30f, 4f, 36f), new Vector3(20f, 8f, 8f), M.farBuilding, false);
            Box(far, "Bldg", new Vector3(8f, 6f, 40f), new Vector3(16f, 12f, 10f), M.farBuilding, false);
            Box(far, "Bldg", new Vector3(36f, 3.5f, 2f), new Vector3(8f, 7f, 24f), M.farBuilding, false);
            Box(far, "Bldg", new Vector3(-6f, 4f, -36f), new Vector3(26f, 8f, 8f), M.farBuilding, false);

            // 메인 홀 천장 트러스(벽 높이 6m 바로 아래, 캣워크·3인칭 시야 위) + 작업등
            var work = Group(env, "WorkLights");
            foreach (float bz in new[] { -6f, 5.5f })
                Box(work, "TrussBeam", new Vector3(0f, 5.85f, bz), new Vector3(31f, 0.1f, 0.16f), M.fence, false).GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            foreach (var p in new[] { new Vector3(-10f, 0f, -6f), new Vector3(-2f, 0f, -6f), new Vector3(5f, 0f, -6f), new Vector3(12f, 0f, -6f), new Vector3(-10f, 0f, 5.5f), new Vector3(-2f, 0f, 5.5f), new Vector3(5f, 0f, 5.5f), new Vector3(12f, 0f, 5.5f) })
            {
                Box(work, "Fixture", new Vector3(p.x, 5.77f, p.z), new Vector3(0.9f, 0.05f, 0.22f), M.workLight, false).GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                PointLight(work, "Light", new Vector3(p.x, 5.5f, p.z), new Color(1f, 0.93f, 0.8f), 11f, 1.5f, true);
            }

            // 사무 구역(밝음)·서버실(어둡고 차가움) 벽등
            var annex = Group(env, "AnnexLights");
            System.Action<Vector3, Color, float, float> wallLamp = (pos, c, range, inten) =>
            {
                Box(annex, "WallLamp", pos, new Vector3(0.6f, 0.08f, 0.12f), M.workLight, false).GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                PointLight(annex, "Light", pos + new Vector3(0f, -0.2f, 0f), c, range, inten, true);
            };
            wallLamp(new Vector3(-11f, 2.7f, 19.43f), new Color(1f, 0.95f, 0.85f), 7f, 1.6f);   // 경비실
            wallLamp(new Vector3(-3.3f, 2.7f, 19.43f), new Color(1f, 0.95f, 0.85f), 7f, 1.3f);  // 사무실
            wallLamp(new Vector3(-8f, 2.7f, 12.07f), new Color(1f, 0.95f, 0.85f), 7f, 1.2f);    // 복도 서쪽
            wallLamp(new Vector3(1f, 2.7f, 12.07f), new Color(1f, 0.95f, 0.85f), 7f, 1.2f);     // 복도 동쪽
            wallLamp(new Vector3(10.5f, 3f, 19.43f), new Color(0.55f, 0.75f, 1f), 9f, 0.9f);   // 서버실

            // 밝기 구역(경비병 시야 거리 보정)
            Zone(zones, "Yard", new Vector3(-28f, 1.5f, 0f), new Vector3(24f, 3f, 40f), 0.45f);
            Zone(zones, "Hall", new Vector3(0f, 1.5f, 0f), new Vector3(32f, 3f, 24f), 0.8f);
            Zone(zones, "Catwalk", new Vector3(0.5f, DeckY + 1.5f, -10.1f), new Vector3(30f, 3f, 2.2f), 0.4f);
            Zone(zones, "Corridor", new Vector3(-5f, 1.5f, 13f), new Vector3(21f, 3f, 2.2f), 0.85f);
            Zone(zones, "GuardRoom", new Vector3(-11f, 1.5f, 17f), new Vector3(8f, 3f, 5f), 1f);
            Zone(zones, "Office", new Vector3(-3.25f, 1.5f, 17f), new Vector3(6.5f, 3f, 5f), 0.9f);
            Zone(zones, "Storage", new Vector3(2.75f, 1.5f, 17f), new Vector3(4.5f, 3f, 5f), 0.45f);
            Zone(zones, "Vault", new Vector3(10.5f, 1.5f, 16f), new Vector3(9f, 3f, 7.5f), 0.6f);
            Zone(zones, "CoreSpot", GoalPos + new Vector3(0f, 1.5f, 0f), new Vector3(3f, 3f, 3f), 1f);
        }

        // 확장 맵용 VARCO 소품 배치(레벨 안은 BoxCollider = NavMesh 장애물)
        static void BuildSP2Props2(Transform level, Material beamMat)
        {
            var inside = new GameObject("Props2").transform;
            inside.SetParent(level, false);
            var deco = new GameObject("DecorProps").transform;
            var cams = new GameObject("SecurityCameras").transform;

            // (a) 야드: 컨테이너 미로·지게차·팔레트
            PlaceProp(inside, "container", "Container_Y1", new Vector3(-31f, 0f, -15.5f), 90f, true, null, 0f);
            PlaceProp(inside, "container", "Container_Y2", new Vector3(-24f, 0f, -8f), 0f, true, null, 0f);
            PlaceProp(inside, "container", "Container_Y3", new Vector3(-34f, 0f, -4f), 0f, true, null, 0f);
            PlaceProp(inside, "container", "Container_Y4", new Vector3(-29f, 0f, 4f), 90f, true, null, 0f);
            PlaceProp(inside, "container", "Container_Y5", new Vector3(-24.5f, 0f, 12.5f), 0f, true, null, 0f);
            PlaceProp(inside, "container", "Container_Y6", new Vector3(-35.5f, 0f, 13f), 90f, true, null, 0f);
            PlaceProp(inside, "forklift", "Forklift_Y", new Vector3(-37.5f, 0f, 4f), 0f, true, null, 0f);
            PlaceProp(inside, "pallets", "Pallets_Y1", new Vector3(-20.5f, 0f, -16f), 0f, true, null, 0f);
            PlaceProp(inside, "pallets", "Pallets_Y2", new Vector3(-32.5f, 0f, 9f), 0f, true, null, 0f);
            PlaceProp(inside, "drums", "Drums_Y", new Vector3(-19.5f, 0f, 5f), 0f, true, null, 0f);
            PlaceProp(inside, "tarp", "Tarp_Y", new Vector3(-26.5f, 0f, -13.5f), 0f, true, null, 0f);
            PlaceProp(cams, "camera", "Cam_YardDoor", new Vector3(-16.2f, 3.8f, -1f), -90f, false, "+x", -16f);

            // (b) 메인 홀
            PlaceProp(inside, "drums", "Drums_W", new Vector3(-14.4f, 0f, 5.5f), 0f, true, "-x", -15f);
            PlaceProp(inside, "drums", "Drums_S", new Vector3(-4f, 0f, -10.4f), 0f, true, "-z", -11f);
            PlaceProp(inside, "pallets", "Pallets_SE", new Vector3(14.4f, 0f, -10.2f), 0f, true, "+x", 15f);
            PlaceProp(inside, "tarp", "Tarp_NW", new Vector3(-9.5f, 0f, 10.4f), 0f, true, "+z", 11f);
            PlaceProp(inside, "tarp", "Tarp_NE", new Vector3(7.5f, 0f, 10.4f), 0f, true, "+z", 11f);
            PlaceProp(inside, "lockers", "Lockers_Partition", new Vector3(1.8f, 0f, 8.5f), 90f, true, "-x", 1.5f);
            PlaceProp(inside, "server", "Server_H1", new Vector3(14.6f, 0f, 6.5f), -90f, true, "+x", 15f);
            PlaceProp(inside, "server", "Server_H2", new Vector3(14.6f, 0f, 7.35f), -90f, true, "+x", 15f);
            PlaceProp(cams, "camera", "Cam_HallLoading", new Vector3(-14.8f, 3.8f, -1f), 90f, false, "-x", -15f);
            PlaceProp(cams, "camera", "Cam_VaultDoor", new Vector3(10f, 2.9f, 10.8f), 180f, false, "+z", 11f);

            // (c) 사무 구역
            PlaceProp(inside, "desk", "GuardDesk", new Vector3(-11f, 0f, 19.3f), 180f, true, "+z", 19.5f);
            PlaceProp(inside, "lockers", "Lockers_GuardRoom", new Vector3(-14.6f, 0f, 16.6f), 90f, true, "-x", -15f);
            PlaceProp(inside, "desk", "OfficeDesk", new Vector3(-3.3f, 0f, 19.3f), 180f, true, "+z", 19.5f);
            PlaceProp(inside, "lockers", "Lockers_Office", new Vector3(-0.1f, 0f, 17.2f), -90f, true, "+x", 0.25f);
            PlaceProp(inside, "pallets", "Pallets_Storage", new Vector3(1.4f, 0f, 15.6f), 0f, true, "-x", 0.75f);
            PlaceProp(inside, "drums", "Drums_Storage", new Vector3(4.4f, 0f, 15.5f), 0f, true, "+x", 5f);
            PlaceProp(inside, "tarp", "Tarp_Storage", new Vector3(2.4f, 0f, 18.9f), 0f, true, "+z", 19.5f);
            PlaceProp(cams, "camera", "Cam_GuardRoom", new Vector3(-12.6f, 2.4f, 19.3f), 180f, false, "+z", 19.5f);
            PlaceProp(cams, "camera", "Cam_Corridor", new Vector3(4.8f, 2.6f, 13f), -90f, false, "+x", 5f);

            // (d) 서버실
            for (int i = 0; i < 4; i++)
                PlaceProp(inside, "server", "Server_V" + (i + 1), new Vector3(7f + i * 0.85f, 0f, 19.2f), 180f, true, "+z", 19.5f);
            PlaceProp(inside, "lockers", "Lockers_Vault", new Vector3(14.6f, 0f, 13.6f), -90f, true, "+x", 15f);
            PlaceProp(cams, "camera", "Cam_Vault", new Vector3(14.8f, 2.6f, 16f), -90f, false, "+x", 15f);
            // (예전 장식용 레이저는 퍼즐 레이저 격자(ShadowProtocolScenePuzzles)로 대체)
            GameObject la = null, lb = null;
            if (la != null && lb != null)
            {
                Vector3 a = la.transform.position + Vector3.up * 0.45f, b = lb.transform.position + Vector3.up * 0.45f;
                var beam = Box(deco, "LaserBeam", (a + b) * 0.5f, new Vector3(0.015f, 0.015f, Vector3.Distance(a, b) - 0.3f), beamMat, false);
                beam.transform.rotation = Quaternion.LookRotation(b - a);
                var br = beam.GetComponent<Renderer>(); br.shadowCastingMode = ShadowCastingMode.Off; br.receiveShadows = false;
                foreach (var dev in new[] { la, lb })
                {
                    var l = PointLight(dev.transform, "RedGlow", dev.transform.position + Vector3.up * 0.5f, new Color(1f, 0.15f, 0.1f), 1.6f, 1.2f, false);
                    l.transform.SetParent(dev.transform, true);
                }
            }
        }

        // 경비병 6명: 야드 정찰형, 홀 순찰형 3, 서버실 정문 고정 감시형, 사무 복도 순찰형
        static void BuildGuards2(GuardBuildContext ctx)
        {
            var yard = BuildGuard(ctx, "Guard_Yard", new[]
            {
                new Vector3(-19f, 0f, -17f), new Vector3(-19f, 0f, 17f), new Vector3(-31f, 0f, 17f), new Vector3(-28f, 0f, -9f)
            }, null);
            yard.patrolSpeed = 1.9f; yard.waitAtPoint = 0.8f; yard.viewDistance = 11f;

            BuildGuard(ctx, "Guard_West", new[]
            {
                new Vector3(-6.5f, 0f, 7.5f), new Vector3(-6.5f, 0f, -6.5f), new Vector3(-1.5f, 0f, -6.5f), new Vector3(-1.5f, 0f, 7.5f)
            }, null);
            BuildGuard(ctx, "Guard_Center", new[]
            {
                new Vector3(3f, 0f, 4f), new Vector3(3f, 0f, -9.5f), new Vector3(9.5f, 0f, -9.5f)
            }, "super");
            BuildGuard(ctx, "Guard_Vault", new[]
            {
                // 캐워크 동쪽 출구(경사로 x 14)에서 떨어진 삼각 루프
                new Vector3(6.3f, 0f, 4f), new Vector3(6.3f, 0f, -3.5f), new Vector3(9.2f, 0f, -1.6f)
            }, "heavy");

            // 서버실 정문 앞 고정 감시형: 홀 서남쪽을 훑는다(동쪽 벽을 따라 등 뒤로 접근하는 틈이 있다)
            var door = BuildGuard(ctx, "Guard_Door", new[] { new Vector3(8.5f, 0f, 9.6f) }, "heavy");
            door.stationary = true; door.scanAngle = 45f; door.scanSpeed = 0.55f; door.viewDistance = 9f;
            door.patrolPoints[0].rotation = Quaternion.Euler(0f, 240f, 0f);
            door.transform.rotation = Quaternion.Euler(0f, 240f, 0f);

            var office = BuildGuard(ctx, "Guard_Office", new[] { new Vector3(-1f, 0f, 13.1f), new Vector3(2.7f, 0f, 17f) }, "super");  // 복도 중앙 ↔ 창고 안(창고에서 북쪽을 볼 때 복도 동쪽이 빈다)
            office.patrolSpeed = 1.3f; office.waitAtPoint = 3f; office.viewDistance = 9f;
        }
    }
}
