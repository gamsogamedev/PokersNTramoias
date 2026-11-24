using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Camada visual do poker. Escuta o DealerManager e desenha as cartas
/// (mãos, mesa com 5 posições e reservas) usando CardVisual + CardSpriteLibrary.
/// Não altera o estado do jogo: só lê snapshots.
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
    [Tooltip("O dealer vê todas as cartas (mãos e reservas). Desligue para ver só as do jogador controlado.")]
    public bool dealerSeesAllCards = true;
    public int boardSlots = 5;
    public TMP_Text resultText;

    private void OnEnable()
    {
        if (dealer == null) return;
        dealer.StateChanged += OnStateChanged;
        if (dealer.Game != null) Refresh();
    }

    private void OnDisable()
    {
        if (dealer != null) dealer.StateChanged -= OnStateChanged;
    }

    private void OnStateChanged(PokerGameEvent _) => Refresh();

    private void Refresh()
    {
        PokerGameSnapshot state = dealer.Game.CaptureSnapshot();

        // Mãos dos jogadores
        for (int i = 0; i < playerHandContainers.Length; i++)
        {
            if (playerHandContainers[i] == null) continue;
            Clear(playerHandContainers[i]);
            if (i >= state.Players.Count) continue;

            bool visible = dealerSeesAllCards || i == state.ControlledPlayerIndex || state.IsHandOver;
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
                Spawn(card, dealerSeesAllCards, reservedContainer, false);
        }

        UpdateResult(state);
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
            var img = visual.GetComponent<UnityEngine.UI.Image>();
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
        var lines = new List<string>();
        if (state.MatchOutcome != PokerMatchOutcome.InProgress)
            lines.Add(PokerMatchDisplay.Outcome(state.MatchOutcome));
        for (int i = 0; i < state.Players.Count; i++)
        {
            if (state.Players[i].IsEliminated) lines.Add($"J{i + 1} faliu");
            if ((state.WinnerByFoldIndex >= 0 || state.Street == PokerStreet.Showdown) && state.Payouts[i] > 0)
                lines.Add($"J{i + 1} recebeu {state.Payouts[i]}");
        }
        if (state.WinnerByFoldIndex >= 0) lines.Insert(0, $"J{state.WinnerByFoldIndex + 1} venceu por desistência");
        resultText.text = string.Join("  /  ", lines);
    }
}
