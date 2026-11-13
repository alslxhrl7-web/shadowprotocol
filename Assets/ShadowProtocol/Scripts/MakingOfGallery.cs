using System;
using UnityEngine;

namespace ShadowProtocol
{
    /// <summary>
    /// 제작 공정(Making Of) 갤러리 데이터 + 3D 턴테이블 뷰어.
    /// 모델별 공정 이미지(VARCO 생성 이미지 → Blender 정리 후 형태/텍스처 → 게임 안)와 실제 게임 모델을
    /// 텍스처 / 회색 클레이 / 와이어프레임으로 돌려 본다(삼각형 수·Mesh LOD 단계·텍스처 크기 표시).
    /// </summary>
    public class MakingOfGallery : MonoBehaviour
    {
        [Serializable]
        public class Stage
        {
            public string caption;
            public Sprite image;
            [Tooltip("이미지 대신(또는 이미지가 없을 때) 보여 줄 글(예: VARCO 프롬프트)")]
            [TextArea(3, 10)] public string text;
        }

        [Serializable]
        public class Entry
        {
            public string title;
            [TextArea] public string note;
            public Stage[] stages;
            public GameObject model;
            public RuntimeAnimatorController controller;
            public float yaw = 30f;
            [Tooltip("고품질 리빌드 등으로 교체 예정")]
            public bool pending;
        }

        public Entry[] entries;
        public Material clayMat;
        public Vector3 stagePosition = new Vector3(600f, -300f, 600f);
        public float spinSpeed = 28f;

        public enum ViewMode { Textured, Clay, Wireframe }
        public ViewMode Mode { get; private set; }
        public RenderTexture Texture => rt;
        public string Stats { get; private set; }
        public int Current { get; private set; } = -1;

        Camera cam;
        TurntableCamera tcam;
        RenderTexture rt;
        Transform pivot;
        GameObject inst;
        Renderer[] rends;
        Material[][] original;

        void EnsureStage()
        {
            if (cam != null) return;
            var root = new GameObject("TurntableStage").transform;
            root.SetParent(transform, false);
            root.position = stagePosition;
            pivot = new GameObject("Pivot").transform;
            pivot.SetParent(root, false);
            rt = new RenderTexture(1024, 768, 24) { name = "TurntableRT", antiAliasing = 4 };
            var camGO = new GameObject("TurntableCamera");
            camGO.transform.SetParent(root, false);
            cam = camGO.AddComponent<Camera>();
            cam.targetTexture = rt;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.07f, 0.09f, 0.12f);
            cam.fieldOfView = 30f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 60f;
            cam.enabled = false;
            tcam = camGO.AddComponent<TurntableCamera>();
            var key = new GameObject("Key").AddComponent<Light>();
            key.transform.SetParent(root, false);
            key.type = LightType.Point;
            key.range = 30f;
            key.intensity = 2.2f;
            key.transform.localPosition = new Vector3(4f, 6f, -5f);
            key.renderMode = LightRenderMode.ForcePixel;
            var fill = new GameObject("Fill").AddComponent<Light>();
            fill.transform.SetParent(root, false);
            fill.type = LightType.Point;
            fill.range = 30f;
            fill.intensity = 1.1f;
            fill.color = new Color(0.7f, 0.8f, 1f);
            fill.transform.localPosition = new Vector3(-5f, 3f, 4f);
            fill.renderMode = LightRenderMode.ForcePixel;
        }

