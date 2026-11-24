using UnityEngine;

namespace ShadowProtocol
{
    /// <summary>
    /// 애니메이션 없는 리깅 캐릭터용 절차적 포즈.
    /// T-포즈 팔 내리기, 이동 속도 기반 걷기 흔들기, 상하 흔들림, 웅크리기 낮추기.
    /// Humanoid면 Animator.GetBoneTransform, 아니면 본 이름으로 찾는다.
    /// 모델 루트(캐릭터 오브젝트의 자식)에 붙인다.
    /// </summary>
    public class CharacterProceduralPose : MonoBehaviour
    {
        [Tooltip("이동 속도를 잴 대상. 비우면 부모")]
        public Transform mover;
        public PlayerController player;

        [Header("Pose")]
        public float armDownAngle = 72f;
        public float walkSpeedForFullSwing = 3.5f;
        public float legSwing = 28f;
        public float armSwing = 22f;
        public float strideFrequency = 1.6f;
        public float bobHeight = 0.04f;
        [Tooltip("정지 시 호흡(상체 앞뒤 흔들림, 도)")]
        public float idleBreath = 2f;

        [Header("Crouch")]
        [Tooltip("발 고정을 못 쓸 때(발 본 없음)만 쓰는 웅크리기 낮추기 값")]
        public float crouchDrop = 0.3f;
        [Tooltip("포즈 변화 후 더 낮은 발이 바닥에 닿도록 모델 높이를 맞춘다")]
        public bool plantFeet = true;
        public float crouchThigh = 75f;
        public float crouchKnee = 120f;
        public float crouchSpine = 35f;

        Animator animator;
        Transform hips, spine, lUpperArm, rUpperArm, lLowerArm, rLowerArm, lUpperLeg, rUpperLeg, lLowerLeg, rLowerLeg;
        Transform lFoot, rFoot, lToe, rToe;
        float restFootHeight;
        Quaternion[] footRest;
        Transform[] bones;
        Quaternion[] rest;
        Vector3 basePos;
        Vector3 lastMoverPos;
        float speed, phase, crouch;

        void Start()
        {
            if (mover == null) mover = transform.parent != null ? transform.parent : transform;
            if (player == null) player = GetComponentInParent<PlayerController>();
            animator = GetComponentInChildren<Animator>();
            basePos = transform.localPosition;
            lastMoverPos = mover.position;

            if (animator != null && animator.isHuman)
            {
                hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                spine = animator.GetBoneTransform(HumanBodyBones.Spine);
                lUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                rUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                lLowerArm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                rLowerArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
                lUpperLeg = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                rUpperLeg = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
                lLowerLeg = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
                rLowerLeg = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
                lFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                rFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
                lToe = animator.GetBoneTransform(HumanBodyBones.LeftToes);
                rToe = animator.GetBoneTransform(HumanBodyBones.RightToes);
            }
            else
            {
                hips = Find("Hips", "pelvis");
                spine = Find("Spine", "spine_01");
                lUpperArm = Find("LeftArm", "LeftUpperArm", "upperarm_l", "UpperArm.L");
                rUpperArm = Find("RightArm", "RightUpperArm", "upperarm_r", "UpperArm.R");
                lLowerArm = Find("LeftForeArm", "LeftLowerArm", "lowerarm_l", "Forearm.L");
                rLowerArm = Find("RightForeArm", "RightLowerArm", "lowerarm_r", "Forearm.R");
                lUpperLeg = Find("LeftUpLeg", "LeftUpperLeg", "thigh_l", "Thigh.L");
                rUpperLeg = Find("RightUpLeg", "RightUpperLeg", "thigh_r", "Thigh.R");
                lLowerLeg = Find("LeftLeg", "LeftLowerLeg", "calf_l", "Shin.L");
                rLowerLeg = Find("RightLeg", "RightLowerLeg", "calf_r", "Shin.R");
                lFoot = Find("LeftFoot", "foot_l", "Foot.L");
                rFoot = Find("RightFoot", "foot_r", "Foot.R");
                lToe = Find("LeftToeBase", "ball_l", "Toe.L");
                rToe = Find("RightToeBase", "ball_r", "Toe.R");
            }

            // 컨트롤러 없는 Animator가 포즈를 덮어쓰지 않게 끈다.
            if (animator != null && animator.runtimeAnimatorController == null) animator.enabled = false;
            // Animator Controller가 있으면 보조 모드: 머리 둘러보기·제압 동작만 덧입힌다.
            assist = animator != null && animator.enabled && animator.runtimeAnimatorController != null;
            guard = GetComponentInParent<GuardAI>();
            if (animator != null && animator.isHuman)
            {
                neck = animator.GetBoneTransform(HumanBodyBones.Neck);
                head = animator.GetBoneTransform(HumanBodyBones.Head);
                chest = animator.GetBoneTransform(HumanBodyBones.Chest);
                lHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                rHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            }
            else
            {
                // Generic(플레이어): 뼈 이름으로
                neck = Find("Neck", "neck_01");
                head = Find("Head", "head");
                chest = Find("Spine1", "Chest", "spine_02");
                lHand = Find("LeftHand", "hand_l", "Hand.L");
                rHand = Find("RightHand", "hand_r", "Hand.R");
            }
            lookPhase = Random.value * 10f;

            bones = new[] { hips, spine, lUpperArm, rUpperArm, lLowerArm, rLowerArm, lUpperLeg, rUpperLeg, lLowerLeg, rLowerLeg };
            rest = new Quaternion[bones.Length];
            for (int i = 0; i < bones.Length; i++) if (bones[i] != null) rest[i] = bones[i].localRotation;
            if (lFoot != null && rFoot != null) restFootHeight = LowestFootY() - mover.position.y;
            footRest = new[] { lFoot != null ? lFoot.localRotation : Quaternion.identity, rFoot != null ? rFoot.localRotation : Quaternion.identity };
        }

