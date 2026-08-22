using System.Collections.Generic;
using UnityEngine;

namespace WallHitSound.Services.Effects
{
    /// <summary>
    /// バーストとひび割れの形を作る。数値は試作ページで決めたものをそのまま持ってきている。
    /// 生成はメニュー到達時だけで、曲中はここを通らない。
    /// </summary>
    internal static class EffectShapes
    {
        // ─── 漫画風バースト ───────────────────────────────
        /// <summary>とがりの数。</summary>
        public const int BurstSpikes = 11;
        /// <summary>外径のばらつき（0 で全部同じ長さ）。</summary>
        public const float BurstOuterJitter = 0.22f;
        /// <summary>くびれの深さ（外径に対する内側頂点の比）。</summary>
        public const float BurstInnerRatio = 0.44f;
        /// <summary>とがりの角度のばらつき。</summary>
        public const float BurstAngleJitter = 0.30f;
        /// <summary>縁の太さ（外側へ相似拡大する割合）。とがりの先ほど太く見える。</summary>
        public const float BurstEdgeWidth = 0.33f;
        /// <summary>
        /// 出現時の飛び出し量。イージングの係数に (値-1)*3.2 として渡すので、1.18 での
        /// 実際の飛び出しは約 1%。試作ページと同じ式・同じ数値にして見た目を揃えてある。
        /// はっきり弾ませたい場合は HitEffectAnimator.EaseOutBack の係数 3.2 を上げる。
        /// </summary>
        public const float BurstOvershoot = 1.18f;
        /// <summary>半径（メートル）。</summary>
        public const float BurstRadius = 0.32f;

        // ─── ひび割れ ─────────────────────────────────────
        /// <summary>中心から放射するひびの本数。</summary>
        public const int CrackMainCracks = 12;
        /// <summary>1 本あたりの折れの数。</summary>
        public const int CrackSegments = 3;
        /// <summary>折れるたびの角度の振れ幅（ラジアン）。</summary>
        public const float CrackWobble = 0.25f;
        /// <summary>枝分かれの発生率。</summary>
        public const float CrackBranchRate = 0.58f;
        /// <summary>隣どうしをつなぐ横線の本数。割れたガラスらしさはここで出る。</summary>
        public const int CrackCrossLinks = 2;
        /// <summary>線の太さ（メートル）。</summary>
        public const float CrackLineWidth = 0.008f;
        /// <summary>ひびの長さのばらつき。</summary>
        public const float CrackLengthJitter = 0.35f;
        /// <summary>半径（メートル）。</summary>
        public const float CrackRadius = 0.40f;

        /// <summary>
        /// 星型ポリゴンのバーストを作る。
        /// 縁は本体を相似拡大した形との間を四角形で埋めた輪っかなので、本体と重ならない
        /// （重ねると消えぎわに色が濁るため）。
        /// </summary>
        public static EffectGeometry BuildBurst(ref ShapeRng rng, Color32 fill, Color32 edge)
        {
            int count = BurstSpikes * 2;
            var pts = new Vector2[count];
            float half = Mathf.PI / BurstSpikes;

            for (int i = 0; i < count; i++)
            {
                bool outer = (i % 2) == 0;
                float a = i * half + rng.Signed() * half * BurstAngleJitter;
                float r = outer
                    ? 1f - rng.Next() * BurstOuterJitter
                    : BurstInnerRatio * (1f - rng.Next() * 0.35f);
                pts[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (r * BurstRadius);
            }

            var b = new GeometryBuilder(BurstRadius);
            float outward = 1f + BurstEdgeWidth;

            // 縁（輪っか）を先に積む
            for (int i = 0; i < count; i++)
            {
                int j = (i + 1) % count;
                b.AddQuad(pts[i] * outward, pts[j] * outward, pts[j], pts[i], edge);
            }

            // 本体（中心からの三角形ファン）
            for (int i = 0; i < count; i++)
            {
                int j = (i + 1) % count;
                b.AddTriangle(Vector2.zero, pts[i], pts[j], fill);
            }

            return b.Build();
        }

        /// <summary>
        /// 放射状のひび割れを作る。中心から伸びる折れ線に枝と横つなぎを足し、
        /// 中心側が太く外へ行くほど細くなるようにしている。
        /// </summary>
        public static EffectGeometry BuildCrack(ref ShapeRng rng, Color32 color)
        {
            var strokes = new List<Vector2[]>();
            var widths = new List<float>();
            var mains = new List<Vector2[]>();
            float stepA = Mathf.PI * 2f / CrackMainCracks;

            for (int i = 0; i < CrackMainCracks; i++)
            {
                float a = i * stepA + rng.Signed() * stepA * 0.35f;
                float total = 1f - rng.Next() * CrackLengthJitter;
                float d = total / CrackSegments;

                var pts = new Vector2[CrackSegments + 1];
                Vector2 cur = Vector2.zero;
                pts[0] = cur;

                for (int s = 0; s < CrackSegments; s++)
                {
                    a += rng.Signed() * CrackWobble;
                    cur += new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d;
                    pts[s + 1] = cur;

                    // 枝分かれ。根元と先端では出さず、途中の節からだけ生やす
                    if (s >= 1 && rng.Next() < CrackBranchRate)
                    {
                        float side = rng.Next() < 0.5f ? -1f : 1f;
                        float ba = a + side * (0.4f + rng.Next() * 0.6f);
                        float bl = d * (0.5f + rng.Next() * 0.8f);
                        strokes.Add(new[] { cur, cur + new Vector2(Mathf.Cos(ba), Mathf.Sin(ba)) * bl });
                        widths.Add(0.55f);
                    }
                }

                mains.Add(pts);
                strokes.Add(pts);
                widths.Add(1f);
            }

            // 横つなぎ
            for (int k = 0; k < CrackCrossLinks; k++)
            {
                float f = 0.34f + k * 0.24f;
                for (int i = 0; i < mains.Count; i++)
                {
                    if (rng.Next() < 0.3f) continue;
                    Vector2 from = Along(mains[i], f);
                    Vector2 to = Along(mains[(i + 1) % mains.Count], f + (rng.Next() * 0.12f - 0.06f));
                    strokes.Add(new[] { from, to });
                    widths.Add(0.45f);
                }
            }

            var b = new GeometryBuilder(CrackRadius);
            for (int n = 0; n < strokes.Count; n++)
            {
                Vector2[] pts = strokes[n];
                float w = widths[n];
                for (int i = 0; i < pts.Length - 1; i++)
                {
                    b.AddTaperedSegment(
                        pts[i] * CrackRadius,
                        pts[i + 1] * CrackRadius,
                        TaperedWidth(pts[i], w),
                        TaperedWidth(pts[i + 1], w),
                        color);
                }
            }

            return b.Build();
        }

        /// <summary>中心から遠いほど細くする。</summary>
        private static float TaperedWidth(Vector2 normalizedPoint, float strokeWidth)
        {
            float dist = Mathf.Min(1f, normalizedPoint.magnitude);
            return CrackLineWidth * strokeWidth * (1f - dist * 0.55f);
        }

        /// <summary>折れ線上の割合 f （0=中心、1=先端）の座標を返す。</summary>
        private static Vector2 Along(Vector2[] pts, float f)
        {
            f = Mathf.Clamp01(f);
            float t = f * (pts.Length - 1);
            int i = Mathf.Clamp(Mathf.FloorToInt(t), 0, pts.Length - 2);
            return Vector2.Lerp(pts[i], pts[i + 1], t - i);
        }
    }
}
