using UnityEngine;

namespace WallHitSound.Services.Effects
{
    /// <summary>
    /// 壁に当たったときの見た目のエフェクトをまとめて扱う。
    /// 形も Mesh も GameObject もメニュー到達時・設定変更時に作り置きし、
    /// 衝突の瞬間は「作り置きから 1 つ選んで表示する」だけにしている
    /// （曲中に生成を挟むと VR のフレームレート判定に引っかかるため）。
    /// 音の <see cref="WallHitSoundService"/> と同じ方針。
    /// </summary>
    public static class HitEffectService
    {
        /// <summary>エフェクトなし。</summary>
        public const string TypeNone = "none";
        /// <summary>火花（従来の見た目）。</summary>
        public const string TypeSpark = "spark";
        /// <summary>漫画風バースト。</summary>
        public const string TypeBurst = "burst";
        /// <summary>ひび割れ。</summary>
        public const string TypeCrack = "crack";

        /// <summary>当たるたびの形の振れ幅。この数だけ作り置きして順に使う。</summary>
        private const int VariantCount = 6;
        /// <summary>同時に出せる数。連続で当たっても作り足さないための余裕。</summary>
        private const int PoolSize = 3;

        private static readonly Color32 DefaultFill = new Color32(255, 210, 30, 255);
        private static readonly Color32 DefaultEdge = new Color32(224, 27, 36, 255);
        private static readonly Color32 DefaultLine = new Color32(239, 243, 250, 255);

        private static GameObject _root;
        private static HitEffectAnimator[] _pool;
        private static int _nextSlot;

        private static EffectGeometry[] _burstVariants;
        private static EffectGeometry[] _crackVariants;

        // 作り置きしたときの色。設定で色を変えたらここと食い違うので作り直す。
        // 衝突のたびに通るので、文字列のキーではなく色そのものを比べる
        private static Color32 _builtFill, _builtEdge, _builtLine;

        // 形を作り直すたびに進める。プール側の Mesh の組み直しはこれを見て判断する
        private static int _variantVersion;
        private static int _preparedVersion = -1;

        private static uint _spawnCount;

        /// <summary>
        /// 形・Mesh・プールを事前に用意する。メニュー到達時と設定変更時に呼ぶ。
        /// 2 回目以降は色が変わっていなければ何もしない。
        /// </summary>
        public static void Prewarm()
        {
            try
            {
                string type = CurrentType();

                if (type == TypeSpark)
                {
                    ParticleEffectService.Prewarm(ReadInt(() => PluginConfig.Instance.ParticleCount, 0));
                    return;
                }

                // none のときは Mesh もプールも要らない
                if (type != TypeBurst && type != TypeCrack) return;

                EnsureReady();
            }
            catch (System.Exception ex)
            {
                Plugin.Log?.Error("WallHitSound: Effect prewarm failed: " + ex.Message);
            }
        }

        /// <summary>
        /// 現在の設定のエフェクトを頭の少し前に出す。設定が none なら何もしない。
        /// </summary>
        public static void Play()
        {
            try
            {
                string type = CurrentType();
                if (type == TypeNone) return;

                Vector3 position;
                if (!EffectCamera.TryGetSpawnPoint(out position)) return;

                PlayAt(type, position);
            }
            catch (System.Exception ex)
            {
                Plugin.Log?.Error("WallHitSound: Effect play failed: " + ex.Message);
            }
        }

        /// <summary>指定した種類・位置でエフェクトを出す。</summary>
        public static void PlayAt(string type, Vector3 position)
        {
            if (type == TypeSpark)
            {
                ParticleEffectService.Spawn(position, ReadInt(() => PluginConfig.Instance.ParticleCount, 0));
                return;
            }

            if (type != TypeBurst && type != TypeCrack) return;

            // 通常はここまでに Prewarm が済んでいる。設定ファイルを直接書き換えた場合など、
            // 済んでいなければここで一度だけ用意する
            EnsureReady();
            if (_pool == null) return;

            var slot = _pool[_nextSlot];
            _nextSlot = (_nextSlot + 1) % _pool.Length;
            if (slot == null || !slot.IsReady) return;

            float scale = Mathf.Clamp(ReadFloat(() => PluginConfig.Instance.EffectScale, 1f), 0.3f, 2f);
            float opacity = Mathf.Clamp01(ReadFloat(() => PluginConfig.Instance.EffectOpacity, 1f));

            // 作り置きを順に使う。UnityEngine.Random を回すとゲーム側の乱数列に
            // 影響しうるので、単純な巡回で散らす
            int variant = (int)(_spawnCount++ % VariantCount);

            slot.Play(
                variant,
                type == TypeCrack,
                position,
                scale,
                opacity,
                type == TypeBurst ? EffectShapes.BurstOvershoot : 1f);
        }