        Transform Find(params string[] names)
        {
            var all = GetComponentsInChildren<Transform>(true);
            foreach (var n in names)
                foreach (var t in all)
                    if (string.Equals(t.name, n, System.StringComparison.OrdinalIgnoreCase)) return t;
            foreach (var n in names)
                foreach (var t in all)
                    if (t.name.EndsWith(n, System.StringComparison.OrdinalIgnoreCase)) return t;
            return null;
        }

        void LateUpdate()
        {
            if (bones == null) return;
            float dt = Mathf.Max(Time.deltaTime, 1e-5f);
            if (assist) { AssistPose(dt); return; }

            Vector3 delta = mover.position - lastMoverPos;
            lastMoverPos = mover.position;
            delta.y = 0f;
            float instSpeed = delta.magnitude / dt;
            if (instSpeed > 15f) instSpeed = 0f; // 순간이동·튐 무시(웅크린 정지가 걷기처럼 보이던 문제)
            speed = Mathf.Lerp(speed, instSpeed, 1f - Mathf.Exp(-10f * dt));
            if (speed < 0.25f) speed = Mathf.Max(0f, speed - dt * 2f);
            float move01 = Mathf.Clamp01(speed / walkSpeedForFullSwing);
            phase += dt * strideFrequency * Mathf.PI * 2f * Mathf.Clamp(speed / walkSpeedForFullSwing, 0f, 1.8f);

            bool crouching = player != null && player.IsCrouching;
            crouch = Mathf.Lerp(crouch, crouching ? 1f : 0f, 1f - Mathf.Exp(-12f * dt));

            // 매 프레임 휴식 포즈에서 다시 계산한다(누적 방지).
            for (int i = 0; i < bones.Length; i++) if (bones[i] != null) bones[i].localRotation = rest[i];
            if (footRest != null)
            {
                if (lFoot != null) lFoot.localRotation = footRest[0];
                if (rFoot != null) rFoot.localRotation = footRest[1];
            }

            Vector3 fwd = mover.forward;
            Vector3 right = mover.right;
            float s = Mathf.Sin(phase);

            // T-포즈 풀기: 위팔을 몸 쪽으로 내린다.
            Rotate(lUpperArm, fwd, armDownAngle);
            Rotate(rUpperArm, fwd, -armDownAngle);
            Rotate(lLowerArm, right, -10f);
            Rotate(rLowerArm, right, -10f);

            // 걷기: 다리와 팔을 반대로 흔든다.
            float leg = legSwing * move01 * s;
            float arm = armSwing * move01 * s;
            float lThigh = leg - crouchThigh * crouch;
            float rThigh = -leg - crouchThigh * crouch;
            float lKnee = Mathf.Max(0f, -leg) * 0.8f + crouchKnee * crouch;
            float rKnee = Mathf.Max(0f, leg) * 0.8f + crouchKnee * crouch;
            Rotate(lUpperLeg, right, lThigh);
            Rotate(rUpperLeg, right, rThigh);
            Rotate(lLowerLeg, right, lKnee);
            Rotate(rLowerLeg, right, rKnee);
            // 발바닥을 바닥과 평행하게(허벅지+무릎 회전만큼 되돌린다). 안 하면 까치발이 되어 몸이 뜬다.
            Rotate(lFoot, right, -(lThigh + lKnee));
            Rotate(rFoot, right, -(rThigh + rKnee));
            Rotate(lUpperArm, right, -arm);
            Rotate(rUpperArm, right, arm);
            float idle = 1f - move01;
            float breath = Mathf.Sin(Time.time * 1.6f) * idleBreath * idle;
            Rotate(spine, right, crouchSpine * crouch + breath);
            Rotate(lUpperArm, fwd, -breath * 0.5f);
            Rotate(rUpperArm, fwd, breath * 0.5f);

            // 상하 흔들림 + 웅크리기 낮추기(모델 루트 이동)
            float bob = Mathf.Abs(Mathf.Sin(phase)) * bobHeight * move01;
            transform.localPosition = basePos;
            if (plantFeet && lFoot != null && rFoot != null)
            {
                float footNow = LowestFootY() - mover.position.y;
                transform.position += Vector3.up * (restFootHeight - footNow + bob);
            }
            else
            {
                transform.localPosition = basePos + Vector3.up * (bob - crouchDrop * crouch);
            }
        }

