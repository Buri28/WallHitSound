using UnityEngine;

namespace WallHitSound.Services.Effects
{
    /// <summary>
    /// エフェクトが共有するマテリアル。
    /// 以前は火花 1 個ごとに Shader.Find と new Material をしていたが、マテリアルは
    /// GameObject と一緒には破棄されないため、衝突のたびに設定した数だけ積み上がっていた。
    /// アプリ全体で 1 枚ずつだけ作って使い回す。
    /// </summary>
    internal static class EffectMaterial
    {
        // メッシュ（バースト・ひび割れ）用。濃さは頂点カラーのアルファで表すので、
        // 頂点カラーを反映しないシェーダは候補に入れない
        // （Unlit/Transparent は頂点カラーを見ないため、落ちると不透明な白い塊になる）。
        private static readonly string[] MeshShaders = { "Sprites/Default", "UI/Default" };

        // 火花用。変更前と同じ探索順・同じ描画順にして、見た目を変えないでおく。
        private static readonly string[] TrailShaders = { "Hidden/Internal-Colored", "Sprites/Default" };

        private static Material _mesh;
        private static Material _trail;
        private static bool _meshFailed;

        /// <summary>バースト・ひび割れ用。見つからなければ null（呼び出し側で描画をあきらめる）。</summary>
        public static Material Mesh
        {
            get
            {
                if (_mesh != null) return _mesh;
                if (_meshFailed) return null;

                _mesh = Create(MeshShaders, "mesh");
                if (_mesh == null)
                {
                    // 毎回 Shader.Find を走らせないように、あきらめたことを覚えておく
                    _meshFailed = true;
                    return null;
                }
                // 壁や譜面より後に描いて隠れにくくする
                _mesh.renderQueue = 3600;
                return _mesh;
            }
        }

        /// <summary>火花用。renderQueue はシェーダ既定のまま（変更前と同じ）。</summary>
        public static Material Trail
        {
            get
            {
                if (_trail != null) return _trail;
                _trail = Create(TrailShaders, "trail");
                return _trail;
            }
        }

        private static Material Create(string[] candidates, string usage)
        {
            Shader shader = null;
            foreach (var name in candidates)
            {
                shader = Shader.Find(name);
                if (shader != null)
                {
                    Plugin.LogInfo($"WallHitSound: Effect shader for {usage}: '{name}'");
                    break;
                }
            }

            if (shader == null)
            {
                // ここに来ると描画できないが、音は鳴るので致命ではない
                Plugin.LogWarn($"WallHitSound: No usable shader found for {usage} effects");
                return null;
            }

            return new Material(shader)
            {
                // シーン遷移で消えないようにする（アプリ全体で常駐させるため）
                hideFlags = HideFlags.HideAndDontSave,
            };
        }
    }
}
