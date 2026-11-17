using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Barra de UI reutilizável com animação suave. Usada tanto pela barra de
/// desconfiança quanto pelas barras de dinheiro.
///
/// Setup: crie um objeto UI Image, mude o "Image Type" para "Filled" e
/// "Fill Method" para "Horizontal". Importante: a Image precisa ter algum
/// Source Image atribuído (pode ser o sprite padrão "UISprite"), senão o
/// preenchimento não funciona.
/// </summary>
public class SmoothBar : MonoBehaviour
{
    [Header("Referências")]
    public Image fillImage;
    public TMP_Text label; // opcional

    [Header("Animação")]
    public float smoothSpeed = 5f;

    [Header("Cor (opcional)")]
    public bool useGradient = false;
    public Gradient colorGradient;

    private float targetValue;
    private float displayedValue;

    /// <summary>
    /// Define o valor da barra (0 a 1) e, opcionalmente, o texto exibido.
    /// </summary>
    public void SetValue(float normalized, string labelText = null)
    {
        targetValue = Mathf.Clamp01(normalized);

        if (label != null && labelText != null)
            label.text = labelText;
    }

    /// <summary>
    /// Pula direto pro valor, sem animar (útil na inicialização).
    /// </summary>
    public void SetValueInstant(float normalized, string labelText = null)
    {
        targetValue = Mathf.Clamp01(normalized);
        displayedValue = targetValue;
        ApplyVisuals();

        if (label != null && labelText != null)
            label.text = labelText;
    }

    private void Update()
    {
        if (Mathf.Approximately(displayedValue, targetValue)) return;

        displayedValue = Mathf.Lerp(displayedValue, targetValue, Time.deltaTime * smoothSpeed);

        // Evita ficar "quase chegando" pra sempre
        if (Mathf.Abs(displayedValue - targetValue) < 0.001f)
            displayedValue = targetValue;

        ApplyVisuals();
    }

    private void ApplyVisuals()
    {
        if (fillImage == null) return;

        fillImage.fillAmount = displayedValue;

        if (useGradient)
            fillImage.color = colorGradient.Evaluate(displayedValue);
    }
}
