using UnityEngine;

namespace WallHitSound.Services
{
    /// <summary>
    /// 火花 1 粒の動き。寿命が尽きたら破棄せず非アクティブにしてプールへ返す。
    /// （以前は Destroy していたため、衝突のたびに設定した数だけ GameObject を
    /// 作っては捨てていた）
    /// </summary>
    public class TransientMover : MonoBehaviour
    {
        public Vector3 Velocity; // 初速: 0.4〜1.5 程度が控えめ
        public float Drag = 0.0f; // 減衰係数: 6.0〜12.0 で素早く減速
        public float Lifetime = 1.0f; // 寿命: TrailRenderer.time + 0.01〜0.03 目安
        private float _elapsed;

        /// <summary>使い回すときに寿命を測り直す。</summary>
        public void Restart()
        {
            _elapsed = 0f;
        }

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
                gameObject.SetActive(false);
            }
        }
    }
}
