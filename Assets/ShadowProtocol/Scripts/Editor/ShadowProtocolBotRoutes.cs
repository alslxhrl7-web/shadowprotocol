using System.Collections.Generic;
using UnityEngine;
using static ShadowProtocol.EditorTools.ShadowProtocolBot;

namespace ShadowProtocol.EditorTools
{
    /// <summary>봇 테스트 경로(플레이 모드에서 ShadowProtocolBot.Run에 넘긴다)</summary>
    public static class ShadowProtocolBotRoutes
    {
        static Vector3 V(float x, float z, float y = 0f) => new Vector3(x, y, z);

        static readonly Vector3 Start = V(-35.5f, -15f);
        static readonly Vector3 Core = V(13.2f, 17.4f);
        static readonly Vector3 LaserN = new Vector3(-0.72f, 0f, -0.694f).normalized;

        // 야드 서쪽 → 사무동 출입문(우회로)
        static readonly Vector3[] YardToOffice = { V(-37.8f, -6f), V(-38.2f, 8f), V(-30.5f, 10.2f), V(-26.8f, 11f), V(-22.6f, 8.3f), V(-17.5f, 12.9f), V(-13.5f, 13.1f) };
        // 야드 → 정면 하역문
        static readonly Vector3[] YardToFront = { V(-37.8f, -6f), V(-31f, -0.4f), V(-24f, -0.8f), V(-17.5f, -1f), V(-13.6f, -0.6f) };
        // 홀: 남쪽(캣워크 아래) → 동쪽 벽 → 서버실 정문 앞(정문 경비 등 뒤)
        static readonly Vector3[] HallToVaultDoor = { V(-12.2f, -10.4f), V(-3f, -10.4f), V(4.5f, -10.4f), V(10.4f, -10.2f), V(10.4f, -2.4f), V(13.4f, -1.2f), V(13.4f, 5.2f), V(13.3f, 9.6f) };

        static void AddMoves(List<Step> s, IEnumerable<Vector3> pts, bool crouch = true)
        {
            foreach (var p in pts) s.Add(Move(p, crouch));
        }

        static List<Vector3> Rev(Vector3[] a) { var l = new List<Vector3>(a); l.Reverse(); return l; }

        static GuardAI G(string n) { var go = GameObject.Find("Guards/" + n); return go != null ? go.GetComponent<GuardAI>() : null; }

        static void VaultAndBack(List<Step> s, Vector3 entry, Vector3 inside, System.Func<bool> waitBeforeExit = null, string waitLabel = null)
        {
            // 문 앞 경비에게 발소리가 들리지 않게 웅크려 들어간다
            s.Add(Move(entry, crouch: true, nav: false, check: false, label: "enter vault"));
            s.Add(Move(inside, crouch: true, nav: false, check: false, label: "inside vault"));
            s.Add(Move(Core + LaserN * 4.4f, crouch: false, nav: false, check: false, label: "laser start"));
            s.AddRange(Laser(true));
            s.Add(Move(Core, crouch: false, nav: false, check: false, label: "data core"));
            s.Add(WaitUntil("data secured", () => GameManager.Instance.HasData, false));
            s.Add(Move(Core + LaserN * 0.75f, crouch: false, nav: false, check: false, label: "laser exit start"));
            s.AddRange(Laser(false));
            // 레이저 안쪽(문에서 안 보이는 곳)에서 나갈 때를 기다린다
            if (waitBeforeExit != null) s.Add(WaitUntil(waitLabel ?? "wait before exit", waitBeforeExit, true));
            s.Add(Move(inside, crouch: true, nav: false, check: false, label: "vault inside (out)"));
            s.Add(Move(entry, crouch: true, nav: false, hold: 1.5f, label: "vault door (out)"));
        }

        /// <summary>경로 1(도구 없음): 정면 하역문 → 홀 남쪽·동쪽 벽 → 정문 경비 등 뒤의 키패드(UI 흐름) → 레이저 격자 → 코어 → 같은 길로 탈출</summary>
        public static List<Step> HallQuiet()
        {
            var s = new List<Step>();
            AddMoves(s, YardToFront);
            AddMoves(s, HallToVaultDoor);
            s.Add(Move(V(11.5f, 10.25f), crouch: true, nav: false, hold: 5f, label: "front keypad", standAfter: true)); // 키패드는 서서 조작
            s.Add(Keypad("Keypad_Front"));
            VaultAndBack(s, V(10f, 12.7f), V(10f, 14.2f));
            s.Add(Move(V(11.5f, 10.25f), crouch: true, nav: false, hold: 1f));
            // 돌아갈 때는 캐트워크(어둡고 경비 머리 위)로 홀을 건너 하역문으로 나간다
            s.Add(Move(V(13.3f, 9.6f)));
            s.Add(Move(V(13.4f, 5.2f)));
            s.Add(Move(V(14f, -1.6f)));
            s.Add(Move(V(14f, -9.7f, 3.2f), crouch: true, nav: false, label: "ramp E up"));
            s.Add(Move(V(0f, -10.1f, 3.2f), crouch: true, nav: false, label: "catwalk mid"));
            s.Add(Move(V(-13f, -10.1f, 3.2f), crouch: true, nav: false, label: "catwalk west"));
            s.Add(Move(V(-13f, -2.0f), crouch: true, nav: false, label: "ramp W down"));
            s.Add(Move(V(-13.6f, -0.6f), crouch: true, nav: false));
            var back = Rev(YardToFront); back.RemoveAt(0);
            AddMoves(s, back);
            s.Add(Move(Start, label: "extraction"));
            s.Add(WaitSeconds(2f));
            return s;
        }

