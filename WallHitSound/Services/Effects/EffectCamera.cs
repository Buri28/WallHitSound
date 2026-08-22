using UnityEngine;

namespace WallHitSound.Services.Effects
{
    /// <summary>
    /// エフェクトの表示位置と向きの基準になるカメラ（＝頭の位置）。
    /// 結果は控えない。メニューとゲームプレイでカメラが入れ替わるため、控えると
    /// メニューで掴んだカメラを曲中も使い続けてしまう（Camera.main は Unity 側で
    /// キャッシュされているので、毎回引いても走査は起きない）。
    /// </summary>
    internal static class EffectCamera
    {
        /// <summary>接触点から前方へどれだけ離して出すか（メートル）。</summary>
        private const float ForwardOffset = 0.50f;
        /// <summary>目線より少し上げる量（メートル）。</summary>
        private const float UpOffset = 0.06f;

        /// <summary>現在のカメラ。見つからなければ null。</summary>
        public static Camera Current
        {
            get
            {
                var cam = Camera.main;
                if (cam != null) return cam;

                // MainCamera タグが付いていない場面のための保険。
                // 掴んだものを覚えないので、Camera.main が現れれば次からそちらに戻る
                var all = Camera.allCameras;
                return (all != null && all.Length > 0) ? all[0] : null;
            }
        }

        /// <summary>
        /// 頭の少し前をエフェクトの表示位置として返す。カメラが取れなければ false。
        /// </summary>
        public static bool TryGetSpawnPoint(out Vector3 position)
        {
            var cam = Current;
            if (cam == null)
            {
                position = Vector3.zero;
                return false;
            }

            var t = cam.transform;
            position = t.position + t.forward * ForwardOffset + Vector3.up * UpOffset;
            return true;
        }
    }
}
