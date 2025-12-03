using UnityEngine;

namespace WallHitSound.Services
{
    public class TransientMover : MonoBehaviour
    {
        public Vector3 Velocity; // 初速: 0.4〜1.5 程度が控えめ
        public float Drag = 0.0f; // 減衰係数: 6.0〜12.0 で素早く減速
        public float Lifetime = 1.0f; // 寿命: TrailRenderer.time + 0.01〜0.03 目安
        private float _elapsed;

        void Update()
        {
            // Exponential damping toward zero to keep movement subtle
            if (Drag > 0.0f)
            {
                float k = 1.0f - Mathf.Exp(-Drag * Time.deltaTime);
                Velocity = Vector3.Lerp(Velocity, Vector3.zero, k);
            }
            transform.position += Velocity * Time.deltaTime;
            _elapsed += Time.deltaTime;
            if (_elapsed >= Lifetime)
            {
                Destroy(gameObject);
            }
        }
    }
}
