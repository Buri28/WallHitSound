using System.Collections.Generic;
using UnityEngine;

namespace WallHitSound.Services.Effects
{
    /// <summary>
    /// エフェクト 1 パターン分の頂点データ。座標はメートル単位で、原点が衝突点。
    /// メニュー到達時に作り置きし、衝突のたびに Mesh へ流し込んで使い回す。
    /// </summary>
    internal class EffectGeometry
    {
        public Vector3[] Vertices;
        public int[] Triangles;
        /// <summary>ベース色。アルファは常に 255 で、実際の濃さは再生中に書き換える。</summary>
        public Color32[] Colors;
        /// <summary>頂点ごとの出現の順番（0=中心、1=外周）。ひび割れが外へ広がる表現に使う。</summary>
        public float[] Reveal;
    }

    /// <summary>
    /// 三角形を積んで <see cref="EffectGeometry"/> を作るための入れ物。
    /// 生成はメニュー到達時だけなので、ここでの List の確保は曲中の負荷にならない。
    /// </summary>
    internal class GeometryBuilder
    {
        private readonly List<Vector3> _verts = new List<Vector3>();
        private readonly List<int> _tris = new List<int>();
        private readonly List<Color32> _colors = new List<Color32>();
        private readonly List<float> _reveal = new List<float>();

        /// <summary>この半径を 1 として出現の順番を測る。</summary>
        private readonly float _revealRadius;

        public GeometryBuilder(float revealRadius)
        {
            _revealRadius = revealRadius > 0f ? revealRadius : 1f;
        }

        private int AddVertex(Vector2 p, Color32 c)
        {
            _verts.Add(new Vector3(p.x, p.y, 0f));
            _colors.Add(c);
            _reveal.Add(Mathf.Clamp01(p.magnitude / _revealRadius));
            return _verts.Count - 1;
        }

        /// <summary>三角形を 1 枚積む。</summary>
        public void AddTriangle(Vector2 a, Vector2 b, Vector2 c, Color32 color)
        {
            int i0 = AddVertex(a, color);
            int i1 = AddVertex(b, color);
            int i2 = AddVertex(c, color);
            _tris.Add(i0); _tris.Add(i1); _tris.Add(i2);
        }

        /// <summary>四角形を 1 枚積む（頂点は周回順）。</summary>
        public void AddQuad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color32 color)
        {
            int i0 = AddVertex(a, color);
            int i1 = AddVertex(b, color);
            int i2 = AddVertex(c, color);
            int i3 = AddVertex(d, color);
            _tris.Add(i0); _tris.Add(i1); _tris.Add(i2);
            _tris.Add(i0); _tris.Add(i2); _tris.Add(i3);
        }

        /// <summary>
        /// 太さの変わる線分を四角形 1 枚として積む。ひび割れの先細りはこれで作る。
        /// </summary>
        public void AddTaperedSegment(Vector2 a, Vector2 b, float widthA, float widthB, Color32 color)
        {
            Vector2 dir = b - a;
            float len = dir.magnitude;
            if (len < 1e-5f) return;
            dir /= len;

            Vector2 n = new Vector2(-dir.y, dir.x);
            float ha = Mathf.Max(widthA, 1e-4f) * 0.5f;
            float hb = Mathf.Max(widthB, 1e-4f) * 0.5f;

            AddQuad(a + n * ha, b + n * hb, b - n * hb, a - n * ha, color);
        }

        public EffectGeometry Build()
        {
            return new EffectGeometry
            {
                Vertices = _verts.ToArray(),
                Triangles = _tris.ToArray(),
                Colors = _colors.ToArray(),
                Reveal = _reveal.ToArray(),
            };
        }
    }
}
