using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace ShadowProtocol
{
    /// <summary>
    /// 빌드에서 씬을 불러올 때 NavMeshAgent(네이티브 컴포넌트)가 NavMeshSurface.OnEnable(AddData)보다 먼저
    /// 활성화되면 "Failed to create agent because there is no valid NavMesh"가 나고 경비가 멈춘다.
    /// 그래서 경비의 NavMeshAgent는 꺼진 채로 씬에 저장하고, Start에서 이 도우미로 NavMesh 등록을 보장한 뒤 켠다.
    /// </summary>
    public static class NavBootstrap
    {
        static int checkedFrame = -1;
        static bool ready;

        /// <summary>NavMesh가 등록돼 있는지 확인하고, 없으면 씬의 NavMeshSurface 데이터를 등록(데이터가 없으면 굽기)한다.</summary>
        public static bool EnsureNavMesh()
        {
            if (checkedFrame == Time.frameCount) return ready;
            checkedFrame = Time.frameCount;
            int before = NavMesh.CalculateTriangulation().vertices.Length;
            if (before > 0) { ready = true; return true; }

            int added = 0, baked = 0;
            foreach (var s in NavMeshSurface.activeSurfaces)
            {
                if (s == null) continue;
                if (s.navMeshData == null) { s.BuildNavMesh(); baked++; }
                else { s.RemoveData(); s.AddData(); added++; }
            }
            int after = NavMesh.CalculateTriangulation().vertices.Length;
            ready = after > 0;
            Debug.Log($"[ShadowProtocol] NavMesh 보정: 시작 시 정점 {before} → 등록 {added}개·재굽기 {baked}개 후 정점 {after}");
            return ready;
        }

        /// <summary>에이전트를 가장 가까운 NavMesh 위에 놓고 켠다. 성공하면 true.</summary>
        public static bool EnableAgent(NavMeshAgent agent)
        {
            if (agent == null) return false;
            if (agent.enabled && agent.isOnNavMesh) return true;
            if (!EnsureNavMesh()) return false;
            var t = agent.transform;
            if (NavMesh.SamplePosition(t.position, out NavMeshHit hit, 2f, NavMesh.AllAreas)) t.position = hit.position;
            agent.enabled = true;
            if (!agent.isOnNavMesh && NavMesh.SamplePosition(t.position, out hit, 5f, NavMesh.AllAreas)) agent.Warp(hit.position);
            return agent.isOnNavMesh;
        }
    }
}
