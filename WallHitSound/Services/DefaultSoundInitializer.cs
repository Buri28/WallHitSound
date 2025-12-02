using System;
using System.IO;
using UnityEngine;

namespace WallHitSound.Services
{
    /// <summary>
    /// デフォルトのサウンドファイルを初期化するクラス。
    /// 初回起動時に UserData フォルダにデフォルト音声をコピーする。
    /// </summary>
    public static class DefaultSoundInitializer
    {
        /// <summary>
        /// UserData フォルダにデフォルトサウンドをセットアップする。
        /// </summary>
        public static void InitializeDefaultSounds(string userDataPath)
        {
            try
            {
                if (string.IsNullOrEmpty(userDataPath) || !Directory.Exists(userDataPath))
                {
                    Plugin.Log?.Warn("WallHitSound: UserData path is invalid for default sound setup");
                    return;
                }

                // デフォルトサウンドを複数生成
                CopyDefaultSoundIfNeeded(userDataPath, "wall_hit", DefaultSoundGenerator.GenerateWallHitSound);
                CopyDefaultSoundIfNeeded(userDataPath, "deep_impact", DefaultSoundGenerator.GenerateDeepImpactSound);

                Plugin.Log?.Info("WallHitSound: Default sound files initialized");
            }
            catch (Exception ex)
            {
                Plugin.Log?.Error($"WallHitSound: Error initializing default sounds: {ex.Message}");
            }
        }

        /// <summary>
        /// 指定されたサウンドファイルが UserData になければ、
        /// 生成またはリソースからコピーする。
        /// </summary>
        private static void CopyDefaultSoundIfNeeded(string userDataPath, string soundName, System.Func<AudioClip> generator)
        {
            string targetPath = Path.Combine(userDataPath, $"{soundName}.wav");

            // ファイルが既に存在する場合はスキップ
            if (File.Exists(targetPath))
            {
                Plugin.Log?.Debug($"WallHitSound: Default sound already exists at {targetPath}");
                return;
            }

            try
            {
                // デフォルト音を生成
                AudioClip defaultClip = generator();

                if (defaultClip != null)
                {
                    byte[] wavData = AudioClipToWav(defaultClip);
                    File.WriteAllBytes(targetPath, wavData);
                    Plugin.Log?.Info($"WallHitSound: Default sound generated and saved to {targetPath}");
                }
                else
                {
                    Plugin.Log?.Warn("WallHitSound: Failed to generate default sound");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.Warn($"WallHitSound: Failed to create default sound: {ex.Message}");
            }
        }

        /// <summary>
        /// AudioClip を WAV バイト配列に変換する。
        /// </summary>
        private static byte[] AudioClipToWav(AudioClip clip)
        {
            // シンプルな WAV エンコード（16-bit PCM）
            float[] samples = new float[clip.samples * clip.channels];
            clip.GetData(samples, 0);

            int sampleRate = clip.frequency;
            int channels = clip.channels;
            int numSamples = samples.Length;

            // WAV ヘッダーを構築
            byte[] wavHeader = new byte[44];
            int subChunk2Size = numSamples * 2; // 16-bit = 2 bytes per sample

            // "RIFF" チャンク
            System.Text.Encoding.ASCII.GetBytes("RIFF").CopyTo(wavHeader, 0);
            System.BitConverter.GetBytes(36 + subChunk2Size).CopyTo(wavHeader, 4);
            System.Text.Encoding.ASCII.GetBytes("WAVE").CopyTo(wavHeader, 8);

            // "fmt " サブチャンク
            System.Text.Encoding.ASCII.GetBytes("fmt ").CopyTo(wavHeader, 12);
            System.BitConverter.GetBytes(16).CopyTo(wavHeader, 16); // Subchunk1Size
            System.BitConverter.GetBytes((short)1).CopyTo(wavHeader, 20); // AudioFormat (PCM)
            System.BitConverter.GetBytes((short)channels).CopyTo(wavHeader, 22);
            System.BitConverter.GetBytes(sampleRate).CopyTo(wavHeader, 24);
            System.BitConverter.GetBytes(sampleRate * channels * 2).CopyTo(wavHeader, 28); // ByteRate
            System.BitConverter.GetBytes((short)(channels * 2)).CopyTo(wavHeader, 32); // BlockAlign
            System.BitConverter.GetBytes((short)16).CopyTo(wavHeader, 34); // BitsPerSample

            // "data" チャンク
            System.Text.Encoding.ASCII.GetBytes("data").CopyTo(wavHeader, 36);
            System.BitConverter.GetBytes(subChunk2Size).CopyTo(wavHeader, 40);

            // オーディオデータを 16-bit PCM に変換
            byte[] audioData = new byte[subChunk2Size];
            int audioIndex = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                short sample = (short)(Mathf.Clamp01(samples[i]) * 32767f);
                System.BitConverter.GetBytes(sample).CopyTo(audioData, audioIndex);
                audioIndex += 2;
            }

            // ヘッダーとデータを結合
            byte[] wavFile = new byte[wavHeader.Length + audioData.Length];
            System.Buffer.BlockCopy(wavHeader, 0, wavFile, 0, wavHeader.Length);
            System.Buffer.BlockCopy(audioData, 0, wavFile, wavHeader.Length, audioData.Length);

            return wavFile;
        }
    }
}
