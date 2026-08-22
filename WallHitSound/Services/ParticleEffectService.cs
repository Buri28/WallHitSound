using System.Collections.Generic;
using UnityEngine;
using WallHitSound.Services.Effects;

namespace WallHitSound.Services
{
    /// <summary>
    /// 半球状に赤〜オレンジの火花（スパーク）を出す。
    /// GameObject・マテリアル・色は使い回す。以前は 1 粒ごとに GameObject を作って捨て、
    /// さらに 1 粒ごとに Shader.Find と new Material をしていた（マテリアルは
    /// GameObject と一緒には破棄されないので、当たるたびに積み上がっていた）。
    /// 見た目（色の範囲・軌跡の長さ・飛び方・描画順）は変更前のまま。
    /// </summary>
    public static class ParticleEffectService
    {
        /// <summary>設定で指定できる最大数。</summary>
        public const int MaxCount = 200;

        // 生成半径（接触点近傍）。調整目安: 0.003f（極小）〜 0.30f（やや広め）
        private const float SpawnRadius = 0.30f;
        // 起点から少し離して放出（接触点から浮かせる）。0.01〜0.10 で調整可
        private const float StartOffset = 0.10f;
        // 表示時間（短いほど控えめ）: 0.03f〜0.10f 推奨
        private const float TrailTime = 0.10f;
        // 線幅（細いほど控えめ）: 0.0003f〜0.010f 推奨
        private const float TrailWidth = 0.005f;
        // 火花 1 粒の寿命
        private const float SparkLifetime = TrailTime + 0.015f;

        // プールは設定数の 2 倍持つ。1 回の Spawn でちょうど一周してしまうと、
        // 寿命（0.115 秒）以内に次のヒットが来たときに飛行中の粒を奪ってしまうため
        private const int PoolMultiplier = 2;

        private static GameObject _root;
        private static readonly List<TransientMover> _pool = new List<TransientMover>();
        private static int _next;

        /// <summary>
        /// 火花を設定数ぶん用意しておく。メニュー到達時・設定変更時に呼ぶ。
        /// </summary>
        public static void Prewarm(int count)
        {
            if (count <= 0) return;
            EnsurePool(Mathf.Min(count, MaxCount) * PoolMultiplier);
        }

        /// <summary>
        /// 接触点のまわりに火花を出す。
        /// </summary>
        public static void Spawn(Vector3 position, int count)
        {
            if (count <= 0) return;
            count = Mathf.Min(count, MaxCount);

            if (_pool.Count < count * PoolMultiplier)
            {
                // 設定変更のあとに Prewarm を通っていない場合の保険
                if (Plugin.VerboseLogs) Plugin.LogWarn("WallHitSound: Growing spark pool during play");
                EnsurePool(count * PoolMultiplier);
            }
            if (_pool.Count == 0) return;

            // 半球の向きをランダムに決定
            // 調整案: 衝突面の法線が取れる場合はその方向に置換すると自然（例: raycastHit.normal）
            Vector3 hemiNormal = Random.onUnitSphere;
            if (hemiNormal.sqrMagnitude < 1e-4f) hemiNormal = Vector3.up;
            hemiNormal.Normalize();

            for (int i = 0; i < count; i++)
            {
                var mover = _pool[_next];
                _next = (_next + 1) % _pool.Count;
                if (mover == null) continue;

                // 半球内ランダム方向
                Vector3 randomDir = Random.onUnitSphere;
                if (Vector3.Dot(randomDir, hemiNormal) < 0f) randomDir = -randomDir;

                // 円状（半球）に放出：randomDir を主体にし、法線寄与は控えめ（形状を円状に維持）
                Vector3 outward = (randomDir * 0.8f + hemiNormal * 0.2f).normalized;
                // 発生位置を起点から少し離した位置へ移動
                Vector3 spawnPos = position + randomDir * Random.Range(0.0f, SpawnRadius) + outward * StartOffset;

                var go = mover.gameObject;
                go.SetActive(false);          // 使い回しの前に前回の軌跡を切る
                go.transform.position = spawnPos;

                // 飛び方は直線的（重力を考慮しない実装）
                mover.Velocity = outward * Random.Range(0.8f, 1.2f);
                mover.Drag = 6.0f;
                mover.Lifetime = SparkLifetime;
                mover.Restart();

                go.SetActive(true);

                // 前の位置から線が伸びないように、有効化してから消す
                var trail = go.GetComponent<TrailRenderer>();
                if (trail != null) trail.Clear();
            }
        }

        /// <summary>
        /// 粒ごとの色。レンジは赤(1.0,0.15,0.15)〜オレンジ(1.0,0.6,0.0)。
        /// もっと控えめにするには彩度を下げる or αを下げる。
        /// プール作成時に決めて焼いておく（以前は 1 粒ごとに Gradient を作っていたので、
        /// 200 個設定だと 1 ヒットで数百のオブジェクトが生まれていた）。
        /// </summary>
        private static Gradient MakeGradient()
        {
            Color start = Color.Lerp(new Color(1.0f, 0.15f, 0.15f), new Color(1.0f, 0.6f, 0.0f), Random.Range(0f, 1f));
            Color end = new Color(start.r, start.g, start.b, 0.0f);

            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(start, 0.0f), new GradientColorKey(end, 1.0f) },
                new[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) }
            );
            return gradient;
        }

        private static void EnsurePool(int size)
        {
            // List は Unity の破棄判定を通らないので、中身が生きているかまで見る
            if (_pool.Count > 0 && _pool[0] == null)
            {
                _pool.Clear();
                _next = 0;
            }

            if (_root == null)
            {
                _root = new GameObject("WallHitSound_Sparks");
                Object.DontDestroyOnLoad(_root);
            }

            var material = EffectMaterial.Trail;

            while (_pool.Count < size)
            {
                var spark = new GameObject("WallHitSpark");
                spark.transform.SetParent(_root.transform, false);

                var trail = spark.AddComponent<TrailRenderer>();
                trail.time = TrailTime;
                trail.startWidth = TrailWidth;
                trail.endWidth = 0.0f;
                // 頂点間距離（小さいほど滑らか）: 0.0012f〜0.004f
                trail.minVertexDistance = 0.0018f;
                trail.numCornerVertices = 2;
                trail.numCapVertices = 2;
                trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                trail.receiveShadows = false;
                trail.colorGradient = MakeGradient();
                if (material != null) trail.sharedMaterial = material;

                var mover = spark.AddComponent<TransientMover>();
                spark.SetActive(false);
                _pool.Add(mover);
            }
        }
    }
}
