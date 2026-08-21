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

        // ─── 低音系の「鈍い叩く音」3種 ─────────────────────────────────────
        // 正弦波を重ねただけでは、どれだけ低くしても電子音にしかならない。
        // 実際の打撃音は「当たった瞬間の破裂（ノイズ）」と「叩かれた物が鳴る成分（共鳴）」でできている。
        // ここでは次の3つを組み合わせて作る:
        //   1. すぐ消えるノイズ … 当たった瞬間の質感
        //   2. 減衰する共鳴を複数 … 物の鳴り。周波数を整数倍にしないことで音程感を薄くする
        //   3. 軽い歪み … 山だけが突出しないようにして、体感的な音量を稼ぐ
        //   4. ローパスフィルタ … 高い成分を削って「鈍い」音にする

        /// <summary>
        /// 壁を叩いたような「ドン」。ノイズを共鳴させて作る、音程感の無い打撃音。
        /// </summary>
        public static AudioClip GenerateThudSound()
        {
            return CreateImpact("thud", duration: 0.43f,
                noiseAmount: 4.5f, noiseDecay: 9.1f, resonanceFreq: 120f, resonanceQ: 2.0f,
                clickAmount: 0.65f, clickDecay: 42f, cutoff: 1100f, drive: 10.0f, seed: 4002);
        }

        /// <summary>
        /// thud と同じ音色で、余韻を長めに残した版。
        /// 違いはノイズの減衰速度だけ（9.1 → 6.2）。
        /// </summary>
        public static AudioClip GenerateThudLongSound()
        {
            return CreateImpact("thud_long", duration: 0.50f,
                noiseAmount: 4.5f, noiseDecay: 6.2f, resonanceFreq: 120f, resonanceQ: 2.0f,
                clickAmount: 0.65f, clickDecay: 42f, cutoff: 1100f, drive: 10.0f, seed: 4002);
        }

        /// <summary>
        /// 太鼓のように響く「ドーン」。低く、長く尾を引く。
        /// </summary>
        public static AudioClip GenerateBoomSound()
        {
            // duration は「音が実際に鳴っている長さ」そのもの。
            // 終盤は CalcFadeOutSamples が全体の3割（150ms）をかけて滑らかに落とすので、
            // 聞こえない空白は末尾40ms程度に収まる。
            // 尾を長く保ちたい場合は duration ではなく減衰係数(第2要素)を下げること。
            return CreateKnock("boom", duration: 0.50f,
                modes: new[,] { { 60f, 3f, 1.0f }, { 92f, 4.2f, 0.45f }, { 145f, 6.5f, 0.20f } },
                noiseAmount: 0.40f, noiseDecay: 35f, cutoff: 300f, drive: 3.5f, seed: 1002);
        }

        /// <summary>
        /// ノイズを共鳴させて作る打撃音。
        /// 正弦波を重ねる方式（CreateKnock）はどうしても音程が立って電子音になるため、
        /// ノイズを 2次の共鳴ローパスに通して「ドン」の芯を作り、
        /// フィルタを通さない生のノイズを立ち上がりに重ねて「当たった感」を出す。
        /// </summary>
        /// <param name="name">クリップ名</param>
        /// <param name="duration">長さ（秒）</param>
        /// <param name="noiseAmount">共鳴に入れるノイズの量。多いほど歪んで密度が上がる</param>
        /// <param name="noiseDecay">ノイズの減衰の速さ。ここが余韻の長さを決める</param>
        /// <param name="resonanceFreq">共鳴の中心周波数（Hz）。体感的な音の高さ</param>
        /// <param name="resonanceQ">共鳴の尖り具合。高いほど音程感が出て「ボン」に寄る</param>
        /// <param name="clickAmount">立ち上がりに重ねる生ノイズの量</param>
        /// <param name="clickDecay">その減衰の速さ</param>
        /// <param name="cutoff">仕上げのローパス（Hz）。低いほど鈍いが、体感音量は下がる</param>
        /// <param name="drive">歪みの強さ。山を潰して体感音量を稼ぐ</param>
        /// <param name="seed">ノイズの種。固定しておくと毎回同じ音が生成される</param>
        private static AudioClip CreateImpact(string name, float duration,
            float noiseAmount, float noiseDecay, float resonanceFreq, float resonanceQ,
            float clickAmount, float clickDecay, float cutoff, float drive, int seed)
        {
            const int sampleRate = 44100;
            int sampleCount = (int)(sampleRate * duration);
            float[] samples = new float[sampleCount];

            var random = new System.Random(seed);

            // 2次の共鳴ローパス（ステートバリアブル型）。
            // ノイズをそのまま鳴らすと「シャー」だが、低い周波数で共鳴させると「ドン」になる
            float svfF = 2f * Mathf.Sin(Mathf.PI * resonanceFreq / sampleRate);
            float svfQ = 1f / resonanceQ;
            float svfLow = 0f;
            float svfBand = 0f;

            float driveScale = drive > 1f ? (float)System.Math.Tanh(drive) : 1f;

            float lowpassCoef = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / sampleRate);
            float lowpassState = 0f;

            // 歪みで潰れた減衰を戻す。前半はフルのまま保つ
            float holdSeconds = duration * 0.5f;
            float masterDecay = CalcMasterDecay(duration, holdSeconds);

            int fadeInSamples = (int)(sampleRate * 0.001f);
            int fadeOutSamples = CalcFadeOutSamples(sampleCount, duration, resonanceFreq);

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float white = (float)random.NextDouble() * 2f - 1f;

                // 1. 共鳴に通すノイズ（打撃音の芯）
                float excitation = white * Mathf.Exp(-noiseDecay * t) * noiseAmount;
                float svfHigh = excitation - svfLow - svfQ * svfBand;
                svfBand += svfF * svfHigh;
                svfLow += svfF * svfBand;
                float value = svfLow;

                // 2. 立ち上がりの生ノイズ（当たった瞬間の質感）
                value += white * Mathf.Exp(-clickDecay * t) * clickAmount;

                // 3. 歪みで山を潰して体感音量を稼ぐ
                if (drive > 1f)
                {
                    value = (float)System.Math.Tanh(drive * value) / driveScale;
                }

                // 4. 歪みで失われた減衰を戻す
                value *= MasterEnvelope(t, holdSeconds, masterDecay);

                // 5. 仕上げのローパス（鈍さ）
                lowpassState += lowpassCoef * (value - lowpassState);
                value = lowpassState;

                if (i < fadeInSamples) value *= (float)i / fadeInSamples;
                int remaining = sampleCount - 1 - i;
                if (remaining < fadeOutSamples) value *= FadeOutCurve(remaining, fadeOutSamples);

                samples[i] = value;
            }

            float peak = 0f;
            for (int i = 0; i < sampleCount; i++) peak = Mathf.Max(peak, Mathf.Abs(samples[i]));
            if (peak > 0f)
            {
                float scale = 0.95f / peak;
                for (int i = 0; i < sampleCount; i++) samples[i] *= scale;
            }

            if (sampleCount > 0) samples[sampleCount - 1] = 0f;

            AudioClip clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// 歪みの後にかける全体の減衰（1サンプルぶん）。
        ///
        /// 歪み(tanh)は波形の山を潰すので、そのままでは本来の減衰が失われ、
        /// 終盤までフルボリュームが続いて最後だけ急に消える。これが「プチッ」の正体で、
        /// 実測では boom が 380ms 時点で -6dB のまま、そこから 100ms で -39dB まで落ちていた。
        ///
        /// 前半（hold）はフルのまま保って体感音量を稼ぎ、後半だけ指数的に落とす。
        /// </summary>
        /// <param name="t">経過秒</param>
        /// <param name="holdSeconds">フルのまま保つ秒数</param>
        /// <param name="decayRate">hold 後の減衰の速さ</param>
        private static float MasterEnvelope(float t, float holdSeconds, float decayRate)
        {
            return t <= holdSeconds ? 1f : Mathf.Exp(-decayRate * (t - holdSeconds));
        }

        /// <summary>
        /// MasterEnvelope の減衰係数を求める。終端でちょうど -35dB になるようにする。
        /// </summary>
        private static float CalcMasterDecay(float duration, float holdSeconds)
        {
            // 35dB / (20/ln10) = 減衰にかける秒数ぶんの係数
            return 35f / 8.6859f / Mathf.Max(0.001f, duration - holdSeconds);
        }

        /// <summary>
        /// 終端フェードの長さを決める。
        ///
        /// 歪み（tanh）を通すと波形の山が潰れ、本来の減衰が失われて
        /// 終盤でもそれなりの音量が残る。そこを短いフェードで切ると、
        /// まだ聞こえている音がいきなり消えて「プチッ」と鳴る（実測で -12dB から 50ms で断ち切っていた）。
        ///
        /// 音量を落とす仕事は MasterEnvelope が受け持つので、ここは
        /// 波形を 0 で終わらせるための最小限でよい。
        /// 最低周波数の3周期分は確保する（低音は1周期が長く、短いと波形の途中で切れるため）。
        /// </summary>
        private static int CalcFadeOutSamples(int sampleCount, float duration, float lowestHz)
        {
            const int sampleRate = 44100;
            float seconds = Mathf.Max(3f / lowestHz, duration * 0.08f);
            return Mathf.Min((int)(sampleRate * seconds), (int)(sampleCount * 0.9f));
        }

        /// <summary>
        /// 終端フェードの曲線。直線だとフェードの開始点で音量変化が急に折れ、
        /// それ自体がクリックとして聞こえるため、両端の傾きが 0 になる曲線を使う。
        /// </summary>
        /// <param name="remaining">終端までの残りサンプル数</param>
        /// <param name="fadeOutSamples">フェード全体のサンプル数</param>
        private static float FadeOutCurve(int remaining, int fadeOutSamples)
        {
            float x = (float)remaining / fadeOutSamples;
            return 0.5f - 0.5f * Mathf.Cos(Mathf.PI * x);
        }

        /// <summary>
        /// 減衰する共鳴を重ねて打撃音を作る（boom 用）。
        /// 音程感が残るので、余韻の長い響き系に向く。
        /// 音程感を消したい打撃音には CreateImpact を使うこと。
        /// </summary>
        /// <param name="name">クリップ名</param>
        /// <param name="duration">長さ（秒）</param>
        /// <param name="modes">共鳴成分。1行につき { 周波数(Hz), 減衰の速さ, 音量 }。
        /// 周波数を整数倍にしないほど、音程感の無い「打撃音」らしくなる</param>
        /// <param name="noiseAmount">当たった瞬間のノイズの強さ</param>
        /// <param name="noiseDecay">ノイズの減衰の速さ（大きいほど一瞬で消える）</param>
        /// <param name="cutoff">ローパスフィルタの遮断周波数（Hz）。低いほど鈍い音になる</param>
        /// <param name="drive">歪みの強さ（1で歪みなし）。山を潰して全体の音量を稼ぐ。
        /// ローパスの前にかけるので、増えた高い倍音はそのあと削られる</param>
        /// <param name="seed">ノイズの種。固定しておくと毎回同じ音が生成される</param>
        private static AudioClip CreateKnock(string name, float duration, float[,] modes,
            float noiseAmount, float noiseDecay, float cutoff, float drive, int seed)
        {
            const int sampleRate = 44100;
            int sampleCount = (int)(sampleRate * duration);
            float[] samples = new float[sampleCount];

            // ビルドや起動のたびに音が変わらないよう、乱数の種は固定する
            var random = new System.Random(seed);

            float driveScale = drive > 1f ? (float)System.Math.Tanh(drive) : 1f;

            // 一次ローパスフィルタの係数
            float lowpassCoef = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / sampleRate);
            float lowpassState = 0f;

            // 歪みで潰れた減衰を戻す。前半はフルのまま保つ
            float holdSeconds = duration * 0.5f;
            float masterDecay = CalcMasterDecay(duration, holdSeconds);

            int fadeInSamples = (int)(sampleRate * 0.001f);
            float lowestHz = modes[0, 0];
            for (int m = 1; m < modes.GetLength(0); m++)
                lowestHz = Mathf.Min(lowestHz, modes[m, 0]);
            int fadeOutSamples = CalcFadeOutSamples(sampleCount, duration, lowestHz);

            int modeCount = modes.GetLength(0);

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;

                // 1. 当たった瞬間のノイズ
                float value = ((float)random.NextDouble() * 2f - 1f)
                              * Mathf.Exp(-noiseDecay * t) * noiseAmount;

                // 2. 物が鳴る成分
                for (int m = 0; m < modeCount; m++)
                {
                    value += Mathf.Sin(2f * Mathf.PI * modes[m, 0] * t)
                             * Mathf.Exp(-modes[m, 1] * t) * modes[m, 2];
                }

                // 3. 軽く歪ませて山を潰す。
                //    ピークだけが突出した波形は、音量を上げても「小さい」と感じるため
                if (drive > 1f)
                {
                    value = (float)System.Math.Tanh(drive * value) / driveScale;
                }

                // 4. 歪みで失われた減衰を戻す
                value *= MasterEnvelope(t, holdSeconds, masterDecay);

                // 5. ローパスフィルタ
                lowpassState += lowpassCoef * (value - lowpassState);
                value = lowpassState;

                if (i < fadeInSamples) value *= (float)i / fadeInSamples;
                int remaining = sampleCount - 1 - i;
                if (remaining < fadeOutSamples) value *= FadeOutCurve(remaining, fadeOutSamples);

                samples[i] = value;
            }

            // ピークを揃える。パラメータをいじってもクリップ（音割れ）せず、
            // 音の種類を変えても音量が大きく変わらないようにするため
            float peak = 0f;
            for (int i = 0; i < sampleCount; i++) peak = Mathf.Max(peak, Mathf.Abs(samples[i]));
            if (peak > 0f)
            {
                float scale = 0.95f / peak;
                for (int i = 0; i < sampleCount; i++) samples[i] *= scale;
            }

            // 終端は必ず 0 で締める。
            // フェードを掛けても、そのあとの正規化(0.95/peak)で微小な残差が持ち上がり、
            // 最後のサンプルが 0 でないとそこが不連続点になる。
            if (sampleCount > 0) samples[sampleCount - 1] = 0f;

            AudioClip clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