        public void Show(int index)
        {
            EnsureStage();
            Hide();
            Current = index;
            if (entries == null || index < 0 || index >= entries.Length || entries[index].model == null) { Stats = "3D 모델 없음"; return; }
            var e = entries[index];
            inst = Instantiate(e.model, pivot);
            inst.transform.localPosition = Vector3.zero;
            foreach (var c in inst.GetComponentsInChildren<Collider>()) c.enabled = false;
            foreach (var lg in inst.GetComponentsInChildren<LODGroup>()) lg.enabled = false;
            var an = inst.GetComponentInChildren<Animator>();
            if (an != null)
            {
                an.runtimeAnimatorController = e.controller;
                an.applyRootMotion = false;
                an.updateMode = AnimatorUpdateMode.UnscaledTime;
                an.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
            rends = inst.GetComponentsInChildren<Renderer>();
            original = new Material[rends.Length][];
            for (int i = 0; i < rends.Length; i++) original[i] = rends[i].sharedMaterials;

            // 크기 맞추기: 가장 긴 변 기준으로 카메라 거리 조정
            var b = new Bounds(pivot.position, Vector3.zero);
            bool has = false;
            foreach (var r in rends) { if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds); }
            inst.transform.position += pivot.position - new Vector3(b.center.x, b.min.y, b.center.z);
            float size = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            float dist = size / (2f * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad)) * 1.35f + size * 0.5f;
            cam.transform.position = pivot.position + new Vector3(0f, b.size.y * 0.55f + size * 0.25f, -dist);
            cam.transform.LookAt(pivot.position + Vector3.up * b.size.y * 0.45f);
            pivot.localRotation = Quaternion.Euler(0f, e.yaw, 0f);
            cam.enabled = true;
            ComputeStats();
            SetMode(Mode);
        }

        public void Hide()
        {
            if (inst != null) Destroy(inst);
            inst = null;
            if (cam != null) cam.enabled = false;
        }

        public void SetMode(ViewMode m)
        {
            Mode = m;
            if (tcam != null) tcam.wireframe = m == ViewMode.Wireframe;
            if (rends == null) return;
            for (int i = 0; i < rends.Length; i++)
            {
                if (rends[i] == null) continue;
                if (m == ViewMode.Textured) rends[i].sharedMaterials = original[i];
                else
                {
                    var mats = new Material[original[i].Length];
                    for (int k = 0; k < mats.Length; k++) mats[k] = clayMat;
                    rends[i].sharedMaterials = mats;
                }
            }
            if (cam != null) cam.backgroundColor = m == ViewMode.Wireframe ? new Color(0.02f, 0.03f, 0.05f) : new Color(0.07f, 0.09f, 0.12f);
        }

        void ComputeStats()
        {
            long tris = 0, lastLod = 0;
            int lods = 1, texW = 0, texH = 0;
            foreach (var r in rends)
            {
                Mesh mesh = null;
                if (r is SkinnedMeshRenderer smr) mesh = smr.sharedMesh;
                else { var mf = r.GetComponent<MeshFilter>(); if (mf != null) mesh = mf.sharedMesh; }
                if (mesh == null) continue;
                lods = Mathf.Max(lods, mesh.lodCount);
                for (int s = 0; s < mesh.subMeshCount; s++)
                {
                    tris += mesh.GetIndexCount(s) / 3;
                    if (mesh.lodCount > 1) lastLod += mesh.GetLod(s, mesh.lodCount - 1).indexCount / 3;
                    else lastLod += mesh.GetIndexCount(s) / 3;
                }
                foreach (var mat in r.sharedMaterials)
                {
                    if (mat == null || mat.mainTexture == null) continue;
                    texW = Mathf.Max(texW, mat.mainTexture.width);
                    texH = Mathf.Max(texH, mat.mainTexture.height);
                }
            }
            Stats = $"삼각형 {tris:N0}" + (lods > 1 ? $"  ·  Mesh LOD {lods}단계(가장 먼 단계 {lastLod:N0})" : "") +
                    (texW > 0 ? $"  ·  텍스처 {texW}×{texH}" : "  ·  텍스처 없음");
        }

        void Update()
        {
            if (inst != null && pivot != null) pivot.Rotate(0f, spinSpeed * Time.unscaledDeltaTime, 0f, Space.World);
        }

        void OnDestroy()
        {
            if (rt != null) rt.Release();
        }
    }
}
