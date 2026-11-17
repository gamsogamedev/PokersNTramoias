using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Controla a desconfiança dos jogadores. O sistema de trapaça (futuro)
/// só precisa chamar AddSuspicion(valor) toda vez que uma trapaça for feita.
/// </summary>
public class SuspicionSystem : MonoBehaviour
{
    public enum Level { Calmo, Alerta, Perigo, Descoberto }

    [Header("Valores")]
    public float maxSuspicion = 100f;
    public float startingSuspicion = 0f;

    [Header("Decaimento")]
    [Tooltip("Segundos sem trapacear antes da desconfiança começar a baixar.")]
    public float decayDelay = 3f;
    [Tooltip("Quanto a desconfiança baixa por segundo depois do delay.")]
    public float decayPerSecond = 2f;

    [Header("Limites (0 a 1 da barra)")]
    [Range(0f, 1f)] public float alertThreshold = 0.4f;
    [Range(0f, 1f)] public float dangerThreshold = 0.75f;

    [Header("UI")]
    public SmoothBar bar;

    [Header("Eventos (ligue no Inspector ou via código)")]
    public UnityEvent onAlert;
    public UnityEvent onDanger;
    public UnityEvent onBusted;

    /// <summary>Disparado sempre que o nível muda (subindo ou descendo).</summary>
    public event Action<Level> OnLevelChanged;

    public float Current { get; private set; }
    public float Normalized => Current / maxSuspicion;
    public Level CurrentLevel { get; private set; } = Level.Calmo;

    private float timeSinceLastIncrease;

    private void Start()
    {
        Current = Mathf.Clamp(startingSuspicion, 0f, maxSuspicion);
        CurrentLevel = EvaluateLevel();
        if (bar != null) bar.SetValueInstant(Normalized, LabelText());
    }

    private void Update()
    {
        if (CurrentLevel == Level.Descoberto) return;

        timeSinceLastIncrease += Time.deltaTime;

        if (timeSinceLastIncrease >= decayDelay && Current > 0f)
        {
            Current = Mathf.Max(0f, Current - decayPerSecond * Time.deltaTime);
            Refresh();
        }
    }

    /// <summary>
    /// Aumenta a desconfiança. Chamado pelo sistema de trapaça.
    /// </summary>
    public void AddSuspicion(float amount)
    {
        if (CurrentLevel == Level.Descoberto) return;

        Current = Mathf.Clamp(Current + amount, 0f, maxSuspicion);
        timeSinceLastIncrease = 0f;
        Refresh();
    }

    /// <summary>
    /// Reduz a desconfiança de uma vez (ex: distração, item, evento do jogo).
    /// </summary>
    public void ReduceSuspicion(float amount)
    {
        if (CurrentLevel == Level.Descoberto) return;

        Current = Mathf.Clamp(Current - amount, 0f, maxSuspicion);
        Refresh();
    }

    public void ResetSuspicion()
    {
        Current = 0f;
        timeSinceLastIncrease = 0f;
        CurrentLevel = Level.Calmo;
        if (bar != null) bar.SetValueInstant(0f, LabelText());
    }

    private void Refresh()
    {
        if (bar != null) bar.SetValue(Normalized, LabelText());

        Level newLevel = EvaluateLevel();
        if (newLevel == CurrentLevel) return;

        bool subiu = newLevel > CurrentLevel;
        CurrentLevel = newLevel;
        OnLevelChanged?.Invoke(newLevel);

        // Eventos só disparam quando o nível sobe
        if (!subiu) return;

        switch (newLevel)
        {
            case Level.Alerta:     onAlert?.Invoke();  break;
            case Level.Perigo:     onDanger?.Invoke(); break;
            case Level.Descoberto: onBusted?.Invoke(); break;
        }
    }

    private Level EvaluateLevel()
    {
        if (Current >= maxSuspicion) return Level.Descoberto;
        if (Normalized >= dangerThreshold) return Level.Perigo;
        if (Normalized >= alertThreshold) return Level.Alerta;
        return Level.Calmo;
    }

    private string LabelText() => $"Desconfiança {Mathf.RoundToInt(Current)}%";
}
