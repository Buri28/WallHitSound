using UnityEngine;

namespace WallHitSound.Services
{
    /// <summary>
    /// WAV ファイルバイト列から AudioClip を生成するユーティリティクラス。
    /// </summary>
    public static class WavUtility
    {
        /// <summary>
        /// WAV ファイルバイト列を AudioClip に変換する。
        /// </summary>
        public static AudioClip ToAudioClip(byte[] wavFileBytes)
        {
            if (wavFileBytes == null || wavFileBytes.Length < 44)
                return null;

            // WAV ヘッダーを解析
            int sampleRate = System.BitConverter.ToInt32(wavFileBytes, 24);
            int channels = System.BitConverter.ToInt16(wavFileBytes, 8);
            int subChunk2Size = System.BitConverter.ToInt32(wavFileBytes, 40);

            // オーディオデータを抽出
            float[] audioData = new float[subChunk2Size / 2]; // 16-bit オーディオ
            int index = 44; // WAV ヘッダー後から開始

            for (int i = 0; i < audioData.Length; i++)
            {
                short sample = System.BitConverter.ToInt16(wavFileBytes, index);
                audioData[i] = sample / 32768f; // [-1, 1] 範囲に正規化
                index += 2;
            }

            // AudioClip を生成
            AudioClip clip = AudioClip.Create("custom_audio", audioData.Length, channels, sampleRate, false);
            clip.SetData(audioData, 0);
            return clip;
        }
    }
}
