using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Camada visual do poker. Escuta o DealerManager e desenha as cartas
/// (mãos, mesa com 5 posições e reservas) usando CardVisual + CardSpriteLibrary.
/// Não altera o estado do jogo: só lê snapshots.
/// Inclui o poder de espiar: vira as cartas de um adversário até o fim da mão.
/// </summary>
public class PokerTableView : MonoBehaviour
{
    [Header("Referências")]
    public DealerManager dealer;
    public CardSpriteLibrary spriteLibrary;
    public CardVisual cardPrefab;

    [Header("Containers (use Horizontal Layout Group em cada um)")]
    [Tooltip("Um container por jogador, na ordem dos índices (0, 1, 2...)")]
    public RectTransform[] playerHandContainers;
    public RectTransform boardContainer;
    public RectTransform reservedContainer;

    [Header("Opções")]
    [Tooltip("Ligado: mostra as mãos de todos. Desligado: só a mão do jogador controlado (comparsa) fica virada para cima.")]
    public bool showAllHands = false;
    [Tooltip("Escreve no Console quais mãos estão visíveis a cada atualização (para depurar).")]
    public bool debugLog = false;
    [Tooltip("No showdown, revela as mãos dos jogadores que não deram fold.")]
    public bool revealAtShowdown = true;
    [Tooltip("Mostra as cartas reservadas (de troca) viradas para cima. Necessário para a trapaça.")]
    public bool showReservedCards = true;
    public int boardSlots = 5;
    public TMP_Text resultText;

    [Header("Apostas")]
    [Tooltip("Texto do pote total (soma de tudo que já foi apostado na mão)")]
    public TMP_Text potText;
    [Tooltip("Um texto por jogador, na ordem dos índices: quanto ele apostou NESTA rodada")]
    public TMP_Text[] playerBetTexts;
    [Tooltip("Esconde o texto quando o jogador ainda não apostou nada na rodada")]
    public bool hideZeroBets = true;

    [Header("Poder: espiar adversário")]
    [Tooltip("Um botão por assento, mesmos índices dos jogadores. Deixe vazio no assento do comparsa.")]
    public Button[] peekButtons;

    [Tooltip("Texto do botão. {0} é substituído pelo custo de suspeita.")]
    public string peekLabelFormat = "Espiar (+{0})";

    private UnityAction[] peekActions;

    private void OnEnable()
    {
        if (dealer == null) return;
        dealer.StateChanged += OnStateChanged;
        BindPeekButtons();
        if (dealer.Game != null) Refresh();
    }

    private void OnDisable()
    {
        if (dealer != null) dealer.StateChanged -= OnStateChanged;
        UnbindPeekButtons();
    }

    private void OnStateChanged(PokerGameEvent change) => Refresh();

    // ---------- Poder de espiar ----------

    /// <summary>
    /// Pede ao jogo para virar as cartas do assento até o fim da mão. O jogo valida a regra,
    /// soma a suspeita e dispara o evento que atualiza a tela.
    /// </summary>
    public bool PeekSeat(int seat)
    {
        if (dealer == null || dealer.Game == null)
        {
            Debug.LogWarning("[PokerTableView] Espiar: DealerManager ou Game não existe.", this);
            return false;
        }
        if (!dealer.Game.CanPeek(seat))
        {
            Debug.LogWarning($"[PokerTableView] Espiar recusado no assento {seat}: {WhyNotPeek(seat)}", this);
            return false;
        }
        dealer.Game.PeekOpponent(seat);
        Debug.Log($"[PokerTableView] Espiou o assento {seat}. Suspeita agora: {dealer.Game.Suspicion}", this);
        return true;
    }

    private string WhyNotPeek(int seat)
    {
        PokerGameSnapshot s = dealer.Game.CaptureSnapshot();
        if (seat < 0 || seat >= s.Players.Count) return $"assento fora do intervalo (0 a {s.Players.Count - 1})";
        if (seat == s.ControlledPlayerIndex) return "esse assento é o jogador controlado (comparsa); confira Controlled Player Index e a ordem do array Peek Buttons";
        if (s.MatchOutcome != PokerMatchOutcome.InProgress) return $"a partida terminou ({s.MatchOutcome})";
        if (s.IsHandOver) return "a mão terminou; comece a próxima";
        if (s.Players[seat].IsEliminated) return "jogador falido";
        if (s.Players[seat].HasFolded) return "jogador já deu fold";
        if (s.Players[seat].IsPeeked) return "já foi espiado nesta mão";
        return "motivo desconhecido";
    }

    private void BindPeekButtons()
    {
        if (peekButtons == null || peekButtons.Length == 0)
        {
            Debug.LogWarning("[PokerTableView] Peek Buttons está vazio: nenhum botão de espiar foi ligado.", this);
            return;
        }
        peekActions = new UnityAction[peekButtons.Length];
        for (int i = 0; i < peekButtons.Length; i++)
        {
            if (peekButtons[i] == null) continue;
            int seat = i;
            peekActions[i] = () =>
            {
                Debug.Log($"[PokerTableView] Botão de espiar clicado (assento {seat}).", this);
                PeekSeat(seat);
            };
            peekButtons[i].onClick.AddListener(peekActions[i]);
        }
    }

    private void UnbindPeekButtons()
    {
        if (peekButtons == null || peekActions == null) return;
        for (int i = 0; i < Math.Min(peekButtons.Length, peekActions.Length); i++)
            if (peekButtons[i] != null && peekActions[i] != null)
                peekButtons[i].onClick.RemoveListener(peekActions[i]);
    }

