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
        // 웅크린 머리 꼭대기 0.89~0.94m(8차 클립)에 맞춤: 1.05에서는 기본 줌에서 캐릭터가 서 있을 때보다 화면 높이의 3% 아래에 잡혔다(2026-11-25 측정)
        public float crouchPivotHeight = 0.95f;
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

        float yaw, pitch = 15f, targetDist, currentDist, pivotHeight, raise, orbitAngle = 200f, backLimit = 99f, sideShift;
        // 뒤·위가 모두 막혔을 때 시험해 볼 옆 위치(피벗 기준 오른쪽 +, m)
        static readonly float[] sideCandidates = { 1.1f, -0.6f, -1.1f };
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
            ApplyLayerCulling(cam);
        }

        public const string SmallDetailLayer = "SmallDetail";
        public const float SmallDetailCullDistance = 30f;

        /// <summary>작은 장식 층은 30m 넘으면 그리지 않는다(Camera.layerCullDistances는 씬에 저장되지 않아 실행 때 설정)</summary>
        public static void ApplyLayerCulling(Camera c)
        {
            if (c == null) return;
            int l = LayerMask.NameToLayer(SmallDetailLayer);
            if (l < 0) return;
            var d = new float[32];
            d[l] = SmallDetailCullDistance;
            c.layerCullDistances = d;
            c.layerCullSpherical = true;
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
            sideShift = 0f;
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
            float udt = Time.unscaledDeltaTime > 0f ? Mathf.Min(Time.unscaledDeltaTime, 0.1f) : dt;
            crouchBlend = Mathf.Lerp(crouchBlend, player.IsCrouching ? 1f : 0f, 1f - Mathf.Exp(-5f * dt));
            runBlend = Mathf.Lerp(runBlend, player.IsRunning ? 1f : 0f, 1f - Mathf.Exp(-3f * dt));
            UpdateFocus(udt);
            float wantPivot = player.IsCrouching ? crouchPivotHeight : standPivotHeight;
            pivotHeight = Mathf.Lerp(pivotHeight, wantPivot, 1f - Mathf.Exp(-8f * dt));
            smoothPivot = Vector3.Lerp(smoothPivot, Pivot(), 1f - Mathf.Exp(-followSharpness * dt));

            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);

            // 1) 피벗 → 어깨 지점(옆벽에 붙었을 때 어깨 오프셋이 벽을 뚫지 않게)
            Vector3 right = rot * Vector3.right;
            Vector3 side = rot * (aiming ? aimShoulderOffset : Vector3.Lerp(shoulderOffset, crouchShoulderOffset, crouchBlend));
            Vector3 shoulder = ClipOffset(smoothPivot, side);

            // 2) 어깨 → 뒤쪽. 0.8m보다 가까워지면 0으로 당기는 대신 카메라를 들어 올린다.
            Vector3 back = rot * Vector3.back;
            // 어쌔신 크리드식 구도: 웅크리면 가깝고 낮게(긴장감), 달리면 살짝 멀리(속도감)
            float stanceMul = Mathf.Lerp(1f, crouchDistanceScale, crouchBlend) * Mathf.Lerp(1f, runDistanceScale, runBlend);
            float want = aiming ? Mathf.Min(aimDistance, targetDist) : Mathf.Max(minDistance, targetDist * stanceMul);
            float allowed = CastDistance(shoulder, back, want);
            float wantRaise = 0f, wantSide = 0f;
            bool fallback = false;
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
                    fallback = true;
                }

                // 문틀 안에서 옆을 볼 때처럼 뒤도 위(상인방)도 막히면 카메라를 옆(문 통로 쪽)으로 비켜 세운다
                if (!aiming)
                {
                    Vector3 rc = shoulder + Vector3.up * wantRaise + back * (fallback ? backLimit : Mathf.Min(bestA, want));
                    float bestScore = (rc - smoothPivot).magnitude;
                    if (bestScore < 1.0f)
                    {
                        foreach (float s in sideCandidates)
                        {
                            Vector3 o = ClipOffset(smoothPivot, right * s);
                            float a2 = CastDistance(o, back, want);
                            Vector3 c2 = o + back * a2;
                            Vector3 toC = c2 - smoothPivot;
                            float sc = toC.magnitude;
                            if (sc < 0.05f || CastDistance(smoothPivot, toC / sc, sc) < sc - 0.06f) continue;
                            // 이미 비켜 선 쪽을 조금 더 선호해 좌우로 튀지 않게
                            if (sideShift * s > 0f) sc += 0.2f;
                            if (sc > bestScore + 0.15f) { bestScore = sc; wantSide = s; }
                        }
                        if (wantSide != 0f) { wantRaise = 0f; fallback = false; backLimit = want; }
                    }
                }
            }
            float baseSide = Vector3.Dot(side, right);
            float wantShift = wantSide != 0f ? wantSide - baseSide : 0f;
            sideShift = Mathf.Lerp(sideShift, wantShift, 1f - Mathf.Exp(-8f * dt));
            if (Mathf.Abs(sideShift) > 0.01f) shoulder = ClipOffset(smoothPivot, side + right * sideShift);

            raise = Mathf.Lerp(raise, wantRaise, 1f - Mathf.Exp(-(wantRaise > raise ? 12f : 4f) * dt));
            // 실제 어깨 지점에서 위 공간을 다시 재서 들어 올린 높이가 상인방을 넘지 않게
            float usedRaise = raise > 0.01f ? Mathf.Min(raise, CastDistance(shoulder, Vector3.up, raise)) : 0f;
            Vector3 raisedShoulder = shoulder + Vector3.up * usedRaise;
            // 거리는 항상 실제(들어 올리는·비켜 서는 중인) 지점에서 다시 잰다: 움직이는 동안 벽을 뚫지 않는다
            float finalAllowed = Mathf.Min(CastDistance(raisedShoulder, back, want), wantRaise > 0f ? backLimit : want);
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
            if (handheld) ApplyHandheld(dt, aiming, ref pos, ref finalRot);
            ApplyShake(udt, ref finalRot);
            ApplyShot(udt, ref pos, ref finalRot);
            transform.SetPositionAndRotation(pos, finalRot);

            float fov =GameSettings.Fov + (player.IsRunning ? runFovBoost : 0f) - (aiming ? 6f : 0f);
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, fov, 1f - Mathf.Exp(-6f * dt));

            UpdateFades(smoothPivot, pos);

            float camToPivot = Vector3.Distance(pos, smoothPivot);
            if (playerFade != null && camToPivot < fadePlayerDistance) playerFade.SetTarget(Mathf.Lerp(0.25f, 1f, Mathf.InverseLerp(hidePlayerDistance, fadePlayerDistance, camToPivot)));
            ShowPlayer(camToPivot > hidePlayerDistance);
        }

        [Header("자세별 구도(어쌔신 크리드식)")]
        [Tooltip("웅크리면 카메라 거리 배율(기본 4m → 약 2.5m)")]
        public float crouchDistanceScale = 0.62f;
        [Tooltip("달리면 카메라 거리 배율")]
        public float runDistanceScale = 1.12f;
        public Vector3 crouchShoulderOffset = new Vector3(0.6f, 0f, 0f);
        float crouchBlend, runBlend;

        // ---------------- 연출(ActionDirector가 호출) ----------------
        Transform focusTarget;
        float focusUntil;
        float shake, shakeTime;
        Vector3 shotPos, shotLook;
        float shotUntil, shotWeight;

        /// <summary>잠깐 대상 쪽으로 시선을 돌린다(들킨 순간 그 경비 쪽). 이후 조작은 그대로.</summary>
        public void Focus(Transform target, float realSeconds)
        {
            focusTarget = target;
            focusUntil = Time.unscaledTime + realSeconds;
        }

        /// <summary>충격 흔들림(1 = 강한 타격)</summary>
        public void Shake(float amount) { shake = Mathf.Max(shake, amount); }

        /// <summary>잠깐 지정한 자리에서 지정한 곳을 보는 샷(암살 측면 클로즈업)</summary>
        public void CinematicShot(Vector3 pos, Vector3 lookAt, float realSeconds)
        {
            shotPos = pos;
            shotLook = lookAt;
            shotUntil = Time.unscaledTime + realSeconds;
        }

        void UpdateFocus(float udt)
        {
            if (focusTarget == null || Time.unscaledTime > focusUntil) { focusTarget = null; return; }
            Vector3 d = focusTarget.position + Vector3.up * 1.4f - (player.transform.position + Vector3.up * pivotHeight);
            Vector3 flat = new Vector3(d.x, 0f, d.z);
            if (flat.sqrMagnitude < 0.01f) return;
            float ty = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
            float k = 1f - Mathf.Exp(-7f * udt);
            yaw = Mathf.LerpAngle(yaw, ty, k);
            pitch = Mathf.Lerp(pitch, Mathf.Clamp(12f - Mathf.Atan2(d.y, flat.magnitude) * Mathf.Rad2Deg, minPitch, 30f), k * 0.6f);
        }

        void ApplyShake(float udt, ref Quaternion rot)
        {
            if (shake <= 0.001f) { shake = 0f; return; }
            shakeTime += udt * 28f;
            float a = shake * shake * 2.2f; // 도
            float x = (Mathf.PerlinNoise(shakeTime, 1.3f) - 0.5f) * 2f * a;
            float y = (Mathf.PerlinNoise(4.1f, shakeTime) - 0.5f) * 2f * a;
            float z = (Mathf.PerlinNoise(shakeTime, 7.7f) - 0.5f) * 2f * a * 0.6f;
            rot = rot * Quaternion.Euler(x, y, z);
            shake = Mathf.MoveTowards(shake, 0f, udt * 2.2f);
        }

        void ApplyShot(float udt, ref Vector3 pos, ref Quaternion rot)
        {
            float want = Time.unscaledTime < shotUntil ? 1f : 0f;
            shotWeight = Mathf.MoveTowards(shotWeight, want, udt * (want > shotWeight ? 7f : 2.5f));
            if (shotWeight <= 0.001f) return;
            float w = Mathf.SmoothStep(0f, 1f, shotWeight);
            Vector3 look = shotLook - shotPos;
            if (look.sqrMagnitude < 0.0001f) return;
            pos = Vector3.Lerp(pos, shotPos, w);
            rot = Quaternion.Slerp(rot, Quaternion.LookRotation(look, Vector3.up), w);
        }

        [Header("핸드헬드(사실감)")]
        [Tooltip("걸음에 맞춘 미세한 상하·좌우 흔들림과 손에 든 카메라 같은 느린 떨림")]
        public bool handheld = true;
        public float swayIdleDeg = 0.15f;
        public float swayMoveDeg = 0.3f;
        public float bobWalk = 0.012f;
        public float bobRun = 0.03f;
        public float bobCrouch = 0.008f;
        float bobPhase, swayTime, moveBlend;

        void ApplyHandheld(float dt, bool aiming, ref Vector3 pos, ref Quaternion rot)
        {
            Vector3 v = player.PlanarVelocity;
            float spd = v.magnitude;
            moveBlend = Mathf.Lerp(moveBlend, Mathf.Clamp01(spd / Mathf.Max(0.1f, player.walkSpeed)), 1f - Mathf.Exp(-4f * dt));
            // 걸음 수/초: 웅크림 1.7, 걷기 1.8 → 달리기 2.8(한 걸음마다 한 번 출렁임)
            float runT = Mathf.InverseLerp(player.walkSpeed, player.runSpeed, spd);
            float steps = spd < 0.1f ? 0f : (player.IsCrouching ? 1.7f : Mathf.Lerp(1.8f, 2.8f, runT));
            bobPhase += dt * steps * Mathf.PI;
            float amp = player.IsCrouching ? bobCrouch : Mathf.Lerp(bobWalk, bobRun, runT);
            if (aiming) amp *= 0.4f;
            float bob = (Mathf.Abs(Mathf.Sin(bobPhase)) - 0.5f) * 2f * amp * moveBlend;
            float side = Mathf.Sin(bobPhase) * amp * 0.5f * moveBlend; // 왼발·오른발에 따라 좌우로

            swayTime += dt;
            float sway = Mathf.Lerp(swayIdleDeg, swayMoveDeg, moveBlend) * (player.IsRunning ? 1.6f : 1f) * (aiming ? 0.5f : 1f);
            float nx = (Mathf.PerlinNoise(swayTime * 0.35f, 0.17f) - 0.5f) * 2f * sway;
            float ny = (Mathf.PerlinNoise(3.7f, swayTime * 0.29f) - 0.5f) * 2f * sway;
            float nz = (Mathf.PerlinNoise(swayTime * 0.23f, 8.1f) - 0.5f) * sway;

            pos += rot * new Vector3(side, bob, 0f);
            rot = rot * Quaternion.Euler(nx, ny, nz);
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

        /// <summary>origin에서 offset만큼 옮기되 벽에 막히면 그 앞까지만</summary>
        Vector3 ClipOffset(Vector3 origin, Vector3 offset)
        {
            float len = offset.magnitude;
            if (len < 0.001f) return origin;
            return origin + offset / len * CastDistance(origin, offset / len, len);
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