        // ---------------- 보조 모드(Animator 사용 시) ----------------

        bool assist;
        GuardAI guard;
        Transform neck, head, chest;
        float lookYaw, lookPitch, lookPhase, strikeT = -1f;
        const float StrikeDuration = 0.5f;

        /// <summary>등 뒤 제압 동작(오른팔 내려치기) 재생 — 대상 경비를 모를 때의 예비 동작</summary>
        public void PlayStrike() { strikeT = 0f; }

        // ---------------- 암살(AC3 참고: 뒤에서 입·어깨를 붙잡고 목덜미를 찌름) ----------------
        Transform lHand, rHand;
        Transform assassinTarget; // 경비 목 뼈
        Transform assassinHead;
        Transform assassinRoot;
        float assassinT = -1f;
        /// <summary>돌진 시간(이 시점에 찌름이 닿는다) — PlayerTools 돌진과 맞춘다</summary>
        public float assassinReach = 0.2f;
        const float AssassinDuration = 0.8f;

        public void PlayAssassinate(GuardAI g)
        {
            if (g == null) { PlayStrike(); return; }
            var an = g.GetComponentInChildren<Animator>();
            assassinRoot = g.transform;
            assassinTarget = an != null && an.isHuman ? an.GetBoneTransform(HumanBodyBones.Neck) : null;
            assassinHead = an != null && an.isHuman ? an.GetBoneTransform(HumanBodyBones.Head) : null;
            if (assassinTarget == null || lHand == null || rHand == null) { PlayStrike(); return; }
            assassinT = 0f;
        }

        void ApplyAssassinate(float dt, Vector3 right)
        {
            if (assassinT < 0f) return;
            assassinT += dt;
            float t = assassinT;
            if (t >= AssassinDuration || assassinTarget == null) { assassinT = -1f; return; }
            float reach = assassinReach;
            Vector3 gf = assassinRoot.forward, gr = assassinRoot.right;
            Vector3 neckP = assassinTarget.position;
            Vector3 headP = assassinHead != null ? assassinHead.position : neckP + Vector3.up * 0.12f;
            // 왼손: 경비 입 앞을 덮어 잡는다(돌진하며 뻗음) → 0.45s부터 놓음
            float wL = t < reach ? Mathf.SmoothStep(0f, 1f, t / reach) : (t < 0.45f ? 1f : Mathf.SmoothStep(1f, 0f, (t - 0.45f) / 0.3f));
            Vector3 mouth = headP + gf * 0.13f - Vector3.up * 0.05f;
            // 오른손: 돌진 동안 몸쪽으로 당겨 두었다가(준비) 닿는 순간 목덜미로 찌름 → 유지 → 놓음
            Vector3 wind = rUpperArm != null ? rUpperArm.position - mover.forward * 0.05f - Vector3.up * 0.25f + mover.right * 0.12f : rHand.position;
            Vector3 stab = neckP - gf * 0.06f + gr * 0.04f;
            Vector3 rTarget; float wR;
            if (t < reach) { rTarget = wind; wR = Mathf.SmoothStep(0f, 1f, t / reach); }
            else if (t < reach + 0.07f) { rTarget = Vector3.Lerp(wind, stab, (t - reach) / 0.07f); wR = 1f; }
            else if (t < 0.45f) { rTarget = stab; wR = 1f; }
            else { rTarget = stab; wR = Mathf.SmoothStep(1f, 0f, (t - 0.45f) / 0.3f); }
            // 붙잡아 뒤로 당기는 동안 상체를 살짝 젖힘
            float pull = t > reach && t < 0.5f ? Mathf.Sin((t - reach) / (0.5f - reach) * Mathf.PI) : 0f;
            if (chest != null) Rotate(chest, right, -8f * pull);
            Vector3 down = Vector3.down * 0.3f;
            TwoBoneIK(lUpperArm, lLowerArm, lHand, mouth, lLowerArm.position + down - mover.right * 0.25f, wL);
            TwoBoneIK(rUpperArm, rLowerArm, rHand, rTarget, rLowerArm.position + down + mover.right * 0.25f, wR);
        }

