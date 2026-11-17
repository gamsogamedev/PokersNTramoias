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

    private void Start()
    {
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

    /// <summary>Starts a fresh preflop hand. Stacks reset until settlement is implemented.</summary>
    [ContextMenu("Poker/New hand")]
    public void PlayRound()
    {
        if (Game == null)
        {
            Game = new PokerGameState(numeroDeJogadores, controlledPlayerIndex, startingChips, minimumBet);
            Game.StateChanged += OnStateChanged;
        }
        Game.StartNewHand();
    }

    [ContextMenu("Poker/Next street")]
    public void AdvanceStreet() { if (Game != null && Game.CanAdvanceStreet) Game.AdvanceStreet(); }

    [ContextMenu("Poker/Check or call current player")]
    public void CheckOrCall()
    {
        if (Game != null && Game.CanCheckOrCall) Game.CheckOrCall(Game.ActingPlayerIndex);
    }

    [ContextMenu("Poker/Raise minimum for current player")]
    public void RaiseMinimum()
    {
        if (Game != null && Game.CanRaise)
            Game.BetOrRaiseTo(Game.ActingPlayerIndex, Game.CurrentBet + Game.MinimumRaise);
    }

    [ContextMenu("Poker/Fold current player")]
    public void FoldCurrentPlayer()
    {
        if (Game != null && Game.ActingPlayerIndex >= 0) Game.Fold(Game.ActingPlayerIndex);
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
                    + (player.HasFolded ? " | FOLDED" : player.IsAllIn ? " | ALL IN" : "");
            }
        if (textoMesa != null) textoMesa.text = $"Mesa: {CardsText(state.Board)}";
        if (textoStreet != null) textoStreet.text = $"Hand {state.HandNumber} / {state.Street}"
            + (state.IsHandOver ? " / complete" : "");
        if (textoReservas != null) textoReservas.text = $"Reserved: {CardsText(state.ReservedCards)}";
        if (textoDescartes != null) textoDescartes.text = $"Discards: {CardsText(state.Discards)}";
        if (textoApostas != null) textoApostas.text = $"Pot: {state.Pot} | Bet: {state.CurrentBet} | "
            + (state.ActingPlayerIndex < 0 ? "Betting complete" : $"{state.Players[state.ActingPlayerIndex].Name} to act");
        if (nextStreetButton != null) nextStreetButton.interactable = Game.CanAdvanceStreet;
        if (checkCallButton != null) checkCallButton.interactable = state.CanCheckOrCall;
        if (raiseButton != null) raiseButton.interactable = state.CanRaise;
        if (foldButton != null) foldButton.interactable = state.ActingPlayerIndex >= 0;
        if (swapButtons != null)
            for (int i = 0; i < swapButtons.Length; i++)
                if (swapButtons[i] != null) swapButtons[i].interactable = state.CanSwap && i < state.ReservedCards.Count;
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
