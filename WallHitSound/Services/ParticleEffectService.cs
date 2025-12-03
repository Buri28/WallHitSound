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

                var spark = new GameObject("WallHitSpark");
                spark.transform.position = spawnPos;

                var trail = spark.AddComponent<TrailRenderer>();
                // 表示時間（短いほど控えめ）: 0.03f〜0.08f 推奨
                trail.time = 0.045f;
                // 線幅（細いほど控えめ）: 0.0003f〜0.003f 推奨
                trail.startWidth = 0.0005f;
                trail.endWidth = 0.0f;
                // 頂点間距離（小さいほど滑らか）: 0.0012f〜0.004f
                trail.minVertexDistance = 0.0015f;
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
                // 散らばり具合の調整
                // ・hemiNormal への係数を上げると面法線方向に寄る（0.3〜0.8推奨）
                // ・randomDir の寄与を下げると収束して控えめに見える
                Vector3 outward = (randomDir + hemiNormal * 0.5f).normalized;
                // 初速レンジ（低いほど控えめ）: 0.4f〜1.5f
                mover.Velocity = outward * Random.Range(0.5f, 0.9f);
                // 減衰（大きいほどすぐ減速）: 6.0f〜12.0f
                mover.Drag = 10.0f;
                // 寿命は trail.time に +α（0.01f〜0.03f）
                mover.Lifetime = trail.time + 0.02f;
            }
        }
    }
}