        // 몸 기울기(관성): 회전할 때 안쪽으로, 가속할 때 앞으로·감속할 때 뒤로
        [Header("Lean (assist 모드)")]
        [Tooltip("회전 기울기 최대(도). 실제 원심 기울기 atan(v·ω/g)의 절반을 상체에만 준다")]
        public float maxBank = 9f;
        [Tooltip("가속 1 m/s²당 앞으로 숙임(도)")]
        public float accelLean = 1.1f;
        public float maxAccelLean = 6f;
        float bank, leanPitch, prevYaw, prevSpeed;
        Vector3 prevLeanPos;
        bool leanInit;

        void ApplyLean(float dt, Vector3 fwd, Vector3 right)
        {
            if (spine == null && chest == null) return;
            Vector3 pos = mover.position;
            float yaw = mover.eulerAngles.y;
            if (!leanInit) { prevLeanPos = pos; prevYaw = yaw; leanInit = true; }
            Vector3 v = (pos - prevLeanPos) / dt; v.y = 0f;
            float yawRate = Mathf.DeltaAngle(prevYaw, yaw) / dt; // 도/초, 오른쪽 회전이 +
            prevLeanPos = pos; prevYaw = yaw;
            float spd = v.magnitude;
            if (spd > 15f) { spd = prevSpeed; yawRate = 0f; } // 순간 이동 무시
            float acc = (spd - prevSpeed) / dt;
            prevSpeed = spd;

            bool down = guard != null && guard.IsKnockedOut;
            float bankT = down ? 0f : Mathf.Clamp(Mathf.Atan2(spd * yawRate * Mathf.Deg2Rad, 9.81f) * Mathf.Rad2Deg * 0.5f, -maxBank, maxBank);
            float pitchT = down ? 0f : Mathf.Clamp(acc * accelLean, -maxAccelLean * 0.6f, maxAccelLean);
            bank = Mathf.Lerp(bank, bankT, 1f - Mathf.Exp(-6f * dt));
            leanPitch = Mathf.Lerp(leanPitch, pitchT, 1f - Mathf.Exp(-5f * dt));
            if (Mathf.Abs(bank) < 0.05f && Mathf.Abs(leanPitch) < 0.05f) return;
            // 오른쪽으로 돌면 몸 위쪽이 오른쪽으로(전진축 기준 음의 회전), 가속하면 앞으로 숙임(오른쪽 축 기준 양의 회전)
            if (spine != null) { Rotate(spine, fwd, -bank * 0.6f); Rotate(spine, right, leanPitch * 0.6f); }
            if (chest != null) { Rotate(chest, fwd, -bank * 0.4f); Rotate(chest, right, leanPitch * 0.4f); }
        }