        /// <summary>
        /// 경로 2(도구 사용): 사무동 → 사무실에 숨었다가 창고로 가는 감독관을 뒤따라 등 뒤 제압 → 기절체를 끌고 옆문 키패드 →
        /// 서버실 안에 숨김 → 레이저 → 코어 → 사무동으로 탈출(무전 확인하러 온 경비는 기절체를 못 찾는다)
        /// </summary>
        public static List<Step> OfficeTools()
        {
            var s = new List<Step>();
            AddMoves(s, YardToOffice);
            s.Add(Move(V(-11.3f, 13.2f)));
            s.Add(Move(V(-3.3f, 16.2f), label: "hide in office"));
            s.Add(WaitUntil("supervisor leaves for storage", () =>
            {
                var g = G("Guard_Office");
                return g == null || g.IsKnockedOut || (g.State == GuardState.Patrol && g.PatrolIndex == 1 && g.transform.position.x > -0.2f && g.transform.position.z < 14.2f);
            }));
            s.Add(Takedown("Guard_Office"));
            s.Add(Drag(true));
            s.Add(Move(V(2.7f, 13.2f), crouch: false, nav: false, check: false, label: "drag → corridor"));
            s.Add(Move(V(4.4f, 12.75f), crouch: false, nav: false, check: false, label: "drag → side keypad"));
            s.Add(Drag(false));
            s.Add(Keypad("Keypad_Side"));
            s.Add(Drag(true));
            s.Add(Move(V(6.9f, 12.9f), crouch: false, nav: false, check: false, label: "drag → vault"));
            s.Add(Move(V(7.1f, 16.6f), crouch: false, nav: false, check: false, label: "hide body in vault"));
            s.Add(Drag(false));
            // 무전 확인하러 온 홀 동쪽 경비(Guard_Vault)가 순찰로 돌아갈 때까지 서버실 안에서 기다린다(웅크림 1.25 m/s에서 복도에서 마주침)
            VaultAndBack(s, V(7.2f, 13.2f), V(8.8f, 13.6f), () => { var g = G("Guard_Vault"); return g == null || g.IsKnockedOut || g.State == GuardState.Patrol; }, "east guard back on patrol");
            // 서버실 문 앞으로 나가기 전에 안전 확인(무전 확인하러 온 홀 동쪽 경비가 복도에 있으면 서버실 안에서 기다린다. 웅크림 1.25 m/s에서 마주쳤음)
            s.Add(Move(V(6.9f, 12.9f), crouch: true, nav: false, hold: 1.5f));
            s.Add(Move(V(4.2f, 12.9f), crouch: true, nav: false, hold: 2f));
            s.Add(Move(V(-3.3f, 13.1f)));
            s.Add(Move(V(-11.3f, 13.2f)));
            AddMoves(s, Rev(YardToOffice));
            s.Add(Move(Start, label: "extraction"));
            s.Add(WaitSeconds(2f));
            return s;
        }

        /// <summary>적응 테스트: 캣워크를 지나고 재시도(체크포인트)하기를 n번</summary>
        public static List<Step> CatwalkProbe(int attempts)
        {
            var s = new List<Step>();
            for (int i = 0; i < attempts; i++)
            {
                if (i == 0) AddMoves(s, YardToFront);
                s.Add(Move(V(-13f, -1.6f)));
                s.Add(Move(V(-13f, -9.8f, 3.2f), crouch: true, nav: false, label: "ramp up"));
                s.Add(Move(V(-9f, -10.1f, 3.2f), crouch: true, nav: false, label: "catwalk"));
                s.Add(Move(V(-4f, -10.1f, 3.2f), crouch: true, nav: false, label: "catwalk mid"));
                s.Add(WaitSeconds(0.5f));
                s.Add(Restart(true));
            }
            s.Add(WaitSeconds(6f));
            return s;
        }
    }
}
