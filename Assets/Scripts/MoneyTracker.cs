using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Acompanha o dinheiro de todos os jogadores. Tem duas visualizações:
/// - goalBar: a fatia do dinheiro total que está com o comparsa (seu objetivo, 100% = vitória)
/// - playerBars: uma barra por jogador mostrando sua fatia do dinheiro total
///
/// O motor de poker (futuro) deve usar TransferChips() ao pagar o pote.
/// </summary>
public class MoneyTracker : MonoBehaviour
{
    [Header("UI")]
    public SmoothBar goalBar;
    public SmoothBar[] playerBars; // na mesma ordem da lista de jogadores (opcional)

    [Header("Eventos")]
    public UnityEvent onGoalReached;

    private List<Player> players;
    private bool goalFired;

    public int TotalChips { get; private set; }
    public Player Accomplice { get; private set; }

    /// <summary>
    /// Chamado pelo DealerManager depois de criar os jogadores.
    /// </summary>
    public void Initialize(List<Player> playerList)
    {
        players = playerList;
        goalFired = false;

        TotalChips = 0;
        foreach (var p in players) TotalChips += p.chips;

        Accomplice = players.Find(p => p.isAccomplice);
        if (Accomplice == null)
            Debug.LogWarning("MoneyTracker: nenhum jogador marcado como comparsa (isAccomplice).");

        Refresh(instant: true);
    }

    /// <summary>
    /// Move fichas de um jogador para outro (use isso ao pagar o pote).
    /// Retorna quanto foi realmente transferido (limitado ao que o jogador tem).
    /// </summary>
    public int TransferChips(Player from, Player to, int amount)
    {
        int real = Mathf.Min(amount, from.chips);
        if (real <= 0) return 0;

        from.chips -= real;
        to.chips += real;
        Refresh();
        return real;
    }

    /// <summary>
    /// Atalho útil pra debug e pra trapaças: tira fichas dos oponentes
    /// (um por vez, até acabar) e entrega pro comparsa.
    /// </summary>
    public int MoveToAccomplice(int amount)
    {
        if (Accomplice == null) return 0;

        int restante = amount;
        foreach (var p in players)
        {
            if (p == Accomplice || p.chips <= 0) continue;
            restante -= TransferChips(p, Accomplice, restante);
            if (restante <= 0) break;
        }
        return amount - restante;
    }

    public void Refresh(bool instant = false)
    {
        if (players == null || TotalChips <= 0) return;

        // Barra de objetivo: fatia do comparsa
        if (goalBar != null && Accomplice != null)
        {
            float share = (float)Accomplice.chips / TotalChips;
            string text = $"Comparsa: ${Accomplice.chips} / ${TotalChips}";
            if (instant) goalBar.SetValueInstant(share, text);
            else goalBar.SetValue(share, text);
        }

        // Barras individuais
        if (playerBars != null)
        {
            for (int i = 0; i < players.Count && i < playerBars.Length; i++)
            {
                if (playerBars[i] == null) continue;

                float share = (float)players[i].chips / TotalChips;
                string text = $"{players[i].playerName}: ${players[i].chips}";
                if (instant) playerBars[i].SetValueInstant(share, text);
                else playerBars[i].SetValue(share, text);
            }
        }

        // Vitória: comparsa ficou com tudo
        if (!goalFired && Accomplice != null && Accomplice.chips >= TotalChips)
        {
            goalFired = true;
            onGoalReached?.Invoke();
        }
    }
}
