using UnityEngine;
using UnityEngine.Rendering;

namespace ShadowProtocol
{
    /// <summary>턴테이블 카메라: 와이어프레임 보기. SRP(HDRP)에서는 OnPreRender가 불리지 않아 카메라 렌더 이벤트로 GL.wireframe을 켠다.</summary>
    [RequireComponent(typeof(Camera))]
    public class TurntableCamera : MonoBehaviour
    {
        public bool wireframe;
        Camera cam;

        void Awake() { cam = GetComponent<Camera>(); }

        void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += Begin;
            RenderPipelineManager.endCameraRendering += End;
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= Begin;
            RenderPipelineManager.endCameraRendering -= End;
            GL.wireframe = false;
        }

        void Begin(ScriptableRenderContext ctx, Camera c) { if (c == cam) GL.wireframe = wireframe; }
        void End(ScriptableRenderContext ctx, Camera c) { if (c == cam) GL.wireframe = false; }

        // 빌트인 파이프라인 대비
        void OnPreRender() { GL.wireframe = wireframe; }
        void OnPostRender() { GL.wireframe = false; }
    }
}
