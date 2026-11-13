using UnityEngine;

namespace ShadowProtocol
{
    /// <summary>턴테이블 카메라: 와이어프레임 보기(Built-in 파이프라인의 GL.wireframe)</summary>
    [RequireComponent(typeof(Camera))]
    public class TurntableCamera : MonoBehaviour
    {
        public bool wireframe;
        void OnPreRender() { GL.wireframe = wireframe; }
        void OnPostRender() { GL.wireframe = false; }
    }
}
