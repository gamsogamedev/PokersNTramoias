using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Unity adapter for the shared poker state. UI and enemy policies submit actions here.</summary>
[DisallowMultipleComponent]
public class DealerManager : MonoBehaviour
{
    [Header("Table configuration")]
    [Range(2, 10)] public int numeroDeJogadores = 3;
    [Min(0)] public int controlledPlayerIndex;
    [Min(1)] public int startingChips = 1000;
    [Min(1)] public int minimumBet = 20;
    public bool showAllHoleCards = true;

    [Header("Optional TextMeshPro UI")]
    public TMP_Text[] textosMaosJogadores;
    public TMP_Text textoMesa;
    public TMP_Text textoStreet;
    public TMP_Text textoReservas;
    public TMP_Text textoDescartes;
    public TMP_Text textoApostas;

    [Header("Optional buttons (listeners assigned at startup)")]
    public Button nextStreetButton;
    public Button checkCallButton;
    public Button raiseButton;
    public Button foldButton;
    public Button[] swapButtons;

    public PokerGameState Game { get; private set; }
    public event Action<PokerGameEvent> StateChanged;
    private UnityEngine.Events.UnityAction[] swapActions;

    private Image suspicionFill;
    private readonly PokerEnemyTurns enemyTurns = new PokerEnemyTurns();
    private bool PlayerCanAct => Game != null && Game.ActingPlayerIndex == Game.ControlledPlayerIndex;

    private void Update() => enemyTurns.Tick(Game, Time.unscaledTimeAsDouble);
    private void OnDisable() => enemyTurns.Cancel();

    private void Start()
    {
        if (textoStreet != null) suspicionFill = PokerMatchDisplay.CreateBar(textoStreet.transform, -35, 360);
        if (nextStreetButton != null) nextStreetButton.onClick.AddListener(AdvanceStreet);
        if (checkCallButton != null) checkCallButton.onClick.AddListener(CheckOrCall);
        if (raiseButton != null) raiseButton.onClick.AddListener(RaiseMinimum);
        if (foldButton != null) foldButton.onClick.AddListener(FoldCurrentPlayer);
        if (swapButtons != null)
        {
            swapActions = new UnityEngine.Events.UnityAction[swapButtons.Length];
            for (int i = 0; i < swapButtons.Length; i++)
            {
                int index = i;
                swapActions[i] = () => SwapReservedCard(index);
                if (swapButtons[i] != null) swapButtons[i].onClick.AddListener(swapActions[i]);
            }
        }
        if (Game == null) PlayRound();
    }

    /// <summary>Deals the next hand or begins a new match after the outcome.</summary>
    [ContextMenu("Poker/New hand")]
    public void PlayRound()
    {
        if (Game == null)
        {
            Game = new PokerGameState(numeroDeJogadores, controlledPlayerIndex, startingChips, minimumBet);
            Game.StateChanged += OnStateChanged;
        }
        if (Game.MatchOutcome != PokerMatchOutcome.InProgress) Game.StartNewMatch();
        else if (Game.HandNumber == 0 || Game.IsHandOver) Game.StartNewHand();
    }

    [ContextMenu("Poker/Next street")]
    public void AdvanceStreet() { if (Game == null) return; if (Game.IsHandOver) PlayRound(); else if (Game.CanAdvanceStreet) Game.AdvanceStreet(); }

    [ContextMenu("Poker/Check or call current player")]
    public void CheckOrCall()
    {
        if (PlayerCanAct && Game.CanCheckOrCall) Game.CheckOrCall(Game.ActingPlayerIndex);
    }

    [ContextMenu("Poker/Raise minimum for current player")]
    public void RaiseMinimum()
    {
        if (PlayerCanAct && Game.CanRaise)
            Game.BetOrRaiseTo(Game.ActingPlayerIndex, Game.MinimumRaiseTarget);
    }

    [ContextMenu("Poker/Fold current player")]
    public void FoldCurrentPlayer()
    {
        if (PlayerCanAct) Game.Fold(Game.ActingPlayerIndex);
    }

    public void SwapReservedCard(int index)
    {
        if (Game == null || !Game.CanSwap) return;
        if (index >= 0 && index < Game.CaptureSnapshot().ReservedCards.Count) Game.SwapCurrentStreetCard(index);
    }

    private void OnStateChanged(PokerGameEvent change)
    {
        RefreshUI();
        StateChanged?.Invoke(change);
    }

