using UnityEngine;

namespace WallHitSound.Services.Effects
{
    /// <summary>
    /// 作り置きした形を 1 回分再生する。出現 → 保持 → 消滅で終わり、
    /// 終わったら GameObject を非アクティブにしてプールへ戻る（破棄はしない）。
    /// Mesh も含めて全パターンを事前に組んであるので、衝突時にやるのは
    /// 差し替えと頂点カラーの書き換えだけ。
    /// 濃さは頂点カラーのアルファだけで表しているので、シェーダのプロパティ名に依存しない。
    /// </summary>
    internal class HitEffectAnimator : MonoBehaviour
    {
        /// <summary>出現にかける秒数。</summary>
        public const float GrowTime = 0.07f;
        /// <summary>出しきったまま保つ秒数。</summary>
        public const float HoldTime = 0.13f;
        /// <summary>消えるまでの秒数。</summary>
        public const float FadeTime = 0.20f;

        /// <summary>出現の波のぼかし幅（ひび割れが外へ広がる速さ）。</summary>
        private const float RevealSoftness = 0.35f;

        /// <summary>1 パターン分の、すぐ表示できる状態にした Mesh。</summary>
        private class Prepared
        {
            public Mesh Mesh;
            public EffectGeometry Source;
            /// <summary>毎フレーム書き換える作業用の色。使い回すので確保は 1 回だけ。</summary>
            public Color32[] Colors;
        }

        private MeshFilter _filter;
        private Prepared[] _burst;
        private Prepared[] _crack;
        private Prepared _active;

        private float _elapsed;
        private float _scale = 1f;
        private float _opacity = 1f;
        private float _overshoot = 1f;
        private bool _useReveal;

        // 前フレームと同じなら頂点カラーを書き直さないための控え
        private float _lastAlpha = -1f;
        private float _lastReveal = -1f;

        private void Awake()
        {
            _filter = gameObject.AddComponent<MeshFilter>();

            var meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = EffectMaterial.Mesh;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }

        /// <summary>
        /// 全パターンの Mesh を組んでおく。メニュー到達時と、色を変えたときに呼ぶ。
        /// </summary>
        public void Prepare(EffectGeometry[] burst, EffectGeometry[] crack)
        {
            ReleaseMeshes();
            _burst = Build(burst, "Burst");
            _crack = Build(crack, "Crack");
            _active = null;
        }

        private static Prepared[] Build(EffectGeometry[] source, string label)
        {
            if (source == null) return null;

            var result = new Prepared[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                var geometry = source[i];
                var mesh = new Mesh { name = "WallHitEffect" + label + i };
                mesh.MarkDynamic();
                mesh.vertices = geometry.Vertices;
                mesh.colors32 = geometry.Colors;
                mesh.triangles = geometry.Triangles;
                mesh.RecalculateBounds();

                result[i] = new Prepared
                {
                    Mesh = mesh,
                    Source = geometry,
                    Colors = (Color32[])geometry.Colors.Clone(),
                };
            }
            return result;
        }

        /// <summary>Prepare を通っていて表示できる状態か。</summary>
        public bool IsReady
        {
            get { return _burst != null && _crack != null; }
        }

        /// <summary>
        /// 1 回分の再生を始める。
        /// </summary>
        /// <param name="variantIndex">作り置きした形のどれを使うか</param>
        /// <param name="crack">ひび割れなら true（中心から外へ広がるように出す）</param>
        /// <param name="position">表示位置（ワールド座標）</param>
        /// <param name="scale">大きさの倍率</param>
        /// <param name="opacity">濃さ（0～1）</param>
        /// <param name="overshoot">出現時に飛び出す量（1 で飛び出さない）</param>
        public void Play(int variantIndex, bool crack, Vector3 position, float scale, float opacity, float overshoot)
        {
            var set = crack ? _crack : _burst;
            if (set == null || set.Length == 0) return;

            _active = set[((variantIndex % set.Length) + set.Length) % set.Length];
            _filter.sharedMesh = _active.Mesh;

            transform.position = position;
            _scale = scale;
            _opacity = Mathf.Clamp01(opacity);
            _overshoot = overshoot;
            _useReveal = crack;
            _elapsed = 0f;
            _lastAlpha = -1f;
            _lastReveal = -1f;

            gameObject.SetActive(true);
            Apply(0f, 0f, crack ? 0f : 1f);
        }

        private void LateUpdate()
        {
            if (_active == null)
            {
                gameObject.SetActive(false);
                return;
            }

            _elapsed += Time.deltaTime;

            // 常に視線のほうを向かせる（形は XY 平面に作ってある）
            var cam = EffectCamera.Current;
            if (cam != null) transform.rotation = cam.transform.rotation;

            float alpha, scale, reveal;
            if (_elapsed < GrowTime)
            {
                float u = _elapsed / GrowTime;
                scale = _useReveal ? 1f : Mathf.Max(0f, EaseOutBack(u, _overshoot));
                alpha = Mathf.Min(1f, u * 3f);
                reveal = u;
            }
            else if (_elapsed < GrowTime + HoldTime)
            {
                scale = 1f;
                alpha = 1f;
                reveal = 1f;
            }
            else
            {
                float u = (_elapsed - GrowTime - HoldTime) / FadeTime;
                if (u >= 1f)
                {
                    gameObject.SetActive(false);
                    return;
                }
                scale = 1f + u * 0.06f;
                alpha = 1f - u * u;
                reveal = 1f;
            }

            Apply(scale, alpha, reveal);
        }

        private void Apply(float scale, float alpha, float reveal)
        {
            transform.localScale = Vector3.one * (_scale * scale);

            float a = alpha * _opacity;
            // 見た目が変わらないフレームでは頂点カラーを触らない
            if (Mathf.Abs(a - _lastAlpha) < 0.002f && Mathf.Abs(reveal - _lastReveal) < 0.002f) return;
            _lastAlpha = a;
            _lastReveal = reveal;

            var colors = _active.Colors;
            var baseColors = _active.Source.Colors;
            var reveals = _active.Source.Reveal;
            for (int i = 0; i < colors.Length; i++)
            {
                float v = a;
                if (_useReveal && reveal < 1f)
                {
                    // 中心に近い頂点から順に現れる
                    v *= Mathf.Clamp01((reveal * (1f + RevealSoftness) - reveals[i]) / RevealSoftness);
                }
                colors[i].a = (byte)(baseColors[i].a * v);
            }
            _active.Mesh.colors32 = colors;
        }

        /// <summary>行きすぎてから戻る動き。overshoot が 1 なら普通の減速。</summary>
        private static float EaseOutBack(float t, float overshoot)
        {
            float c = (overshoot - 1f) * 3.2f;
            float p = t - 1f;
            return 1f + (c + 1f) * p * p * p + c * p * p;
        }

        private void ReleaseMeshes()
        {
            Release(_burst);
            Release(_crack);
            _burst = null;
            _crack = null;
        }

        private static void Release(Prepared[] set)
        {
            if (set == null) return;
            foreach (var prepared in set)
            {
                if (prepared != null && prepared.Mesh != null) Destroy(prepared.Mesh);
            }
        }

        private void OnDestroy()
        {
            ReleaseMeshes();
        }
    }
}