    private void UpdatePeekButtons(PokerGameSnapshot state)
    {
        if (peekButtons == null) return;
        for (int i = 0; i < peekButtons.Length; i++)
        {
            Button button = peekButtons[i];
            if (button == null) continue;
            button.interactable = dealer.Game.CanPeek(i);
            TMP_Text label = button.GetComponentInChildren<TMP_Text>();
            if (label != null && !string.IsNullOrEmpty(peekLabelFormat))
                label.text = string.Format(peekLabelFormat, state.PeekSuspicionCost);
        }
    }

    // ---------- Desenho ----------

    private void Refresh()
    {
        PokerGameSnapshot state = dealer.Game.CaptureSnapshot();

        // Mãos dos jogadores
        for (int i = 0; i < playerHandContainers.Length; i++)
        {
            if (playerHandContainers[i] == null) continue;
            Clear(playerHandContainers[i]);
            if (i >= state.Players.Count) continue;

            bool atShowdown = revealAtShowdown && state.Street == PokerStreet.Showdown
                && !state.Players[i].HasFolded;
            bool visible = showAllHands || i == state.ControlledPlayerIndex
                || atShowdown || state.Players[i].IsPeeked;
            if (debugLog)
                Debug.Log($"[PokerTableView] jogador {i} (controlado={state.ControlledPlayerIndex}) " +
                          $"visivel={visible} espiado={state.Players[i].IsPeeked} street={state.Street}", this);
            foreach (Card card in state.Players[i].HoleCards)
                Spawn(card, visible, playerHandContainers[i], state.Players[i].HasFolded);
        }

        // Mesa: sempre 5 posições; as ainda não reveladas aparecem viradas
        if (boardContainer != null)
        {
            Clear(boardContainer);
            for (int slot = 0; slot < boardSlots; slot++)
            {
                if (slot < state.Board.Count) Spawn(state.Board[slot], true, boardContainer, false);
                else Spawn(null, false, boardContainer, false);
            }
        }

        // Cartas reservadas (usadas na troca/trapaça)
        if (reservedContainer != null)
        {
            Clear(reservedContainer);
            foreach (Card card in state.ReservedCards)
                Spawn(card, showReservedCards, reservedContainer, false);
        }

        UpdateBets(state);
        UpdatePeekButtons(state);
        UpdateResult(state);
    }

    private void UpdateBets(PokerGameSnapshot state)
    {
        if (potText != null) potText.text = $"Pote: {state.Pot}";

        if (playerBetTexts == null) return;
        for (int i = 0; i < playerBetTexts.Length; i++)
        {
            if (playerBetTexts[i] == null) continue;
            if (i >= state.Players.Count) { playerBetTexts[i].text = ""; continue; }

            int bet = state.Players[i].StreetContribution;
            playerBetTexts[i].text = (bet == 0 && hideZeroBets) ? "" : bet.ToString();
        }
    }

    private void Spawn(Card source, bool faceUp, RectTransform parent, bool dimmed)
    {
        // Cópia local: a visibilidade é decisão da view, não do estado do jogo.
        Card display = source != null
            ? new Card(source.suit, source.rank, faceUp)
            : new Card(Card.Suit.Copas, Card.Rank.Dois, false);

        CardVisual visual = Instantiate(cardPrefab, parent);
        visual.Setup(display, spriteLibrary);

        if (dimmed)
        {
            var img = visual.GetComponent<Image>();
            if (img != null) img.color = new Color(1f, 1f, 1f, 0.35f);
        }
    }

    private static void Clear(RectTransform container)
    {
        for (int i = container.childCount - 1; i >= 0; i--)
        {
            GameObject child = container.GetChild(i).gameObject;
            child.SetActive(false); // evita que o layout conte o objeto até o Destroy
            Destroy(child);
        }
    }

    private void UpdateResult(PokerGameSnapshot state)
    {
        if (resultText == null) return;
        if (!state.IsHandOver) { resultText.text = ""; return; }

        if (state.MatchOutcome == PokerMatchOutcome.Caught)
        {
            resultText.text = PokerMatchDisplay.Outcome(state.MatchOutcome);
            return;
        }

        if (state.WinnerByFoldIndex >= 0)
        {
            resultText.text = $"{state.Players[state.WinnerByFoldIndex].Name} vence (todos desistiram) | Pote: {state.Pot}";
            return;
        }

        if (state.Board.Count < 3) { resultText.text = ""; return; }

        PokerHandValue best = default;
        bool first = true;
        var winners = new List<int>();
        for (int i = 0; i < state.Players.Count; i++)
        {
            PokerSeatSnapshot player = state.Players[i];
            // Ignora quem desistiu, faliu ou não tem exatamente duas cartas.
            if (player.HasFolded || player.IsEliminated || player.HoleCards.Count != 2) continue;
            PokerHandValue value = PokerHandEvaluator.EvaluateHoldem(player.HoleCards, state.Board);
            if (first || value > best) { best = value; winners.Clear(); winners.Add(i); first = false; }
            else if (value == best) winners.Add(i);
        }
        if (first) { resultText.text = ""; return; }

        var names = new List<string>();
        foreach (int w in winners) names.Add(state.Players[w].Name);
        resultText.text = $"Showdown: {string.Join(" e ", names)} ({best.Category}) | Pote: {state.Pot}";
    }
}