    private void RefreshUI()
    {
        PokerGameSnapshot state = Game.CaptureSnapshot();
        if (textosMaosJogadores != null)
            for (int i = 0; i < textosMaosJogadores.Length; i++)
            {
                if (textosMaosJogadores[i] == null) continue;
                if (i >= state.Players.Count) { textosMaosJogadores[i].text = ""; continue; }
                PokerSeatSnapshot player = state.Players[i];
                bool visible = showAllHoleCards || i == state.ControlledPlayerIndex;
                string hand = visible ? CardsText(player.HoleCards) : "?? ??";
                textosMaosJogadores[i].text = $"{player.Name}: {hand} | {player.Chips} chips"
                    + (player.IsEliminated ? " | FALIDO" : player.HasFolded ? " | FOLDED" : player.IsAllIn ? " | ALL IN" : "");
            }
        if (textoMesa != null) textoMesa.text = $"Mesa: {CardsText(state.Board)}";
        if (textoStreet != null) textoStreet.text = $"Hand {state.HandNumber} / {state.Street}"
            + $" / Suspeita {state.Suspicion}/100";
        if (suspicionFill != null) PokerMatchDisplay.SetLevel(suspicionFill, state.Suspicion);
        if (textoReservas != null) textoReservas.text = $"Reserved: {CardsText(state.ReservedCards)}";
        if (textoDescartes != null) textoDescartes.text = $"Discards: {CardsText(state.Discards)}";
        if (textoApostas != null) textoApostas.text = $"Pot: {state.Pot} | Bet: {state.CurrentBet} | "
            + (state.ActingPlayerIndex < 0 ? "Betting complete" : $"{state.Players[state.ActingPlayerIndex].Name} to act");
        if (nextStreetButton != null)
        {
            nextStreetButton.interactable = Game.CanAdvanceStreet || state.IsHandOver;
            SetLabel(nextStreetButton, state.MatchOutcome != PokerMatchOutcome.InProgress ? "Nova partida"
                : state.IsHandOver ? "Próxima mão" : "Avançar");
        }
        if (state.CanRaise) SetLabel(raiseButton, $"Apostar {Game.MinimumRaiseTarget}");
        if (checkCallButton != null) checkCallButton.interactable = PlayerCanAct && state.CanCheckOrCall;
        if (raiseButton != null) raiseButton.interactable = PlayerCanAct && state.CanRaise;
        if (foldButton != null) foldButton.interactable = PlayerCanAct;
        if (swapButtons != null)
            for (int i = 0; i < swapButtons.Length; i++)
                if (swapButtons[i] != null)
                {
                    swapButtons[i].interactable = state.CanSwap && i < state.ReservedCards.Count;
                    SetLabel(swapButtons[i], $"Trocar {i + 1} (+{state.SuccessfulSwapCount + 1})");
                }
    }

    private static void SetLabel(Button button, string value)
    {
        if (button == null) return;
        TMP_Text label = button.GetComponentInChildren<TMP_Text>();
        if (label != null) label.text = value;
    }

    private static string CardsText(System.Collections.Generic.IReadOnlyList<Card> cards)
    {
        var names = new string[cards.Count];
        for (int i = 0; i < names.Length; i++) names[i] = cards[i].ToShortString();
        return names.Length == 0 ? "--" : string.Join(" ", names);
    }

    private void OnDestroy()
    {
        if (Game != null) Game.StateChanged -= OnStateChanged;
        if (nextStreetButton != null) nextStreetButton.onClick.RemoveListener(AdvanceStreet);
        if (checkCallButton != null) checkCallButton.onClick.RemoveListener(CheckOrCall);
        if (raiseButton != null) raiseButton.onClick.RemoveListener(RaiseMinimum);
        if (foldButton != null) foldButton.onClick.RemoveListener(FoldCurrentPlayer);
        if (swapActions != null && swapButtons != null)
            for (int i = 0; i < Math.Min(swapActions.Length, swapButtons.Length); i++)
                if (swapButtons[i] != null) swapButtons[i].onClick.RemoveListener(swapActions[i]);
    }
}

internal static class PokerMatchDisplay
{
    public static string Outcome(PokerMatchOutcome outcome)
    {
        switch (outcome)
        {
            case PokerMatchOutcome.PlayerWon: return "Você venceu a partida";
            case PokerMatchOutcome.Caught: return "Trapaça descoberta";
            case PokerMatchOutcome.PlayerBankrupt: return "Você faliu";
            default: return "";
        }
    }

    public static Image CreateBar(Transform parent, float y, float width)
    {
        Image track = new GameObject("Suspeita", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        track.transform.SetParent(parent, false);
        track.rectTransform.anchoredPosition = new Vector2(0, y);
        track.rectTransform.sizeDelta = new Vector2(width, 10);
        track.color = new Color32(64, 73, 72, 255);
        track.raycastTarget = false;

        Image fill = new GameObject("Nivel", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        fill.transform.SetParent(track.transform, false);
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = Vector2.up;
        fill.rectTransform.sizeDelta = Vector2.zero;
        fill.color = new Color32(246, 164, 68, 255);
        fill.raycastTarget = false;
        return fill;
    }

    public static void SetLevel(Image fill, int suspicion)
    {
        fill.rectTransform.anchorMax = new Vector2(suspicion / 100f, 1);
    }
}
