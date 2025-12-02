using UnityEngine;

namespace WallHitSound.Services
{
    /// <summary>
    /// デフォルトのサウンド（壁打撃音）を生成するジェネレータークラス。
    /// </summary>
    public static class DefaultSoundGenerator
    {
        /// <summary>
        /// 壁に当たったときのイメージにふさわしい効果音を生成する。
        /// 複数の周波数成分を含むクリックノイズ的な音。
        /// </summary>
        public static AudioClip GenerateWallHitSound()
        {
            int sampleRate = 44100;
            float duration = 0.15f; // 150ms の短い効果音
            int sampleCount = (int)(sampleRate * duration);

            float[] samples = new float[sampleCount];

            // 複数周波数成分を合成して壁打撃音を生成
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;

                // 周波数成分1（低域、900Hz）- パンチ感を出す
                float freq1 = 900f;
                float wave1 = Mathf.Sin(2f * Mathf.PI * freq1 * t);

                // 周波数成分2（中域、2200Hz）- カリカリ感を出す
                float freq2 = 2200f;
                float wave2 = Mathf.Sin(2f * Mathf.PI * freq2 * t) * 0.6f;

                // 周波数成分3（高域、4500Hz）- 鋭さを出す
                float freq3 = 4500f;
                float wave3 = Mathf.Sin(2f * Mathf.PI * freq3 * t) * 0.3f;

                // 合成波
                float combinedWave = (wave1 + wave2 + wave3) / 2f;

                // エンベロープ（立ち上がりは素早く、減衰は自然に）
                float envelope = Mathf.Exp(-5f * t); // 指数関数的な減衰

                samples[i] = combinedWave * envelope * 0.6f; // 0.6 で正規化
            }

            AudioClip clip = AudioClip.Create("wall_hit", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// より複雑な打撃音（インパクト + リング）を生成する。
        /// </summary>
        public static AudioClip GenerateImpactSound()
        {
            int sampleRate = 44100;
            float duration = 0.2f;
            int sampleCount = (int)(sampleRate * duration);

            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;

                // インパクト部分（最初の50ms）
                if (t < 0.05f)
                {
                    // ホワイトノイズ的な質感
                    float noise = (Random.value * 2f - 1f) * Mathf.Exp(-20f * t);

                    // 低周波キック
                    float kick = Mathf.Sin(2f * Mathf.PI * 600f * t) * Mathf.Exp(-15f * t);

                    samples[i] = (noise * 0.5f + kick * 0.5f) * 0.7f;
                }
                else
                {
                    // リング部分（残響）
                    float ringFreq = 1200f;
                    float ring = Mathf.Sin(2f * Mathf.PI * ringFreq * (t - 0.05f));
                    samples[i] = ring * Mathf.Exp(-4f * (t - 0.05f)) * 0.3f;
                }
            }

            AudioClip clip = AudioClip.Create("wall_impact", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// 低周波の深い打撃音を生成する（ドン！という感じ）。
        /// </summary>
        public static AudioClip GenerateDeepImpactSound()
        {
            int sampleRate = 44100;
            float duration = 0.25f;
            int sampleCount = (int)(sampleRate * duration);

            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;

                // 第1段階：深い低周波キック（200Hz）- ドンの音源
                float deepKick = Mathf.Sin(2f * Mathf.PI * 200f * t) * Mathf.Exp(-6f * t);

                // 第2段階：中周波アタック（400Hz）- パワーを加える
                float attack = Mathf.Sin(2f * Mathf.PI * 400f * t) * Mathf.Exp(-8f * t) * 0.5f;

                // 第3段階：高周波クリック（1500Hz）- 立ち上がりの鋭さ
                float click = Mathf.Sin(2f * Mathf.PI * 1500f * t) * Mathf.Exp(-15f * t) * 0.2f;

                // 合成波
                float combined = deepKick + attack + click;

                // マスターエンベロープ（全体的な減衰）
                float masterEnvelope = Mathf.Exp(-3f * t);

                samples[i] = combined * masterEnvelope * 0.6f;
            }

            AudioClip clip = AudioClip.Create("deep_impact", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
