using System.Collections.Generic;
using UnityEngine;

namespace ShadowProtocol
{
    /// <summary>
    /// 3인칭 어깨너머 카메라. 마우스 yaw/pitch(감도·Y반전·FOV는 설정), 휠 줌, SphereCast 충돌.
    /// - 최소 거리 0.8m: 뒤가 막히면 0까지 당기지 않고 카메라를 들어 올려 내려다본다. 그래도 가까우면 캐릭터를 반투명하게.
    /// - 큰 소품(CameraFadeable)은 카메라를 당기지 않고 반투명해진다.
    /// - 타이틀·브리핑에서는 기지 전체를 천천히 도는 연출 카메라.
    /// Tab/V로 TopDownCamera(전술 시점) 전환. 조준 중에는 어깨 가까이 당긴다.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(Camera))]
    public class ThirdPersonCamera : MonoBehaviour
    {
        public PlayerController player;
        public TopDownCamera topDown;
        public PlayerTools tools;

        [Header("Mouse (감도·반전은 GameSettings)")]
        public float minPitch = -30f;
        public float maxPitch = 70f;

        [Header("Distance (wheel)")]
        public float distance = 4f;
        public float minDistance = 1.6f;
        public float maxDistance = 7f;
        public float zoomStep = 0.8f;
        public float aimDistance = 2.4f;
        [Tooltip("벽에 막혀도 이 거리보다 가까이 오지 않는다(대신 카메라를 들어 올린다)")]
        public float minCameraDistance = 0.8f;
        public float maxRaise = 1.2f;

        [Header("Framing")]
        public float standPivotHeight = 1.6f;
        public float crouchPivotHeight = 1.05f;
        public Vector3 shoulderOffset = new Vector3(0.45f, 0f, 0f);
        public Vector3 aimShoulderOffset = new Vector3(0.7f, 0.1f, 0f);
        public float followSharpness = 14f;

        [Header("Collision")]
        public float collisionRadius = 0.25f;
        public LayerMask collisionMask = ~0;
        public float fadePlayerDistance = 1.3f;
        public float hidePlayerDistance = 0.45f;

        [Header("FOV")]
        public float runFovBoost = 6f;
        public float topDownFov = 38f;

        [Header("Title orbit")]
        public Vector3 orbitCenter = new Vector3(-12f, 0f, 0f);
        public float orbitRadius = 34f;
        public float orbitHeight = 19f;
        public float orbitSpeed = 4f;

        public bool startTopDown;

        public bool IsTopDown { get; private set; }
        public float Yaw => yaw;
        public float Pitch => pitch;
        public float CurrentDistance => currentDist;
        public float CurrentRaise => raise;

        float yaw, pitch = 15f, targetDist, currentDist, pivotHeight, raise, orbitAngle = 200f, backLimit = 99f;
        Vector3 smoothPivot;
        Camera cam;
        readonly List<Renderer> playerRenderers = new List<Renderer>();
        CameraFadeable playerFade;
        bool playerHidden;
        readonly Dictionary<Collider, bool> ignoreCache = new Dictionary<Collider, bool>();
        readonly RaycastHit[] hits = new RaycastHit[24];

        void Start()
        {
            cam = GetComponent<Camera>();
            if (player == null) player = FindAnyObjectByType<PlayerController>();
            if (topDown == null) topDown = GetComponent<TopDownCamera>();
            if (tools == null && player != null) tools = player.GetComponent<PlayerTools>();
            if (player != null)
            {
                yaw = player.transform.eulerAngles.y;
                foreach (var r in player.GetComponentsInChildren<Renderer>())
                    if (r.enabled && !(r is LineRenderer)) playerRenderers.Add(r);
                playerFade = player.GetComponentInChildren<CameraFadeable>();
            }
            targetDist = currentDist = distance;
            pivotHeight = standPivotHeight;
            smoothPivot = Pivot();
            SetTopDown(startTopDown);
            if (cam != null) cam.fieldOfView = GameSettings.Fov;
        }

        public void SetAngles(float newYaw, float newPitch)
        {
            yaw = newYaw;
            pitch = Mathf.Clamp(newPitch, minPitch, maxPitch);
        }

        /// <summary>플레이 시작 시 플레이어 등 뒤로 바로 옮긴다(타이틀 연출 카메라에서 전환)</summary>
        public void SnapBehindPlayer()
        {
            if (player == null) return;
            yaw = player.transform.eulerAngles.y;
            pitch = 15f;
            pivotHeight = player.IsCrouching ? crouchPivotHeight : standPivotHeight;
            smoothPivot = Pivot();
            raise = 0f;
            currentDist = targetDist;
            LateUpdate();
        }

