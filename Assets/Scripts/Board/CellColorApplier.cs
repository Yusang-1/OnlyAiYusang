using UnityEngine;

public sealed class CellColorApplier
{
    private Renderer _renderer;
    private MaterialPropertyBlock _propertyBlock;
    private int _colorPropertyId;

    private const string BaseColorProperty = "_BaseColor";
    private const string ColorProperty = "_Color";

    public void Initialize(Renderer renderer, Color availableColor, Color unavailableColor)
    {
        _renderer = renderer;
        _propertyBlock = new MaterialPropertyBlock();

        if (_renderer == null || _renderer.sharedMaterial == null)
        {
            _colorPropertyId = -1;
            return;
        }

        // URP(대개 _BaseColor) / Standard(대개 _Color) 호환
        _colorPropertyId = _renderer.sharedMaterial.HasProperty(BaseColorProperty)
            ? Shader.PropertyToID(BaseColorProperty)
            : Shader.PropertyToID(ColorProperty);

        // 초기 상태 반영은 호출 측에서 SetAvailable로 수행
    }

    public void SetAvailable(bool isAvailable, Color availableColor, Color unavailableColor)
    {
        if (_renderer == null || _propertyBlock == null || _colorPropertyId < 0)
            return;

        _renderer.GetPropertyBlock(_propertyBlock);
        _propertyBlock.SetColor(_colorPropertyId, isAvailable ? availableColor : unavailableColor);
        _renderer.SetPropertyBlock(_propertyBlock);
    }
}

