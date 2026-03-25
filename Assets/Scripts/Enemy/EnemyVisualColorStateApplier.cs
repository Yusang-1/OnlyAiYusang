using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Enemy의 상태(EnemyVisualStateId)에 따라 렌더러 색을 오버라이드합니다.
/// - 오버라이드가 있으면 해당 색으로 덮어씀
/// - 오버라이드가 없으면 캡처해둔 원래 색으로 복구
/// </summary>
public sealed class EnemyVisualColorStateApplier : MonoBehaviour
{
    private const int InvalidColorPropertyId = -1;
    private const int DefaultRendererListCapacity = 8;

    [Header("Visual State (Color Override)")]
    [Tooltip("해당 EnemyVisualStateId에 대해 렌더러 색을 덮어씁니다. 목록에 없으면 원래 색으로 복구합니다.")]
    [SerializeField] private VisualColorOverride[] visualColorOverrides = new[]
    {
        new VisualColorOverride { stateId = Enemy.EnemyVisualStateId.Chase, color = Color.black }
    };

    [System.Serializable]
    private struct VisualColorOverride
    {
        public Enemy.EnemyVisualStateId stateId;
        public Color color;
    }

    private struct RendererColorInfo
    {
        public Renderer renderer;
        public int colorPropertyId; // _BaseColor 또는 _Color
        public Color originalColor;
    }

    private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");

    private readonly List<RendererColorInfo> _rendererColorInfos = new List<RendererColorInfo>(DefaultRendererListCapacity);
    private readonly Dictionary<Enemy.EnemyVisualStateId, Color> _overridesMap = new Dictionary<Enemy.EnemyVisualStateId, Color>();
    private MaterialPropertyBlock _propertyBlock;

    private bool _captured;

    private void Awake()
    {
        // MaterialPropertyBlock은 Unity 네이티브 리소스를 생성하므로,
        // 생성자/필드 초기화가 아니라 Awake에서 생성합니다.
        _propertyBlock = new MaterialPropertyBlock();
        BuildOverridesMap();
    }

    private void BuildOverridesMap()
    {
        _overridesMap.Clear();

        if (visualColorOverrides == null)
            return;

        for (int i = 0; i < visualColorOverrides.Length; i++)
        {
            VisualColorOverride ov = visualColorOverrides[i];
            if (_overridesMap.ContainsKey(ov.stateId))
                continue;

            _overridesMap.Add(ov.stateId, ov.color);
        }
    }

    public void ApplyState(Enemy.EnemyVisualStateId visualStateId)
    {
        if (!_captured)
            CaptureOriginalEnemyColors();

        bool hasOverride = _overridesMap.TryGetValue(visualStateId, out Color overrideColor);

        for (int i = 0; i < _rendererColorInfos.Count; i++)
        {
            RendererColorInfo info = _rendererColorInfos[i];
            if (info.renderer == null)
                continue;

            Color target = hasOverride ? overrideColor : info.originalColor;

            _propertyBlock.Clear();
            info.renderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(info.colorPropertyId, target);
            info.renderer.SetPropertyBlock(_propertyBlock);
        }
    }

    private void CaptureOriginalEnemyColors()
    {
        _rendererColorInfos.Clear();

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null)
                continue;

            if (!TryResolveColorPropertyId(r, out int colorPropertyId))
                continue;

            Color original = GetMaterialColor(r, colorPropertyId);

            _rendererColorInfos.Add(new RendererColorInfo
            {
                renderer = r,
                colorPropertyId = colorPropertyId,
                originalColor = original
            });
        }

        _captured = true;
    }

    private static bool TryResolveColorPropertyId(Renderer renderer, out int colorPropertyId)
    {
        colorPropertyId = InvalidColorPropertyId;
        if (renderer == null)
            return false;

        Material[] mats = renderer.sharedMaterials;
        for (int i = 0; i < mats.Length; i++)
        {
            Material m = mats[i];
            if (m == null)
                continue;

            // URP/HDRP Lit 계열: _BaseColor 우선
            if (m.HasProperty("_BaseColor"))
            {
                colorPropertyId = BaseColorPropertyId;
                return true;
            }

            // Built-in Standard 계열: _Color
            if (m.HasProperty("_Color"))
            {
                colorPropertyId = ColorPropertyId;
                return true;
            }
        }

        // HasProperty에 string이 필요하므로, 여기서는 _BaseColor / _Color만 지원합니다.
        return false;
    }

    private static Color GetMaterialColor(Renderer renderer, int colorPropertyId)
    {
        if (renderer == null)
            return Color.white;

        Material[] mats = renderer.sharedMaterials;
        for (int i = 0; i < mats.Length; i++)
        {
            Material m = mats[i];
            if (m == null)
                continue;

            // propertyId는 Shader.PropertyToID 기반이므로 HasProperty(string) 대신 GetColor 시도.
            // (colorPropertyId에 대해 실제로 값이 없다면 GetColor는 Color.white를 반환하는 편입니다.)
            if (m.HasProperty(colorPropertyId == BaseColorPropertyId ? "_BaseColor" : "_Color"))
                return m.GetColor(colorPropertyId);
        }

        return Color.white;
    }
}

