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

        /// <summary>등 뒤 제압 동작(오른팔 내려치기) 재생</summary>
        public void PlayStrike() { strikeT = 0f; }

        void AssistPose(float dt)
        {
            Vector3 up = Vector3.up;
            Vector3 fwd = mover.forward;
            Vector3 right = mover.right;

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

        static void Rotate(Transform bone, Vector3 axis, float angle)
        {
            if (bone == null || Mathf.Approximately(angle, 0f)) return;
            bone.rotation = Quaternion.AngleAxis(angle, axis) * bone.rotation;
        }
    }
}
