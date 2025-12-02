using System;
using System.IO;
using IPA.Utilities;

namespace WallHitSound.Utilities
{
    /// <summary>
    /// Beat Saber のインストールパスと UserData フォルダを取得するヘルパークラス。
    /// </summary>
    public static class BeatSaberPathHelper
    {
        /// <summary>
        /// WallHitSound の UserData パスを取得する。
        /// パスが存在しない場合は null を返す。
        /// </summary>
        public static string GetBeatSaberUserDataPath()
        {
            try
            {
                // IPA の UnityGame API から UserData パスを取得
                string userDataPath = Path.Combine(UnityGame.UserDataPath, "WallHitSound");

                Plugin.Log?.Info($"WallHitSound: UserData path: {userDataPath}");

                return userDataPath;
            }
            catch (Exception ex)
            {
                Plugin.Log?.Error($"WallHitSound: Error determining Beat Saber UserData path: {ex.Message}\n{ex.StackTrace}");
                return null;
            }
        }
    }
}