        public void SetTopDown(bool on)
        {
            IsTopDown = on;
            if (topDown != null)
            {
                topDown.enabled = on;
                if (on) topDown.Snap();
            }
            if (cam != null && on) cam.fieldOfView = topDownFov;
            if (on) ShowPlayer(true);
        }

        public static void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        void Update()
        {
            if (!GameManager.GameplayActive) return;
            if (GameUI.Instance != null && GameUI.Instance.PuzzleOpen) { if (Cursor.lockState == CursorLockMode.Locked) LockCursor(false); return; }
            if (Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked) LockCursor(true);
            if (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.V)) SetTopDown(!IsTopDown);
        }

        Vector3 Pivot() => player != null ? player.transform.position + Vector3.up * pivotHeight : transform.position;

        void LateUpdate()
        {
            if (player == null || cam == null) return;
            var gm = GameManager.Instance;
            if (gm != null && (gm.Flow == GameManager.FlowState.Title || gm.Flow == GameManager.FlowState.Briefing))
            {
                Orbit(Time.unscaledDeltaTime);
                return;
            }
            if (IsTopDown) return;
            if (gm != null && gm.Flow == GameManager.FlowState.Paused) return;
            Step(Time.deltaTime);
        }

        /// <summary>카메라 한 프레임 계산(테스트에서는 dt를 직접 넘겨 여러 번 호출)</summary>
        public void Step(float dt)
        {

            if (GameManager.GameplayActive && Cursor.lockState == CursorLockMode.Locked)
            {
                float sens = GameSettings.MouseSensitivity;
                yaw += Input.GetAxis("Mouse X") * sens;
                pitch -= Input.GetAxis("Mouse Y") * sens * (GameSettings.InvertY ? -1f : 1f);
                float wheel = Input.GetAxis("Mouse ScrollWheel");
                if (Mathf.Abs(wheel) > 0.0001f)
                    targetDist = Mathf.Clamp(targetDist - Mathf.Sign(wheel) * zoomStep, minDistance, maxDistance);
            }
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

            bool aiming = tools != null && tools.Aiming;
            float wantPivot = player.IsCrouching ? crouchPivotHeight : standPivotHeight;
            pivotHeight = Mathf.Lerp(pivotHeight, wantPivot, 1f - Mathf.Exp(-8f * dt));
            smoothPivot = Vector3.Lerp(smoothPivot, Pivot(), 1f - Mathf.Exp(-followSharpness * dt));

            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);

            // 1) 피벗 → 어깨 지점(옆벽에 붙었을 때 어깨 오프셋이 벽을 뚫지 않게)
            Vector3 side = rot * (aiming ? aimShoulderOffset : shoulderOffset);
            Vector3 shoulder = smoothPivot;
            float sideLen = side.magnitude;
            if (sideLen > 0.001f)
            {
                float allowedSide = CastDistance(smoothPivot, side / sideLen, sideLen);
                shoulder = smoothPivot + side / sideLen * allowedSide;
            }

            // 2) 어깨 → 뒤쪽. 0.8m보다 가까워지면 0으로 당기는 대신 카메라를 들어 올린다.
            Vector3 back = rot * Vector3.back;
            float want = aiming ? Mathf.Min(aimDistance, targetDist) : targetDist;
            float allowed = CastDistance(shoulder, back, want);
            float wantRaise = 0f;
            if (allowed < minCameraDistance && want > minCameraDistance)
            {
                float upRoom = CastDistance(shoulder, Vector3.up, maxRaise);
                float bestA = allowed;
                backLimit = want;
                for (float r = 0.3f; r <= maxRaise + 0.001f; r += 0.3f)
                {
                    if (r > upRoom) break;
                    float a2 = CastDistance(shoulder + Vector3.up * r, back, want);
                    // 들어 올린 자리에서 플레이어가 보여야 한다(벽 위로 넘어가는 자리는 제외)
                    Vector3 cand = shoulder + Vector3.up * r + back * a2;
                    Vector3 toCand = cand - smoothPivot;
                    float cl = toCand.magnitude;
                    if (cl > 0.05f && CastDistance(smoothPivot, toCand / cl, cl) < cl - 0.06f) continue;
                    if (a2 > bestA + 0.05f) { bestA = a2; wantRaise = r; }
                    if (a2 >= minCameraDistance) break;
                }
                if (bestA < minCameraDistance)
                {
                    // 뒤가 높은 벽이면 머리 위에서 내려다본다(카메라–플레이어 거리는 들어 올린 높이만큼 확보)
                    wantRaise = Mathf.Min(upRoom, maxRaise);
                    backLimit = Mathf.Max(allowed, 0.05f);
                }
            }
            raise = Mathf.Lerp(raise, wantRaise, 1f - Mathf.Exp(-(wantRaise > raise ? 12f : 4f) * dt));
            Vector3 raisedShoulder = shoulder + Vector3.up * raise;
            // 거리는 항상 실제(들어 올리는 중인) 높이에서 다시 잰다: 올라가는 동안 벽을 뚫지 않는다
            float finalAllowed = raise > 0.01f ? Mathf.Min(CastDistance(raisedShoulder, back, want), wantRaise > 0f ? backLimit : want) : allowed;
            if (finalAllowed < currentDist) currentDist = finalAllowed;
            else currentDist = Mathf.Lerp(currentDist, finalAllowed, 1f - Mathf.Exp(-5f * dt));

