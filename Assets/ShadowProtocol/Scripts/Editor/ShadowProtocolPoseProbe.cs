using System.Collections.Generic;
using UnityEngine;

namespace ShadowProtocol.EditorTools
{
    /// <summary>
    /// 자세 측정(Blender intended_pose와 같은 항목). 플레이 모드·클립 샘플에서 여러 프레임을 모아 평균/범위를 낸다.
    /// 골반 높이 = Hips 뼈 높이, 몸통 기울기 = 골반→목 선과 수직선의 각,
    /// 어깨 굽힘(flex) = 위팔의 몸통-아래 축 기준 앞쪽 각(Blender defs와 같음), 팔꿈치 = 위팔과 아래팔 사이 굽힘(0 = 펴짐),
    /// 손-무릎 = 같은 쪽 무릎(LeftLeg/RightLeg 뼈) 기준 손목의 앞·위·바깥 거리, 발 간격 = 두 발목의 좌우 거리, 머리 꼭대기 = 메시 최고점,
    /// 팔-다리 틈 = 메시 표면 거리(팔 정점: 위팔·아래팔·손 / 다리 정점: 허벅지·정강이, 가장 가까운 다리 정점 법선 기준 음수 = 안으로 파고듦).
    /// </summary>
    public class ShadowProtocolPoseProbe
    {
        readonly Transform root, hips, neck, head, la, lf, lh, ra, rf, rh, lfoot, rfoot, lknee, rknee;
        readonly SkinnedMeshRenderer smr;
        readonly Mesh baked = new Mesh();
        readonly List<float> pelvis = new List<float>(), lean = new List<float>(), top = new List<float>(),
            flL = new List<float>(), flR = new List<float>(), elL = new List<float>(), elR = new List<float>(), feet = new List<float>(),
            kfL = new List<float>(), kuL = new List<float>(), koL = new List<float>(), kfR = new List<float>(), kuR = new List<float>(), koR = new List<float>(), gap = new List<float>();
        int penetratingFrames, gapFrames;
        readonly int[] armVerts, legVerts;

        public ShadowProtocolPoseProbe(Transform characterRoot, Animator an, SkinnedMeshRenderer skin)
        {
            root = characterRoot; smr = skin;
            Transform B(HumanBodyBones b, string fallback)
            {
                var t = an != null && an.isHuman ? an.GetBoneTransform(b) : null;
                if (t == null) foreach (var x in characterRoot.GetComponentsInChildren<Transform>()) if (x.name == fallback) { t = x; break; }
                return t;
            }
            hips = B(HumanBodyBones.Hips, "Hips"); neck = B(HumanBodyBones.Neck, "Neck"); head = B(HumanBodyBones.Head, "Head");
            la = B(HumanBodyBones.LeftUpperArm, "LeftArm"); lf = B(HumanBodyBones.LeftLowerArm, "LeftForeArm"); lh = B(HumanBodyBones.LeftHand, "LeftHand");
            ra = B(HumanBodyBones.RightUpperArm, "RightArm"); rf = B(HumanBodyBones.RightLowerArm, "RightForeArm"); rh = B(HumanBodyBones.RightHand, "RightHand");
            lfoot = B(HumanBodyBones.LeftFoot, "LeftFoot"); rfoot = B(HumanBodyBones.RightFoot, "RightFoot");
            lknee = B(HumanBodyBones.LeftLowerLeg, "LeftLeg"); rknee = B(HumanBodyBones.RightLowerLeg, "RightLeg");

            // 정점을 가장 큰 가중치 뼈로 분류(팔·다리)
            var arm = new List<int>(); var leg = new List<int>();
            if (smr != null && smr.sharedMesh != null)
            {
                var bones = smr.bones; var bw = smr.sharedMesh.boneWeights;
                for (int i = 0; i < bw.Length; i++)
                {
                    int bi = bw[i].boneIndex0;
                    if (bi < 0 || bi >= bones.Length || bones[bi] == null) continue;
                    string n = bones[bi].name;
                    if (n == "LeftArm" || n == "RightArm" || n.EndsWith("ForeArm") || n.EndsWith("Hand")) arm.Add(i);
                    else if (n.EndsWith("UpLeg") || n == "LeftLeg" || n == "RightLeg") leg.Add(i);
                }
            }
            armVerts = arm.ToArray(); legVerts = leg.ToArray();
        }