        // 웅크린 자세 보정: Blender 클립 위에 덧입힌다.
        // 2026-11-24 끔(모두 0, 사용자 결정): 7차 클립(오리진 참고, 골반 0.52m·상체 66°)이 이미 원하는 자세라서,
        // 6차 클립(상체 41°)에 맞춘 이전 값(22/0.10/14/22/14)이 겹쳐 들어가면 상체 83°·골반 0.62m가 됐다.
        [Header("웅크린 자세 보정(기본 꺼짐 — 7차 클립이 자세를 담당)")]
        [Tooltip("상체를 더 숙임(도). 7차 클립 기준 0")]
        public float stealthExtraLean = 0f;
        [Tooltip("골반을 올림(m). 7차 클립 기준 0")]
        public float stealthHipRaise = 0f;
        [Tooltip("위팔을 뒤로(도). 7차 클립 기준 0")]
        public float stealthArmBack = 0f;
        [Tooltip("팔꿈치 펴기(도). 7차 클립 기준 0")]
        public float stealthElbowRelax = 0f;
        [Tooltip("고개를 들어 앞을 봄(도). 7차 클립 기준 0")]
        public float stealthHeadUp = 0f;

        /// <summary>뛰어넘기(0~1): 왼손을 장애물 위에 짚고 몸을 옆으로 눕혀 넘는다(ParkourController가 매 프레임 넣음)</summary>
        [System.NonSerialized] public float vaultWeight;
        /// <summary>슬라이딩(0~1): 상체를 뒤로 젖히고 오른팔을 앞으로 뻗어 균형, 몸을 낮춤</summary>
        [System.NonSerialized] public float slideWeight;
        /// <summary>파쿠르 클립 재생 중에는 기울기·웅크림 보정·절차적 파쿠르를 모두 끈다</summary>
        [System.NonSerialized] public bool overlaySuppressed;
        float parkourDrop;

        void ApplyParkour(Vector3 fwd, Vector3 right)
        {
            float v = vaultWeight, s = slideWeight;
            if (v > 0.001f)
            {
                // 오른쪽 축 기준 음의 회전 = 아래를 향한 팔이 앞으로 / 전진축 기준 양의 회전 = 몸 위가 왼쪽으로
                Rotate(lUpperArm, right, -65f * v);
                Rotate(lLowerArm, right, -10f * v);
                Rotate(rUpperArm, fwd, -35f * v);   // 오른팔은 옆으로 벌려 균형
                if (spine != null) Rotate(spine, fwd, 18f * v);
                if (chest != null) Rotate(chest, fwd, 12f * v);
                if (spine != null) Rotate(spine, right, 10f * v);
            }
            if (s > 0.001f)
            {
                if (spine != null) Rotate(spine, right, -16f * s);
                if (chest != null) Rotate(chest, right, -10f * s);
                Rotate(rUpperArm, right, -55f * s);
                Rotate(lUpperArm, fwd, 30f * s);
                if (head != null) Rotate(head, right, 14f * s); // 젖힌 만큼 고개는 앞을 봄
            }
            // 슬라이딩 중 몸을 바닥 쪽으로(모델 루트만)
            float wantDrop = 0.12f * s;
            if (!Mathf.Approximately(wantDrop, parkourDrop))
            {
                transform.localPosition += Vector3.up * (parkourDrop - wantDrop); // 지난 프레임 낮춘 만큼 되돌리고 새로 낮춤
                parkourDrop = wantDrop;
            }
        }

        [Header("서 있는 자세 보정(AC2·AC3 걷기·달리기 참고)")]
        [Tooltip("걷기(약 2.4 m/s)에서 상체 숙임 추가(도). 클립 측정 9°")]
        public float walkExtraLean = 4f;
        [Tooltip("달리기(5 m/s)에서 상체 숙임 추가(도). 클립 측정 18° → 약 25°")]
        public float runExtraLean = 7f;
        [Tooltip("걷기에서 고개를 들어 앞을 봄(도). 클립은 목이 28° 숙어 바닥을 봄")]
        public float walkHeadUp = 10f;
        public float runHeadUp = 12f;
        float standSpeed;

        void ApplyStandingStyle(Vector3 right, float w)
        {
            if (player == null) return;
            standSpeed = Mathf.Lerp(standSpeed, player.PlanarVelocity.magnitude, 1f - Mathf.Exp(-6f * Time.deltaTime));
            float walkT = Mathf.Clamp01(standSpeed / Mathf.Max(0.1f, player.walkSpeed));
            float runT = Mathf.InverseLerp(player.walkSpeed, player.runSpeed, standSpeed);
            float lean = (walkExtraLean * walkT + (runExtraLean - walkExtraLean) * runT) * w;
            float headUp = (walkHeadUp * walkT + (runHeadUp - walkHeadUp) * runT) * w;
            if (spine != null) Rotate(spine, right, lean * 0.6f);
            if (chest != null) Rotate(chest, right, lean * 0.4f);
            if (neck != null) Rotate(neck, right, -headUp * 0.5f);
            if (head != null) Rotate(head, right, -headUp * 0.5f);
        }

