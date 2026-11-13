using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadowProtocol.EditorTools
{
    /// <summary>
    /// C6 퍼즐: 서버실 보안문 키패드(코드 조각 4개), 창고 배전반(3×3 Lights Out → 덕트 환풍기 정지·조명 차단),
    /// 데이터 코어 앞 레이저 격자(순서대로 꺼지는 빔 4줄).
    /// 모든 진입 경로가 퍼즐을 하나 이상 거친다: 정면·캣워크·환기구·하역문 → 정문 키패드, 사무동 → 옆문 키패드, 덕트 → 배전반,
    /// 그리고 모든 경로가 레이저 격자를 지난다.
    /// </summary>
    public static partial class ShadowProtocolSceneBuilder
    {
        static TextMesh WorldText(Transform parent, string text, Vector3 localPos, Quaternion rot, float size, Color c)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = rot;
            var tm = go.AddComponent<TextMesh>();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tm.font = font;
            tm.text = text;
            tm.fontSize = 64;
            tm.characterSize = size;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = c;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = font.material;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            return tm;
        }

        static void BuildPuzzles(PlayerController pc, GuardDirector dir)
        {
            var root = new GameObject("Puzzles").transform;
            var mDoor = Mat("SecurityDoor", new Color(0.24f, 0.26f, 0.3f));
            var mLampRed = Mat("DoorLamp", new Color(1f, 0.25f, 0.2f), 2f);
            var mPanel = Mat("KeypadBody", new Color(0.12f, 0.13f, 0.15f));
            var mScreen = Mat("KeypadScreen", new Color(0.2f, 0.7f, 1f), 1.6f);
            var mNote = Mat("NotePaper", new Color(0.92f, 0.9f, 0.82f));
            var mMarker = Mat("NoteMarker", new Color(1f, 0.85f, 0.35f), 1.4f);
            var mBeam = Mat("LaserBeam", new Color(1f, 0.1f, 0.08f), 3f);

            // ---------- 보안문 + 키패드 ----------
            SecurityDoor MakeDoor(string name, Vector3 pos, Vector3 size, Vector3 openOffset, Vector3 lampPos)
            {
                var d = new GameObject(name).transform;
                d.SetParent(root, false);
                d.position = new Vector3(pos.x, 0f, pos.z);
                var panel = Box(d, "Panel", new Vector3(0f, pos.y, 0f), size, mDoor);
                var lamp = Box(d, "Lamp", lampPos - d.position, new Vector3(0.18f, 0.08f, 0.18f), mLampRed, false);
                lamp.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                var sd = d.gameObject.AddComponent<SecurityDoor>();
                sd.panel = panel.transform;
                sd.blocker = panel.GetComponent<Collider>();
                sd.openOffset = openOffset;
                sd.lamp = lamp.GetComponent<Renderer>();
                return sd;
            }
            var frontDoor = MakeDoor("SecurityDoor_Front", new Vector3(10f, 1.3f, 11.5f), new Vector3(2f, 2.6f, 0.12f), new Vector3(1.95f, 0f, 0f), new Vector3(10f, 2.75f, 10.92f));
            var sideDoor = MakeDoor("SecurityDoor_Side", new Vector3(5.5f, 1.2f, 12.9f), new Vector3(0.12f, 2.4f, 1.8f), new Vector3(0f, 0f, -1.75f), new Vector3(4.92f, 2.55f, 12.9f));

            KeypadPanel MakeKeypad(string name, string id, string label, Vector3 pos, float yaw, SecurityDoor door)
            {
                var k = new GameObject(name).transform;
                k.SetParent(root, false);
                k.position = pos;
                k.rotation = Quaternion.Euler(0f, yaw, 0f);
                Box(k, "Body", Vector3.zero, new Vector3(0.32f, 0.46f, 0.06f), mPanel, false);
                var scr = Box(k, "Screen", new Vector3(0f, 0.12f, 0.032f), new Vector3(0.24f, 0.1f, 0.01f), mScreen, false);
                scr.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                for (int i = 0; i < 4; i++)
                {
                    var c = Box(k, "Color" + i, new Vector3(-0.09f + i * 0.06f, -0.02f, 0.032f), new Vector3(0.045f, 0.045f, 0.01f), Mat("KeypadColor" + i, PuzzleState.Colors[i], 1.2f), false);
                    c.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                }
                var kp = k.gameObject.AddComponent<KeypadPanel>();
                kp.doors = new[] { door };
                kp.puzzleId = id;
                kp.puzzleLabel = label;
                kp.radius = 1.4f;
                kp.screen = scr.GetComponent<Renderer>();
                return kp;
            }
            MakeKeypad("Keypad_Front", "Keypad_Front", "서버실 정문 키패드", new Vector3(11.5f, 1.35f, 10.95f), 180f, frontDoor);
            MakeKeypad("Keypad_Side", "Keypad_Side", "서버실 옆문 키패드", new Vector3(4.55f, 1.35f, 12.04f), 0f, sideDoor);

            // ---------- 코드 조각 ----------
            void MakeNote(int color, string place, Vector3 pos, float yaw, bool board)
            {
                var n = new GameObject("CodeNote_" + PuzzleState.ColorNames[color]).transform;
                n.SetParent(root, false);
                n.position = pos;
                n.rotation = Quaternion.Euler(0f, yaw, 0f);
                var paper = Box(n, board ? "Whiteboard" : "Paper", Vector3.zero, board ? new Vector3(1.2f, 0.8f, 0.03f) : new Vector3(0.24f, 0.3f, 0.01f), mNote, false);
                paper.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                var stripe = Box(n, "ColorTag", new Vector3(0f, board ? 0.34f : 0.12f, 0.012f), new Vector3(board ? 1.1f : 0.22f, 0.04f, 0.005f), Mat("KeypadColor" + color, PuzzleState.Colors[color], 1.2f), false);
                stripe.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                var tm = WorldText(n, PuzzleState.Icons[color] + " ?", new Vector3(0f, -0.02f, 0.02f), Quaternion.Euler(0f, 180f, 0f), board ? 0.025f : 0.012f, PuzzleState.Colors[color]);
                var marker = Primitive(PrimitiveType.Sphere, n, "Marker", new Vector3(0f, board ? 0.55f : 0.28f, 0.05f), Vector3.one * 0.06f, mMarker);
                marker.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                var note = n.gameObject.AddComponent<CodeNote>();
                note.color = color;
                note.place = place;
                note.display = tm;
                note.marker = marker.GetComponent<Renderer>();
                note.puzzleId = "Notes";
                note.puzzleLabel = "코드 조각 쪽지";
                note.radius = 1.4f;
            }
            MakeNote(0, "경비실 CCTV 모니터 메모", new Vector3(-11f, 1.25f, 18.75f), 180f, false);
            MakeNote(1, "사무실 사물함 안 쪽지", new Vector3(-0.42f, 1.15f, 17.2f), 270f, false);
            MakeNote(2, "사무실 화이트보드", new Vector3(-4.8f, 1.6f, 14.52f), 0f, true);
            MakeNote(3, "창고 상자 위 메모", new Vector3(2.4f, 1.05f, 18.25f), 180f, false);

            // ---------- 배전반 + 덕트 환풍기 ----------
            var fanT = new GameObject("DuctFan").transform;
            fanT.SetParent(root, false);
            fanT.position = new Vector3(5.5f, 0.6f, 18.6f);
            var fanCol = fanT.gameObject.AddComponent<BoxCollider>();
            fanCol.size = new Vector3(1.05f, 1.2f, 1.2f);
            var blades = new GameObject("Blades").transform;
            blades.SetParent(fanT, false);
            blades.localPosition = new Vector3(-0.42f, 0f, 0f);
            var mFan = Mat("FanBlade", new Color(0.45f, 0.47f, 0.5f));
            Box(blades, "BladeA", Vector3.zero, new Vector3(0.04f, 1.05f, 0.16f), mFan, false);
            Box(blades, "BladeB", Vector3.zero, new Vector3(0.04f, 0.16f, 1.05f), mFan, false);
            Box(fanT, "Grille", new Vector3(-0.47f, 0f, 0f), new Vector3(0.02f, 1.15f, 1.15f), Mat("FanGrille", new Color(0.2f, 0.21f, 0.23f)), false).GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            var fan = fanT.gameObject.AddComponent<DuctFan>();
            fan.blades = blades;
            fan.blocker = fanCol;

            var br = new GameObject("BreakerPanel").transform;
            br.SetParent(root, false);
            br.position = new Vector3(4.93f, 1.4f, 16.3f);
            br.rotation = Quaternion.Euler(0f, 270f, 0f);
            Box(br, "Cabinet", Vector3.zero, new Vector3(0.55f, 0.75f, 0.1f), Mat("BreakerCabinet", new Color(0.35f, 0.36f, 0.3f)), false);
            for (int i = 0; i < 9; i++)
                Box(br, "Switch" + i, new Vector3(-0.15f + (i % 3) * 0.15f, 0.15f - (i / 3) * 0.15f, 0.06f), new Vector3(0.07f, 0.09f, 0.03f), mPanel, false).GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            var brLamp = Box(br, "Lamp", new Vector3(0.2f, 0.32f, 0.06f), new Vector3(0.05f, 0.05f, 0.02f), Mat("BreakerLamp", new Color(1f, 0.6f, 0.1f), 2f), false);
            var bp = br.gameObject.AddComponent<BreakerPanel>();
            bp.fan = fan;
            bp.lamp = brLamp.GetComponent<Renderer>();
            bp.puzzleId = "Breaker";
            bp.puzzleLabel = "창고 배전반";
            bp.radius = 1.4f;
            var offLights = new List<Light>();
            var annex = GameObject.Find("Environment/AnnexLights");
            if (annex != null) foreach (var l in annex.GetComponentsInChildren<Light>()) if (l.transform.position.x > 5.5f) offLights.Add(l);
            bp.lightsOff = offLights.ToArray();
            var zones = new List<LightZone>();
            foreach (var z in Object.FindObjectsByType<LightZone>(FindObjectsSortMode.None))
                if (z.name.EndsWith("Vault") || z.name.EndsWith("Storage") || z.name.EndsWith("CoreSpot")) zones.Add(z);
            bp.zonesDarken = zones.ToArray();

            // ---------- 레이저 격자(데이터 코어 앞) ----------
            var gridT = new GameObject("LaserGrid").transform;
            gridT.SetParent(root, false);
            gridT.position = new Vector3(11.8f, 0f, 16.2f);
            var grid = gridT.gameObject.AddComponent<LaserGrid>();
            grid.player = pc;
            Vector3 core = GoalPos;
            Vector3 dirL = new Vector3(-0.694f, 0f, 0.72f).normalized;
            Vector3 n = new Vector3(-0.72f, 0f, -0.694f).normalized;
            float[] offs = { 3.9f, 3.0f, 2.1f, 1.2f };
            var beams = new List<LaserGrid.Beam>();
            var laserPrefab = LoadSP2("laser");
            for (int i = 0; i < offs.Length; i++)
            {
                Vector3 p = core + n * offs[i];
                ClipLine(p, dirL, new Vector2(5.6f, 12.1f), new Vector2(15.4f, 19.4f), out Vector3 a, out Vector3 b);
                var vis = new List<Renderer>();
                foreach (float h in new[] { 0.45f, 1.05f })
                {
                    Vector3 pa = a + Vector3.up * h, pb = b + Vector3.up * h;
                    var beam = Box(gridT, $"Beam{i + 1}_{h:0.00}", Vector3.zero, new Vector3(0.018f, 0.018f, Vector3.Distance(pa, pb)), mBeam, false);
                    beam.transform.position = (pa + pb) * 0.5f;
                    beam.transform.rotation = Quaternion.LookRotation(pb - pa);
                    var r = beam.GetComponent<Renderer>();
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    r.receiveShadows = false;
                    vis.Add(r);
                }
                if (laserPrefab != null)
                {
                    foreach (var end in new[] { a, b })
                    {
                        Vector3 to = (end == a ? b : a) - end;
                        var dev = SpawnProp(laserPrefab, gridT, end, Quaternion.Euler(0f, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg, 0f), Vector3.one * 0.8f);
                        dev.name = $"Emitter{i + 1}";
                        dev.transform.position = end;
                    }
                }
                // (빔 번호 글자는 벽 너머로 비쳐 보여 넣지 않는다)
                beams.Add(new LaraBeam(a, b, vis.ToArray()).ToBeam());
            }
            grid.beams = beams.ToArray();

            // ---------- 적응형 지휘: 자주 쓰는 퍼즐 장치를 순찰 지점에 추가(3회 이상) ----------
            var aroot = dir.transform.Find("AdaptiveRoutes");
            var list = new List<GuardDirector.Adaptation>(dir.adaptations);
            list.Add(new GuardDirector.Adaptation
            {
                routeId = "Breaker", routeLabel = "창고 배전반", guard = GuardNamed("Guard_Office"), post = false, threshold = 3, action = "순찰 지점에 넣었다",
                route = new[] { RoutePoint(aroot, "Breaker_P0", new Vector3(-1f, 0f, 13.1f), 0f), RoutePoint(aroot, "Breaker_P1", new Vector3(3.6f, 0f, 16.3f), 90f) }
            });
            list.Add(new GuardDirector.Adaptation
            {
                routeId = "Keypad_Front", routeLabel = "서버실 정문 키패드", guard = GuardNamed("Guard_Center"), post = false, threshold = 3, action = "순찰 지점에 넣었다",
                route = new[]
                {
                    RoutePoint(aroot, "KeyF_P0", new Vector3(3f, 0f, 4f), 0f), RoutePoint(aroot, "KeyF_P1", new Vector3(10.5f, 0f, 9.3f), 90f),
                    RoutePoint(aroot, "KeyF_P2", new Vector3(3f, 0f, -9.5f), 0f), RoutePoint(aroot, "KeyF_P3", new Vector3(9.5f, 0f, -9.5f), 0f)
                }
            });
            list.Add(new GuardDirector.Adaptation
            {
                routeId = "Keypad_Side", routeLabel = "서버실 옆문 키패드", guard = GuardNamed("Guard_Office"), post = false, threshold = 3, action = "순찰 지점에 넣었다",
                route = new[] { RoutePoint(aroot, "KeyS_P0", new Vector3(-1f, 0f, 13.1f), 0f), RoutePoint(aroot, "KeyS_P1", new Vector3(4f, 0f, 13.1f), 90f), RoutePoint(aroot, "KeyS_P2", new Vector3(2.7f, 0f, 17f), 0f) }
            });
            dir.adaptations = list.ToArray();
        }

        struct LaraBeam
        {
            Vector3 a, b; Renderer[] v;
            public LaraBeam(Vector3 a, Vector3 b, Renderer[] v) { this.a = a; this.b = b; this.v = v; }
            public LaserGrid.Beam ToBeam() => new LaserGrid.Beam { a = a, b = b, visuals = v };
        }

        /// <summary>점 p를 지나는 방향 d의 직선을 XZ 사각형으로 자른다</summary>
        static void ClipLine(Vector3 p, Vector3 d, Vector2 min, Vector2 max, out Vector3 a, out Vector3 b)
        {
            float t0 = float.NegativeInfinity, t1 = float.PositiveInfinity;
            void Slab(float pv, float dv, float lo, float hi)
            {
                if (Mathf.Abs(dv) < 1e-6f) return;
                float ta = (lo - pv) / dv, tb = (hi - pv) / dv;
                if (ta > tb) { float t = ta; ta = tb; tb = t; }
                t0 = Mathf.Max(t0, ta);
                t1 = Mathf.Min(t1, tb);
            }
            Slab(p.x, d.x, min.x, max.x);
            Slab(p.z, d.z, min.y, max.y);
            a = p + d * t0;
            b = p + d * t1;
            a.y = b.y = 0f;
        }
    }
}