        public void Sample(bool withMesh)
        {
            Vector3 fwd = root.forward; fwd.y = 0f; fwd.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            Vector3 torso = neck.position - hips.position;
            pelvis.Add(hips.position.y - root.position.y);
            lean.Add(Vector3.Angle(torso, Vector3.up));
            Vector3 down = -torso.normalized;
            Vector3 torsoFwd = Vector3.Cross(right, down).normalized;
            if (Vector3.Dot(torsoFwd, fwd) < 0f) torsoFwd = -torsoFwd;
            float Flex(Vector3 upper) { var u = upper.normalized; return Mathf.Atan2(Vector3.Dot(u, torsoFwd), Vector3.Dot(u, down)) * Mathf.Rad2Deg; }
            flL.Add(Flex(lf.position - la.position)); flR.Add(Flex(rf.position - ra.position));
            elL.Add(Vector3.Angle(lf.position - la.position, lh.position - lf.position));
            elR.Add(Vector3.Angle(rf.position - ra.position, rh.position - rf.position));
            feet.Add(Mathf.Abs(Vector3.Dot(lfoot.position - rfoot.position, right)) * 100f);
            Vector3 dl = lh.position - lknee.position, dr = rh.position - rknee.position;
            kfL.Add(Vector3.Dot(dl, fwd) * 100f); kuL.Add(dl.y * 100f); koL.Add(-Vector3.Dot(dl, right) * 100f);
            kfR.Add(Vector3.Dot(dr, fwd) * 100f); kuR.Add(dr.y * 100f); koR.Add(Vector3.Dot(dr, right) * 100f);
            if (withMesh && smr != null)
            {
                smr.BakeMesh(baked, true);
                var verts = baked.vertices; var norms = baked.normals;
                var m = smr.transform.localToWorldMatrix;
                float mx = -9f;
                for (int i = 0; i < verts.Length; i++) { verts[i] = m.MultiplyPoint3x4(verts[i]); if (verts[i].y > mx) mx = verts[i].y; }
                top.Add(mx - root.position.y);
                if (armVerts.Length > 0 && legVerts.Length > 0)
                {
                    float best = 9f; int bestLeg = -1, bestArm = -1;
                    for (int a = 0; a < armVerts.Length; a += 2)
                    {
                        var pa = verts[armVerts[a]];
                        for (int l = 0; l < legVerts.Length; l += 2)
                        {
                            float d = (pa - verts[legVerts[l]]).sqrMagnitude;
                            if (d < best) { best = d; bestLeg = legVerts[l]; bestArm = armVerts[a]; }
                        }
                    }
                    if (bestLeg >= 0)
                    {
                        float dist = Mathf.Sqrt(best);
                        Vector3 n = m.MultiplyVector(norms[bestLeg]).normalized;
                        float signed = Vector3.Dot(verts[bestArm] - verts[bestLeg], n) < 0f ? -dist : dist;
                        gap.Add(signed * 100f); gapFrames++;
                        if (signed < -0.003f) penetratingFrames++;
                    }
                }
            }
        }

        static string R(List<float> l, string fmt)
        {
            if (l.Count == 0) return "-";
            float mn = float.MaxValue, mx = float.MinValue, s = 0f;
            foreach (var v in l) { mn = Mathf.Min(mn, v); mx = Mathf.Max(mx, v); s += v; }
            return $"{(s / l.Count).ToString(fmt)} ({mn.ToString(fmt)}~{mx.ToString(fmt)})";
        }

        public string Report() =>
            $"골반 {R(pelvis, "F3")}m | 몸통 기울기 {R(lean, "F1")}° | 머리 꼭대기 {R(top, "F2")}m | 어깨 굽힘 L {R(flL, "F1")}° R {R(flR, "F1")}° | 팔꿈치 L {R(elL, "F1")}° R {R(elR, "F1")}° | " +
            $"손-무릎 L 앞 {R(kfL, "F1")} 위 {R(kuL, "F1")} 바깥 {R(koL, "F1")}cm, R 앞 {R(kfR, "F1")} 위 {R(kuR, "F1")} 바깥 {R(koR, "F1")}cm | 발 간격 {R(feet, "F1")}cm | " +
            $"팔-다리 틈 {R(gap, "F2")}cm (파고든 프레임 {penetratingFrames}/{gapFrames})";

        public float PelvisAvg { get { float s = 0; foreach (var v in pelvis) s += v; return pelvis.Count > 0 ? s / pelvis.Count : 0; } }
        public float LeanAvg { get { float s = 0; foreach (var v in lean) s += v; return lean.Count > 0 ? s / lean.Count : 0; } }
        public int PenetratingFrames => penetratingFrames;
    }
}
