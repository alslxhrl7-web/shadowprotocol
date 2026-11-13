using UnityEngine;

namespace ShadowProtocol
{
    /// <summary>
    /// 탑다운 잠입 플레이어. WASD 이동, Shift 달리기, Ctrl/C 웅크리기.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Speed")]
        // 애니메이션 클립 고유 속도(걷기 1.0, 달리기 3.2, 웅크려 걷기 1.0 m/s)에 맞춘 값. 걷기는 걷기·달리기 블렌드(가벼운 조깅).
        public float walkSpeed = 2.4f;
        public float runSpeed = 5f;
        public float crouchSpeed = 1.5f;
        public float turnSpeed = 720f;
        public float gravity = -20f;

        [Header("Stance")]
        public float standHeight = 1.8f;
        public float crouchHeight = 1.1f;
        public Transform visual;

        [Header("Noise (radius in meters)")]
        public float walkNoiseRadius = 3f;
        public float runNoiseRadius = 10f;
        public float crouchNoiseRadius = 0f;
        public float noiseInterval = 0.3f;

        public bool InputEnabled { get; set; } = true;
        /// <summary>이동 속도 배율(기절체 끌기 등)</summary>
        [System.NonSerialized] public float SpeedMultiplier = 1f;
        [System.NonSerialized] public bool BlockRun;
        /// <summary>퍼즐 조작 중: 서 있어야 한다(노출 위험)</summary>
        [System.NonSerialized] public bool ForceStand;

        /// <summary>값이 있으면 이동 방향 대신 이 방향을 바라본다(조준·던지기)</summary>
        [System.NonSerialized] public Vector3? FaceDirection;


        // 테스트/봇용 가상 입력. UseVirtualInput이 켜져 있으면 Input 대신 이 값으로 같은 이동 코드를 탄다.
        [System.NonSerialized] public bool UseVirtualInput;
        [System.NonSerialized] public Vector2 VirtualMove;
        [System.NonSerialized] public bool VirtualRun;
        [System.NonSerialized] public bool VirtualCrouch;
        /// <summary>켜면 VirtualMove를 카메라가 아닌 월드 XZ 방향으로 해석(봇용)</summary>
        [System.NonSerialized] public bool VirtualMoveIsWorld;

        /// <summary>현재 위치의 밝기(LightZone). 경비병 시야 거리 보정에 쓴다.</summary>
        public float Brightness { get; private set; } = LightZone.DefaultBrightness;
        public bool IsCrouching { get; private set; }
        public bool IsRunning { get; private set; }
        public bool IsMoving { get; private set; }
        public float CurrentNoiseRadius { get; private set; }

        /// <summary>경비병 시야 거리 배율 (웅크리면 덜 보인다)</summary>
        public float VisibilityRangeFactor => IsCrouching ? 0.6f : 1f;

        /// <summary>의심 게이지 상승 배율</summary>
        public float VisibilityExposure
        {
            get
            {
                if (IsCrouching) return 0.45f;
                if (IsRunning) return 1.4f;
                return IsMoving ? 1f : 0.8f;
            }
        }

        public Vector3 HeadPoint => transform.position + Vector3.up * (CurrentHeight * 0.85f);
        public Vector3 CenterPoint => transform.position + Vector3.up * (CurrentHeight * 0.5f);
        public float CurrentHeight => cc != null ? cc.height : standHeight;

        CharacterController cc;
        Camera cam;
        float verticalVelocity;
        float noiseTimer;
        float visualHeight;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            visualHeight = standHeight;
            ApplyHeight(standHeight);
        }

        void Start()
        {
            cam = Camera.main;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            Vector2 input = Vector2.zero;
            bool wantCrouch = false;
            bool wantRun = false;

            if (InputEnabled && UseVirtualInput)
            {
                input = Vector2.ClampMagnitude(VirtualMove, 1f);
                wantCrouch = VirtualCrouch;
                wantRun = VirtualRun && !wantCrouch;
            }
            else if (InputEnabled)
            {
                input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
                wantCrouch = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) || Input.GetKey(KeyCode.C);
                wantRun = (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) && !wantCrouch;
            }

            if (ForceStand) wantCrouch = false;
            if (!wantCrouch && IsCrouching && !CanStand()) wantCrouch = true;
            if (wantCrouch != IsCrouching)
            {
                IsCrouching = wantCrouch;
                ApplyHeight(IsCrouching ? crouchHeight : standHeight);
            }

            Vector3 forward = Vector3.forward;
            Vector3 right = Vector3.right;
            if (cam == null) cam = Camera.main;
            if (cam != null && !(UseVirtualInput && VirtualMoveIsWorld))
            {
                forward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
                right = Vector3.ProjectOnPlane(cam.transform.right, Vector3.up).normalized;
            }

            Vector3 dir = forward * input.y + right * input.x;
            if (dir.sqrMagnitude > 1f) dir.Normalize();

            IsMoving = dir.sqrMagnitude > 0.01f;
            IsRunning = wantRun && IsMoving && !BlockRun;

            float speed = (IsCrouching ? crouchSpeed : (IsRunning ? runSpeed : walkSpeed)) * SpeedMultiplier;

            Vector3 face = FaceDirection.HasValue ? FaceDirection.Value : dir;
            face.y = 0f;
            if ((IsMoving || FaceDirection.HasValue) && face.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(face.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, turnSpeed * dt);
            }

            if (cc.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
            verticalVelocity += gravity * dt;

            Vector3 motion = dir * speed + Vector3.up * verticalVelocity;
            cc.Move(motion * dt);

            UpdateNoise(dt);
            UpdateVisual(dt);
            Brightness = LightZone.BrightnessAt(CenterPoint);
        }

        void UpdateNoise(float dt)
        {
            if (!IsMoving) CurrentNoiseRadius = 0f;
            else if (IsCrouching) CurrentNoiseRadius = crouchNoiseRadius;
            else if (IsRunning) CurrentNoiseRadius = runNoiseRadius;
            else CurrentNoiseRadius = walkNoiseRadius;

            noiseTimer -= dt;
            if (CurrentNoiseRadius > 0f && noiseTimer <= 0f)
            {
                noiseTimer = noiseInterval;
                NoiseSystem.Emit(transform.position, CurrentNoiseRadius, gameObject);
            }
        }

        bool CanStand()
        {
            Vector3 origin = transform.position + Vector3.up * (crouchHeight - 0.05f);
            float dist = standHeight - crouchHeight + 0.1f;
            return !Physics.SphereCast(origin, cc.radius * 0.9f, Vector3.up, out _, dist, ~0, QueryTriggerInteraction.Ignore);
        }

        void ApplyHeight(float h)
        {
            cc.height = h;
            cc.center = new Vector3(0f, h * 0.5f, 0f);
        }

        void UpdateVisual(float dt)
        {
            if (visual == null) return;
            float target = IsCrouching ? crouchHeight : standHeight;
            visualHeight = Mathf.Lerp(visualHeight, target, 1f - Mathf.Exp(-14f * dt));
            visual.localScale = new Vector3(1f, visualHeight / standHeight, 1f);
        }
    }
}
