namespace WallHitSound.Services.Effects
{
    /// <summary>
    /// 形状生成専用の乱数（mulberry32）。
    /// UnityEngine.Random を使わないのは、こちらが状態を進めるとゲーム側の乱数列に
    /// 影響してしまうため。種を固定すれば同じ形が再現でき、試作ページ
    /// （ブラウザ側の同名アルゴリズム）と同じ形が出る。
    /// </summary>
    internal struct ShapeRng
    {
        private uint _state;

        public ShapeRng(uint seed)
        {
            _state = seed;
        }

        /// <summary>0 以上 1 未満の値を返す。</summary>
        public float Next()
        {
            unchecked
            {
                _state += 0x6D2B79F5u;
                uint a = _state;
                uint t = (a ^ (a >> 15)) * (1u | a);
                t = ((t + ((t ^ (t >> 7)) * (61u | t))) ^ t);
                return (float)((t ^ (t >> 14)) / 4294967296.0);
            }
        }

        /// <summary>min 以上 max 未満の値を返す。</summary>
        public float Range(float min, float max)
        {
            return min + (max - min) * Next();
        }

        /// <summary>-1 以上 1 未満の値を返す（ばらつき用）。</summary>
        public float Signed()
        {
            return Next() * 2f - 1f;
        }
    }
}