            Vector3 pos = raisedShoulder + back * currentDist;
            Quaternion finalRot = rot;
            if (raise > 0.01f)
            {
                // 들어 올린 만큼 플레이어를 내려다본다
                Vector3 look = (shoulder + rot * Vector3.forward * 2f) - pos;
                if (look.sqrMagnitude > 0.01f)
                    finalRot = Quaternion.Slerp(rot, Quaternion.LookRotation(look, Vector3.up), Mathf.Clamp01(raise / maxRaise));
            }
            transform.SetPositionAndRotation(pos, finalRot);

            float fov = GameSettings.Fov + (player.IsRunning ? runFovBoost : 0f) - (aiming ? 6f : 0f);
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, fov, 1f - Mathf.Exp(-6f * dt));

            UpdateFades(smoothPivot, pos);

            float camToPivot = Vector3.Distance(pos, smoothPivot);
            if (playerFade != null && camToPivot < fadePlayerDistance) playerFade.SetTarget(Mathf.Lerp(0.25f, 1f, Mathf.InverseLerp(hidePlayerDistance, fadePlayerDistance, camToPivot)));
            ShowPlayer(camToPivot > hidePlayerDistance);
        }

        void Orbit(float dt)
        {
            orbitAngle += orbitSpeed * dt;
            float a = orbitAngle * Mathf.Deg2Rad;
            Vector3 pos = orbitCenter + new Vector3(Mathf.Sin(a) * orbitRadius, orbitHeight, Mathf.Cos(a) * orbitRadius);
            transform.SetPositionAndRotation(pos, Quaternion.LookRotation(orbitCenter + Vector3.up * 2f - pos, Vector3.up));
            cam.fieldOfView = 50f;
            ShowPlayer(true);
        }

        /// <summary>카메라와 피벗 사이를 가리는 큰 소품을 반투명하게</summary>
        void UpdateFades(Vector3 pivot, Vector3 camPos)
        {
            Vector3 d = pivot - camPos;
            float len = d.magnitude;
            if (len < 0.01f) return;
            var ray = new Ray(camPos, d / len);
            foreach (var f in CameraFadeable.All)
            {
                if (f == null || f == playerFade) continue;
                var b = f.WorldBounds;
                b.Expand(0.3f);
                if (b.Contains(camPos) || (b.IntersectRay(ray, out float hit) && hit < len - 0.2f))
                    f.SetTarget(f.fadedAlpha);
            }
        }

        float CastDistance(Vector3 origin, Vector3 dir, float maxDist)
        {
            int n = Physics.SphereCastNonAlloc(origin, collisionRadius, dir, hits, maxDist, collisionMask, QueryTriggerInteraction.Ignore);
            float best = maxDist;
            for (int i = 0; i < n; i++)
            {
                var c = hits[i].collider;
                if (IsIgnored(c)) continue;
                // 시작부터 겹친 경우(distance 0)도 당겨온다
                float dd = Mathf.Max(0f, hits[i].distance - 0.05f);
                if (dd < best) best = dd;
            }
            return best;
        }

        bool IsIgnored(Collider c)
        {
            if (!ignoreCache.TryGetValue(c, out bool ig))
            {
                ig = c.GetComponentInParent<PlayerController>() != null || c.GetComponentInParent<GuardAI>() != null ||
                     c.GetComponentInParent<ThrownObject>() != null;
                if (!ig)
                {
                    var f = c.GetComponentInParent<CameraFadeable>();
                    ig = f != null && f.cameraPassThrough;
                }
                ignoreCache[c] = ig;
            }
            return ig;
        }

        void ShowPlayer(bool show)
        {
            bool hide = !show;
            if (hide == playerHidden) return;
            playerHidden = hide;
            foreach (var r in playerRenderers) if (r != null) r.enabled = show;
        }
    }
}
