using UnityEngine;
using UnityEngine.AI;

namespace ShadowProtocol
{
    /// <summary>
    /// 경비 이동을 사람처럼: NavMeshAgent는 경로·원하는 속도만 계산하고, 몸은 이 컴포넌트가 움직인다.
    /// - 몸이 향한 방향과 경로 방향이 많이 다르면 먼저 감속하고 몸을 돌린 뒤 출발한다(옆걸음·제자리 미끄러짐 방지).
    /// - 출발·정지에 가속·감속이 있다(순찰은 천천히, 추격은 빠르게).
    /// - 회전 속도는 사람 기준(걷기 약 0.7초에 180°).
    /// GuardAI가 Update에서 목적지·속도를 정한 뒤 LateUpdate에서 적용한다. 에이전트가 꺼지면(기절·끌려감) 아무것도 하지 않는다.
    /// </summary>
    [DefaultExecutionOrder(50)]
    [RequireComponent(typeof(NavMeshAgent))]
    public class GuardLocomotion : MonoBehaviour
    {
        [Header("가속(m/s²)")]
        public float walkAccel = 2.8f;
        public float runAccel = 6.5f;
        public float decel = 4.5f;

        [Header("회전(도/초)")]
        public float turnRateWalk = 250f;
        public float turnRateRun = 400f;
        [Tooltip("몸과 경로 방향 차이가 이 각도 이상이면 제자리에서 먼저 돈다")]
        public float turnInPlaceAngle = 80f;
        [Tooltip("이 각도 이하면 제 속도로 걷는다")]
        public float fullSpeedAngle = 20f;

        NavMeshAgent agent;
        GuardAI ai;
        float speed;

        public float Speed => speed;

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            ai = GetComponent<GuardAI>();
        }

        void LateUpdate()
        {
            if (agent == null || !agent.enabled || !agent.isOnNavMesh || (ai != null && ai.IsKnockedOut))
            {
                speed = 0f;
                return;
            }
            if (agent.updatePosition || agent.updateRotation)
            {
                agent.updatePosition = false;
                agent.updateRotation = false;
                agent.nextPosition = transform.position;
            }

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            // 다른 코드가 Warp로 옮겼으면(체크포인트·지휘 재배치) 몸도 그 자리로
            if ((agent.nextPosition - transform.position).sqrMagnitude > 1.5f * 1.5f)
            {
                transform.position = agent.nextPosition;
                speed = 0f;
                return;
            }

            Vector3 desired = agent.isStopped ? Vector3.zero : agent.desiredVelocity;
            desired.y = 0f;
            float targetSpeed = desired.magnitude;

            Vector3 fwd = transform.forward; fwd.y = 0f;
            float angle = targetSpeed > 0.05f ? Vector3.Angle(fwd, desired) : 0f;
            float align = Mathf.Clamp01(Mathf.InverseLerp(turnInPlaceAngle, fullSpeedAngle, angle));
            float want = targetSpeed * align;

            bool running = targetSpeed > 3f;
            float a = want > speed ? (running ? runAccel : walkAccel) : decel;
            speed = Mathf.MoveTowards(speed, want, a * dt);

            if (targetSpeed > 0.05f)
            {
                float rate = Mathf.Lerp(turnRateWalk, turnRateRun, Mathf.InverseLerp(1.5f, 4.2f, targetSpeed));
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(desired, Vector3.up), rate * dt);
            }

            // 경로 방향으로 이동하되 속도는 몸 방향 정렬에 묶는다 → 몸이 돌아선 뒤에야 걸음이 나간다
            Vector3 moveDir = targetSpeed > 0.05f ? desired / targetSpeed : fwd.normalized;
            Vector3 next = transform.position + moveDir * (speed * dt);
            agent.nextPosition = next;          // 에이전트가 NavMesh 위로 보정
            Vector3 p = agent.nextPosition;
            transform.position = new Vector3(p.x, p.y, p.z);
        }
    }
}