        void ApplyStealthCrouch(Vector3 right, float w)
        {
            // 1) 클립이 딛은 발 위치·방향 기억 → 2) 골반 올림 → 3) 다리 IK로 발을 원래 자리에(미끄러짐 없음, 무릎이 덜 굽음)
            if (hips != null && stealthHipRaise > 0f && lUpperLeg != null && lLowerLeg != null && lFoot != null && rUpperLeg != null && rLowerLeg != null && rFoot != null)
            {
                Vector3 lp = lFoot.position, rp = rFoot.position;
                Quaternion lr = lFoot.rotation, rr = rFoot.rotation;
                Vector3 kneeFwd = mover.forward * 0.4f;
                Vector3 lPole = lLowerLeg.position + kneeFwd, rPole = rLowerLeg.position + kneeFwd;
                hips.position += Vector3.up * (stealthHipRaise * w);
                TwoBoneIK(lUpperLeg, lLowerLeg, lFoot, lp, lPole, 1f);
                TwoBoneIK(rUpperLeg, rLowerLeg, rFoot, rp, rPole, 1f);
                lFoot.rotation = lr;
                rFoot.rotation = rr;
            }
            // 오른쪽 축 기준 양의 회전 = 위가 앞으로(숙임), 아래를 향한 팔은 뒤로
            if (spine != null) Rotate(spine, right, stealthExtraLean * 0.6f * w);
            if (chest != null) Rotate(chest, right, stealthExtraLean * 0.4f * w);
            Rotate(lUpperArm, right, stealthArmBack * w);
            Rotate(rUpperArm, right, stealthArmBack * w);
            Rotate(lLowerArm, right, stealthElbowRelax * w);
            Rotate(rLowerArm, right, stealthElbowRelax * w);
            if (neck != null) Rotate(neck, right, -stealthHeadUp * 0.5f * w);
            if (head != null) Rotate(head, right, -stealthHeadUp * 0.5f * w);
        }

        void AssistPose(float dt)
        {
            Vector3 up = Vector3.up;
            Vector3 fwd = mover.forward;
            Vector3 right = mover.right;
            if (overlaySuppressed) return; // 파쿠르 클립 재생 중: 클립 그대로
            ApplyLean(dt, fwd, right);
            if (player != null)
            {
                crouch = Mathf.Lerp(crouch, player.IsCrouching ? 1f : 0f, 1f - Mathf.Exp(-8f * dt));
                bool parkour = vaultWeight > 0.01f || slideWeight > 0.01f;
                if (crouch > 0.01f && !parkour && assassinT < 0f) ApplyStealthCrouch(right, crouch);
                if (crouch < 0.99f && !parkour && assassinT < 0f) ApplyStandingStyle(right, 1f - crouch);
                ApplyParkour(fwd, right);
                ApplyAssassinate(dt, right);
            }

            // 경비병 머리 둘러보기(상태별)
            float yawT = 0f, pitchT = 0f;
            if (guard != null && !guard.IsKnockedOut)
            {
                float t = Time.time + lookPhase;
                Vector3 delta = mover.position - lastMoverPos;
                lastMoverPos = mover.position;
                bool moving = delta.sqrMagnitude > 0.0004f;
                switch (guard.State)
                {
                    case GuardState.Patrol:
                        yawT = Mathf.Sin(t * (moving ? 0.45f : 0.7f)) * (moving ? 18f : 38f);
                        pitchT = Mathf.Sin(t * 0.31f) * 4f;
                        break;
                    case GuardState.Search:
                        yawT = Mathf.Sin(t * 0.9f) * (guard.IsLookingAround ? 15f : 40f);
                        break;
                    default:
                        if (guard.player != null && (guard.CanSeePlayer || guard.State == GuardState.Alert))
                        {
                            Vector3 to = guard.player.HeadPoint - (head != null ? head.position : mover.position + up * 1.6f);
                            Vector3 flat = Vector3.ProjectOnPlane(to, up);
                            yawT = Mathf.Clamp(Vector3.SignedAngle(fwd, flat, up), -70f, 70f);
                            pitchT = Mathf.Clamp(-Mathf.Atan2(to.y, flat.magnitude) * Mathf.Rad2Deg, -30f, 30f);
                        }
                        else yawT = Mathf.Sin(t * 1.1f) * 30f;
                        break;
                }
            }
            float k = 1f - Mathf.Exp(-5f * dt);
            lookYaw = Mathf.Lerp(lookYaw, yawT, k);
            lookPitch = Mathf.Lerp(lookPitch, pitchT, k);
            if (Mathf.Abs(lookYaw) > 0.05f || Mathf.Abs(lookPitch) > 0.05f)
            {
                if (chest != null) Rotate(chest, up, lookYaw * 0.2f);
                if (neck != null) { Rotate(neck, up, lookYaw * 0.35f); Rotate(neck, right, lookPitch * 0.4f); }
                if (head != null) { Rotate(head, up, lookYaw * 0.45f); Rotate(head, right, lookPitch * 0.6f); }
            }

            // 제압: 오른팔을 들어 앞으로 내려친다
            if (strikeT >= 0f)
            {
                strikeT += dt;
                float n = strikeT / StrikeDuration;
                if (n >= 1f) strikeT = -1f;
                else
                {
                    float w = n < 0.35f ? n / 0.35f : 1f - (n - 0.35f) / 0.65f;
                    float chop = n < 0.35f ? -120f * w : Mathf.Lerp(-120f, -40f, (n - 0.35f) / 0.65f) * w;
                    Rotate(rUpperArm, right, chop);
                    Rotate(rLowerArm, right, -40f * w);
                    Rotate(spine, right, 18f * w);
                    Rotate(lUpperArm, right, -50f * w);
                }
            }
        }

