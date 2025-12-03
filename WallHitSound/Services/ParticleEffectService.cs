using UnityEngine;

namespace WallHitSound.Services
{
    public static class ParticleEffectService
    {
        // 半球状に赤〜オレンジの火花（スパーク）を生成する簡易実装
        public static void SpawnHemisphere(Vector3 position, int count, Color fallbackColor)
        {
            if (count <= 0) return;

            // 半球の向きをランダムに決定
            // 調整案: 衝突面の法線が取れる場合はその方向に置換すると自然（例: raycastHit.normal）
            Vector3 hemiNormal = Random.onUnitSphere;
            if (hemiNormal.sqrMagnitude < 1e-4f) hemiNormal = Vector3.up;
            hemiNormal.Normalize();

            // 生成半径（接触点近傍）
            // 調整目安: 0.003f（極小）〜 0.02f（やや広め）
            float radius = 0.005f;
            for (int i = 0; i < count; i++)
            {
                // 半球内ランダム方向
                Vector3 randomDir = Random.onUnitSphere;
                if (Vector3.Dot(randomDir, hemiNormal) < 0f) randomDir = -randomDir;

                Vector3 spawnPos = position + randomDir * Random.Range(0.0f, radius);
                // 起点から少し離して放出（接触点から浮かせる）
                float startOffset = 0.03f; // 0.01〜0.03 で調整可

                var spark = new GameObject("WallHitSpark");
                spark.transform.position = spawnPos;

                var trail = spark.AddComponent<TrailRenderer>();
                // 表示時間（短いほど控えめ）: 0.03f〜0.08f 推奨
                trail.time = 0.06f; // 少し長めにして視認性を向上
                // 線幅（細いほど控えめ）: 0.0003f〜0.003f 推奨
                trail.startWidth = 0.0012f; // 奥に表示する分、線幅を太くして視認性を確保
                trail.endWidth = 0.0f;
                // 頂点間距離（小さいほど滑らか）: 0.0012f〜0.004f
                trail.minVertexDistance = 0.0018f;
                trail.numCornerVertices = 2;
                trail.numCapVertices = 2;

                // 色の調整
                // ・レンジ: 赤(1.0,0.15,0.15)〜オレンジ(1.0,0.6,0.0)
                // ・もっと控えめにするには彩度を下げる or αを下げる
                Color cStart = Color.Lerp(new Color(1.0f, 0.15f, 0.15f), new Color(1.0f, 0.6f, 0.0f), Random.Range(0f, 1f));
                Color cEnd = new Color(cStart.r, cStart.g, cStart.b, 0.0f);
                Gradient gradient = new Gradient();
                gradient.SetKeys(
                    new GradientColorKey[] { new GradientColorKey(cStart, 0.0f), new GradientColorKey(cEnd, 1.0f) },
                    new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) }
                );
                trail.colorGradient = gradient;

                // シェーダのフォールバック
                var shader = Shader.Find("Hidden/Internal-Colored");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    var mat = new Material(shader);
                    trail.material = mat;
                }

                var mover = spark.AddComponent<TransientMover>();
                // 円状（半球）に放出：randomDir を主体にし、法線寄与は控えめ（形状を円状に維持）
                Vector3 outward = (randomDir * 0.8f + hemiNormal * 0.2f).normalized;
                // 発生位置を起点から少し離した位置へ移動
                spawnPos += outward * startOffset;

                // 飛び方は直線的（重力を考慮しない実装）
                mover.Velocity = outward * Random.Range(0.8f, 1.2f);
                mover.Drag = 6.0f;
                mover.Lifetime = trail.time + 0.015f;
            }
        }
    }
}
