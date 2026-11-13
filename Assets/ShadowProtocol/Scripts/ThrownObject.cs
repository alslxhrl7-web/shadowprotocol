using UnityEngine;

namespace ShadowProtocol
{
    /// <summary>
    /// 던진 병/캔. 처음 부딪힌 곳에서 소리(3D)와 소음(NoiseSystem)을 내 경비를 유인한다.
    /// 병은 깨지고, 캔은 그 자리에 남아 다시 주울 수 있다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class ThrownObject : MonoBehaviour
    {
        public ThrowKind kind;
        public float noiseRadius = 11f;
        public GameObject pickupTemplate;

        bool landed;
        float life;

        public static System.Action<Vector3, float> Landed;

        void Update()
        {
            life += Time.deltaTime;
            if (!landed && life > 6f) Land(transform.position);
            if (transform.position.y < -20f) Destroy(gameObject);
        }

        void OnCollisionEnter(Collision c)
        {
            if (landed) return;
            if (c.collider.GetComponentInParent<PlayerController>() != null) return;
            Vector3 p = c.contactCount > 0 ? c.GetContact(0).point : transform.position;
            Land(p);
        }

        void Land(Vector3 p)
        {
            landed = true;
            var am = AudioManager.Instance;
            if (am != null) am.PlayAt(kind == ThrowKind.Bottle ? am.bottleBreak : am.canLand, p, 1f, 2f, 32f, Random.Range(0.95f, 1.05f));
            NoiseSystem.Emit(p, noiseRadius, gameObject);
            Landed?.Invoke(p, noiseRadius);
            Debug.Log($"[ShadowProtocol] 투척물 착지: {ThrowablePickup.KindName(kind)} ({p.x:0.0}, {p.z:0.0}) 소음 반경 {noiseRadius:0}m");
            if (kind == ThrowKind.Bottle)
            {
                Destroy(gameObject, 0.02f);
            }
            else
            {
                // 캔은 굴러가 멈춘 뒤 다시 주울 수 있다
                Invoke(nameof(BecomePickup), 1.6f);
            }
        }

        void BecomePickup()
        {
            var go = new GameObject("Pickup_Can (던진 것)");
            go.transform.position = new Vector3(transform.position.x, Mathf.Max(0f, transform.position.y - 0.05f), transform.position.z);
            var vis = transform;
            var pick = go.AddComponent<ThrowablePickup>();
            pick.kind = ThrowKind.Can;
            Destroy(GetComponent<Rigidbody>());
            foreach (var col in GetComponentsInChildren<Collider>()) Destroy(col);
            vis.SetParent(go.transform, true);
            vis.localPosition = Vector3.zero;
            vis.localRotation = Quaternion.identity;
            pick.visual = vis;
            Destroy(this);
        }
    }
}
