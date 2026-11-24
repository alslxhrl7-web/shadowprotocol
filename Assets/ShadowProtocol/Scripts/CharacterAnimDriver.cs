using UnityEngine;

namespace ShadowProtocol
{
    /// <summary>
    /// Humanoid Animator 파라미터를 캐릭터 상태에서 채운다(모델 루트, Animator 옆에 붙인다).
    /// Speed: 실제 이동 속도(m/s) — 블렌드 트리 임계값이 클립 고유 속도라 그 범위에서는 발이 미끄러지지 않는다.
    /// MoveScale: 가장 빠른 클립보다 빠를 때만 재생 속도를 올려 보폭을 맞춘다.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class CharacterAnimDriver : MonoBehaviour
    {
        public Transform mover;
        public PlayerController player;
        public GuardAI guard;

        [Header("클립 고유 속도(m/s)")]
        public float runNatural = 3.2f;
        public float crouchWalkNatural = 1f;
        public float maxMoveScale = 1.8f;

        [Header("던지기")]
        public float throwDuration = 1.15f;
        [Tooltip("던지기 시작 후 손이 앞으로 나가는(물체를 놓는) 시점")]
        public float throwReleaseDelay = 0.6f;

        public float ThrowReleaseDelay => throwReleaseDelay;

        static readonly int HSpeed = Animator.StringToHash("Speed");
        static readonly int HCrouch = Animator.StringToHash("Crouch");
        static readonly int HMoveScale = Animator.StringToHash("MoveScale");
        static readonly int HThrow = Animator.StringToHash("Throw");
        static readonly int HSearching = Animator.StringToHash("Searching");
        static readonly int HDown = Animator.StringToHash("Down");

        Animator anim;
        Vector3 last;
        float speed, crouch, layerW, throwTimer, talkTimer;
        bool hasCrouch, hasThrow, hasSearch, hasDown;

        public Animator Animator => anim;
        public float Speed => speed;

        void Awake()
        {
            anim = GetComponent<Animator>();
            anim.applyRootMotion = false;
        }

        void Start()
        {
            if (mover == null) mover = transform.parent != null ? transform.parent : transform;
            if (player == null) player = GetComponentInParent<PlayerController>();
            if (guard == null) guard = GetComponentInParent<GuardAI>();
            last = mover.position;
            if (anim.runtimeAnimatorController != null)
            {
                foreach (var p in anim.parameters)
                {
                    if (p.nameHash == HCrouch) hasCrouch = true;
                    else if (p.nameHash == HThrow) hasThrow = true;
                    else if (p.nameHash == HSearching) hasSearch = true;
                    else if (p.nameHash == HDown) hasDown = true;
                }
            }
        }

        void Update()
        {
            if (anim.runtimeAnimatorController == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            Vector3 d = mover.position - last;
            last = mover.position;
            d.y = 0f;
            float inst = d.magnitude / dt;
            if (inst > 15f) inst = speed; // 순간이동(체크포인트 등)은 무시
            speed = Mathf.Lerp(speed, inst, 1f - Mathf.Exp(-10f * dt));
            if (speed < 0.05f) speed = 0f;

            bool down = guard != null && guard.ShowDown;
            if (down) speed = 0f;

            float top = runNatural;
            if (player != null && hasCrouch)
            {
                crouch = Mathf.Lerp(crouch, player.IsCrouching ? 1f : 0f, 1f - Mathf.Exp(-10f * dt));
                anim.SetFloat(HCrouch, crouch);
                top = Mathf.Lerp(runNatural, crouchWalkNatural, crouch);
            }
            anim.SetFloat(HSpeed, speed);
            anim.SetFloat(HMoveScale, speed > top ? Mathf.Min(speed / top, maxMoveScale) : 1f);

            if (guard != null)
            {
                if (hasSearch) anim.SetBool(HSearching, guard.IsLookingAround && !down);
                if (hasDown) anim.SetBool(HDown, down);
            }

            // 상체 레이어(1번): 플레이어 던지기 / 경비 무전
            if (anim.layerCount > 1)
            {
                throwTimer -= dt;
                talkTimer -= dt;
                float target = (throwTimer > 0f || (talkTimer > 0f && !down)) ? (throwTimer > 0f ? 1f : 0.85f) : 0f;
                layerW = Mathf.MoveTowards(layerW, target, dt * 5f);
                anim.SetLayerWeight(1, layerW);
            }
        }

        public void PlayThrow()
        {
            if (anim.runtimeAnimatorController == null || !hasThrow) return;
            anim.SetTrigger(HThrow);
            throwTimer = throwDuration;
            layerW = Mathf.Max(layerW, 0.6f);
        }

        public void Talk(float seconds)
        {
            talkTimer = Mathf.Max(talkTimer, seconds);
        }
    }
}