        /// <summary>발목·발끝 본 중 가장 낮은 높이(발끝이 바닥을 뚫지 않게)</summary>
        float LowestFootY()
        {
            float y = Mathf.Min(lFoot.position.y, rFoot.position.y);
            if (lToe != null) y = Mathf.Min(y, lToe.position.y);
            if (rToe != null) y = Mathf.Min(y, rToe.position.y);
            return y;
        }

        /// <summary>
        /// 두 관절 IK(어깨→팔꿈치→손, 골반→무릎→발): 끝(c)이 target에 오도록 a·b를 돌린다. 굽는 방향은 pole 쪽.
        /// weight만큼 현재 끝 위치에서 target으로 섞는다. Generic 리그라 Unity Humanoid IK를 못 써서 직접 푼다.
        /// </summary>
        public static void TwoBoneIK(Transform a, Transform b, Transform c, Vector3 target, Vector3 pole, float weight)
        {
            if (a == null || b == null || c == null || weight <= 0.001f) return;
            Vector3 pa = a.position, pb = b.position, pc = c.position;
            target = Vector3.Lerp(pc, target, weight);
            float lab = (pb - pa).magnitude, lbc = (pc - pb).magnitude;
            if (lab < 1e-4f || lbc < 1e-4f) return;
            Vector3 at = target - pa;
            float dist = at.magnitude;
            if (dist < 1e-4f) return;
            Vector3 dir = at / dist;
            float lat = Mathf.Clamp(dist, Mathf.Abs(lab - lbc) + 1e-3f, lab + lbc - 1e-3f);
            float cosA = Mathf.Clamp((lab * lab + lat * lat - lbc * lbc) / (2f * lab * lat), -1f, 1f);
            float sinA = Mathf.Sqrt(1f - cosA * cosA);
            Vector3 toPole = Vector3.ProjectOnPlane(pole - pa, dir);
            if (toPole.sqrMagnitude < 1e-6f) toPole = Vector3.ProjectOnPlane(pb - pa, dir);
            if (toPole.sqrMagnitude < 1e-6f) return;
            Vector3 bend = toPole.normalized;
            Vector3 wantB = pa + dir * (cosA * lab) + bend * (sinA * lab);
            a.rotation = Quaternion.FromToRotation(pb - pa, wantB - pa) * a.rotation;
            pb = b.position; pc = c.position;
            Vector3 wantC = pa + dir * lat;
            b.rotation = Quaternion.FromToRotation(pc - pb, wantC - pb) * b.rotation;
        }

        static void Rotate(Transform bone, Vector3 axis, float angle)
        {
            if (bone == null || Mathf.Approximately(angle, 0f)) return;
            bone.rotation = Quaternion.AngleAxis(angle, axis) * bone.rotation;
        }
    }
}