        private static string CurrentType()
        {
            string type = ReadString(() => PluginConfig.Instance.EffectType);
            return string.IsNullOrEmpty(type) ? PluginConfig.DefaultEffectType : type;
        }

        /// <summary>形・プール・Mesh をひととおり整える。すべて済んでいれば何もしない。</summary>
        private static void EnsureReady()
        {
            EnsureRoot();
            EnsureVariants();
            PreparePool();
        }

        /// <summary>常駐する入れ物とプールを作る（初回のみ）。</summary>
        private static void EnsureRoot()
        {
            // 配列は Unity の破棄判定を通らないので、中身が生きているかまで見る
            if (_root != null && _pool != null && _pool.Length > 0 && _pool[0] != null) return;

            if (EffectMaterial.Mesh == null) return;

            if (_root == null)
            {
                _root = new GameObject("WallHitSound_Effects");
                Object.DontDestroyOnLoad(_root);
            }

            _pool = new HitEffectAnimator[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                var go = new GameObject("WallHitEffect" + i);
                go.transform.SetParent(_root.transform, false);
                _pool[i] = go.AddComponent<HitEffectAnimator>();
                go.SetActive(false);
            }
            _nextSlot = 0;
            _preparedVersion = -1; // 作り直したので Mesh も組み直す
            Plugin.LogInfo("WallHitSound: Effect pool created");
        }

        /// <summary>形を作り置きする。色が変わっていたら作り直す。</summary>
        private static void EnsureVariants()
        {
            Color32 fill = ParseColor(ReadString(() => PluginConfig.Instance.BurstFillColor), DefaultFill);
            Color32 edge = ParseColor(ReadString(() => PluginConfig.Instance.BurstEdgeColor), DefaultEdge);
            Color32 line = ParseColor(ReadString(() => PluginConfig.Instance.CrackColor), DefaultLine);

            if (_burstVariants != null && _crackVariants != null &&
                Same(fill, _builtFill) && Same(edge, _builtEdge) && Same(line, _builtLine))
            {
                return;
            }

            _burstVariants = new EffectGeometry[VariantCount];
            _crackVariants = new EffectGeometry[VariantCount];
            for (int i = 0; i < VariantCount; i++)
            {
                var burstRng = new ShapeRng((uint)(0x5EED0000 + i));
                _burstVariants[i] = EffectShapes.BuildBurst(ref burstRng, fill, edge);

                var crackRng = new ShapeRng((uint)(0x0C4AC000 + i));
                _crackVariants[i] = EffectShapes.BuildCrack(ref crackRng, line);
            }

            _builtFill = fill;
            _builtEdge = edge;
            _builtLine = line;
            _variantVersion++;
            Plugin.LogInfo("WallHitSound: Built burst/crack variants");
        }

        /// <summary>プールの各スロットに全パターンの Mesh を組ませる。</summary>
        private static void PreparePool()
        {
            if (_pool == null || _burstVariants == null || _crackVariants == null) return;
            if (_preparedVersion == _variantVersion) return;

            foreach (var slot in _pool)
            {
                if (slot != null) slot.Prepare(_burstVariants, _crackVariants);
            }
            _preparedVersion = _variantVersion;
            Plugin.LogInfo("WallHitSound: Effect meshes prepared");
        }

        private static bool Same(Color32 a, Color32 b)
        {
            return a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;
        }

        /// <summary>"#RRGGBB" を色に変換する。読めなければ既定色。</summary>
        private static Color32 ParseColor(string hex, Color32 fallback)
        {
            if (string.IsNullOrEmpty(hex)) return fallback;
            if (hex[0] != '#') hex = "#" + hex;
            Color parsed;
            return ColorUtility.TryParseHtmlString(hex, out parsed) ? (Color32)parsed : fallback;
        }

        // 設定の読み出しは、生成前や再読み込み中に触られても落ちないように包んでおく
        private static int ReadInt(System.Func<int> read, int fallback)
        {
            try { return read(); } catch { return fallback; }
        }

        private static float ReadFloat(System.Func<float> read, float fallback)
        {
            try { return read(); } catch { return fallback; }
        }

        private static string ReadString(System.Func<string> read)
        {
            try { return read(); } catch { return null; }
        }
    }
}
