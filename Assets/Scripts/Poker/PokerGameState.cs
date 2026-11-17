using System;
using System.Collections.Generic;

public enum PokerStreet { NotStarted, Preflop, Flop, Turn, River, Showdown }
public enum PokerGameEventType { HandStarted, StreetRevealed, Checked, Called, Raised, Folded, CardSwapped, Showdown }

/// <summary>Detached seat data. Modifying a returned Card cannot alter the live game.</summary>
public sealed class PokerSeatSnapshot
{
    public string Name { get; }
    public int Chips { get; }
    public bool HasFolded { get; }
    public bool IsAllIn => !HasFolded && Chips == 0;
    public int StreetContribution { get; }
    public int TotalContribution { get; }
    public IReadOnlyList<Card> HoleCards { get; }

    internal PokerSeatSnapshot(string name, int chips, bool folded, int streetContribution,
        int totalContribution, IReadOnlyList<Card> cards)
    {
        Name = name; Chips = chips; HasFolded = folded;
        StreetContribution = streetContribution; TotalContribution = totalContribution;
        HoleCards = PokerGameState.CopyCards(cards);
    }
}

/// <summary>Authoritative debug snapshot. Contains secrets; do not pass directly to enemy AI.</summary>
public sealed class PokerGameSnapshot
{
    public int HandNumber { get; internal set; }
    public int Version { get; internal set; }
    public PokerStreet Street { get; internal set; }
    public int ActingPlayerIndex { get; internal set; }
    public int ControlledPlayerIndex { get; internal set; }
    public int Pot { get; internal set; }
    public int CurrentBet { get; internal set; }
    public int MinimumRaise { get; internal set; }
    public int DeckCount { get; internal set; }
    public bool BettingRoundComplete { get; internal set; }
    public bool IsHandOver { get; internal set; }
    public int WinnerByFoldIndex { get; internal set; }
    public bool CanSwap { get; internal set; }
    public bool CanRaise { get; internal set; }
    public bool CanCheckOrCall { get; internal set; }
    public bool RequiresOpeningBet { get; internal set; }
    public IReadOnlyList<PokerSeatSnapshot> Players { get; internal set; }
    public IReadOnlyList<Card> Board { get; internal set; }
    public IReadOnlyList<Card> ReservedCards { get; internal set; }
    public IReadOnlyList<Card> Discards { get; internal set; }
}

/// <summary>A committed game mutation, including explicit replacement/discard identity for swaps.</summary>
public sealed class PokerGameEvent
{
    public PokerGameEventType Type { get; }
    public int HandNumber { get; }
    public int Version { get; }
    public PokerStreet Street { get; }
    public int PlayerIndex { get; }
    public int Amount { get; }
    public int BoardIndex { get; }
    public int ReservedIndex { get; }
    public Card ReplacedCard { get; }
    public Card ReplacementCard { get; }

    internal PokerGameEvent(PokerGameEventType type, int hand, int version, PokerStreet street,
        int player, int amount, int boardIndex, int reservedIndex, Card replaced, Card replacement)
    {
        Type = type; HandNumber = hand; Version = version; Street = street;
        PlayerIndex = player; Amount = amount; BoardIndex = boardIndex; ReservedIndex = reservedIndex;
        ReplacedCard = replaced == null ? null : new Card(replaced.suit, replaced.rank, replaced.faceUp);
        ReplacementCard = replacement == null ? null : new Card(replacement.suit, replacement.rank, replacement.faceUp);
    }
}

/// <summary>
/// Owns all 52 card locations, street progression, and a basic turn-based betting round.
/// No blinds, burn cards, payout/side-pot settlement, or short all-in raises yet.
/// Each new hand is a fresh test hand with reset stacks. All-in calls are supported.
/// </summary>
public sealed class PokerGameState
{
    private sealed class Seat
    {
        public readonly List<Card> Cards = new List<Card>();
        public int Chips, StreetContribution, TotalContribution;
        public bool Folded;
    }

    private readonly Seat[] seats;
    private readonly bool[] pending;
    private readonly List<Card> board = new List<Card>();
    private readonly List<Card> reserved = new List<Card>();
    private readonly List<Card> discards = new List<Card>();
    private readonly Deck deck;
    private readonly int startingChips, minimumBet;
    private bool streetUntouched, swappedThisStreet;

    public int ControlledPlayerIndex { get; }
    public bool RequiresOpeningBet { get; }
    public int HandNumber { get; private set; }
    public int Version { get; private set; }
    public PokerStreet Street { get; private set; }
    public int ActingPlayerIndex { get; private set; } = -1;
    public int Pot { get; private set; }
    public int CurrentBet { get; private set; }
    public int MinimumRaise { get; private set; }
    public bool IsHandOver { get; private set; }
    public int WinnerByFoldIndex { get; private set; } = -1;
    public bool BettingRoundComplete => ActingPlayerIndex == -1;
    public bool CanAdvanceStreet => Street != PokerStreet.NotStarted && !IsHandOver && BettingRoundComplete;
    public bool CanSwap => !IsHandOver && (Street == PokerStreet.Turn || Street == PokerStreet.River)
        && streetUntouched && !swappedThisStreet && !seats[ControlledPlayerIndex].Folded && reserved.Count > 0;
    public bool CanRaise => ActingPlayerIndex >= 0 && CountActionable() > 1
        && (long)seats[ActingPlayerIndex].Chips + seats[ActingPlayerIndex].StreetContribution >= (long)CurrentBet + MinimumRaise;
    public bool CanCheckOrCall => ActingPlayerIndex >= 0
        && (!RequiresOpeningBet || Street != PokerStreet.Preflop || CurrentBet > 0);

    public event Action<PokerGameEvent> StateChanged;

    public PokerGameState(int playerCount = 3, int controlledPlayerIndex = 0,
        int startingChips = 1000, int minimumBet = 20, Random random = null, bool requiresOpeningBet = true)
    {
        if (playerCount < 2 || playerCount > 10) throw new ArgumentOutOfRangeException(nameof(playerCount));
        if (controlledPlayerIndex < 0 || controlledPlayerIndex >= playerCount)
            throw new ArgumentOutOfRangeException(nameof(controlledPlayerIndex));
        if (minimumBet <= 0 || startingChips < minimumBet || (long)startingChips * playerCount > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(startingChips), "Stacks must cover the minimum bet and fit the pot counter.");
        this.startingChips = startingChips; this.minimumBet = minimumBet;
        ControlledPlayerIndex = controlledPlayerIndex;
        RequiresOpeningBet = requiresOpeningBet;
        seats = new Seat[playerCount]; pending = new bool[playerCount];
        for (int i = 0; i < seats.Length; i++) seats[i] = new Seat();
        deck = new Deck(random);
    }

    public void StartNewHand()
    {
        deck.Reset(); deck.Shuffle();
        board.Clear(); reserved.Clear(); discards.Clear();
        foreach (Seat seat in seats)
        {
            seat.Cards.Clear(); seat.Chips = startingChips;
            seat.Folded = false; seat.TotalContribution = 0;
        }
        for (int round = 0; round < 2; round++) foreach (Seat seat in seats) seat.Cards.Add(Draw());
        for (int i = 0; i < 2; i++) reserved.Add(Draw());
        HandNumber++; Pot = 0; IsHandOver = false; WinnerByFoldIndex = -1;
        Street = PokerStreet.Preflop;
        BeginBettingRound();
        Publish(PokerGameEventType.HandStarted);
    }

    public void AdvanceStreet()
    {
        if (!CanAdvanceStreet) throw new InvalidOperationException("Finish betting before advancing an active hand.");
        if (Street == PokerStreet.River)
        {
            Street = PokerStreet.Showdown; IsHandOver = true;
            Publish(PokerGameEventType.Showdown);
            return;
        }
        int count = Street == PokerStreet.Preflop ? 3 : 1;
        for (int i = 0; i < count; i++) board.Add(Draw());
        Street = Street == PokerStreet.Preflop ? PokerStreet.Flop
            : Street == PokerStreet.Flop ? PokerStreet.Turn : PokerStreet.River;
        BeginBettingRound();
        Publish(PokerGameEventType.StreetRevealed);
    }

    public int AmountToCall(int playerIndex)
    {
        ValidateIndex(playerIndex);
        return Math.Max(0, CurrentBet - seats[playerIndex].StreetContribution);
    }

    public void CheckOrCall(int playerIndex)
    {
        RequireActor(playerIndex);
        if (!CanCheckOrCall) throw new InvalidOperationException("An opening bet is required preflop before players can check or call.");
        int amount = Math.Min(AmountToCall(playerIndex), seats[playerIndex].Chips);
        CommitChips(playerIndex, amount);
        pending[playerIndex] = false; streetUntouched = false;
        SelectNextActor(playerIndex + 1);
        Publish(amount == 0 ? PokerGameEventType.Checked : PokerGameEventType.Called, playerIndex, amount);
    }

    /// <summary>Target is the player's total contribution on this street, not an increment.</summary>
    public void BetOrRaiseTo(int playerIndex, int target)
    {
        RequireActor(playerIndex);
        if (!CanRaise || (long)target < (long)CurrentBet + MinimumRaise
            || (long)target > (long)seats[playerIndex].Chips + seats[playerIndex].StreetContribution)
            throw new InvalidOperationException("Raise must cover a full minimum raise within the player's stack.");
        int amount = target - seats[playerIndex].StreetContribution;
        MinimumRaise = target - CurrentBet; CurrentBet = target;
        CommitChips(playerIndex, amount);
        for (int i = 0; i < pending.Length; i++) pending[i] = i != playerIndex && CanAct(i);
        streetUntouched = false;
        SelectNextActor(playerIndex + 1);
        Publish(PokerGameEventType.Raised, playerIndex, amount);
    }

    public void Fold(int playerIndex)
    {
        RequireActor(playerIndex);
        seats[playerIndex].Folded = true; pending[playerIndex] = false; streetUntouched = false;
        SelectNextActor(playerIndex + 1);
        Publish(PokerGameEventType.Folded, playerIndex);
    }

    /// <summary>Replace only the newly revealed turn/river, once, before any betting action on it.</summary>
    public void SwapCurrentStreetCard(int reservedIndex)
    {
        if (!CanSwap) throw new InvalidOperationException("Swap once on the fresh turn or river, before betting.");
        if (reservedIndex < 0 || reservedIndex >= reserved.Count) throw new ArgumentOutOfRangeException(nameof(reservedIndex));
        int index = Street == PokerStreet.Turn ? 3 : 4;
        Card replaced = board[index], replacement = reserved[reservedIndex];
        reserved.RemoveAt(reservedIndex); discards.Add(replaced); board[index] = replacement;
        swappedThisStreet = true;
        Publish(PokerGameEventType.CardSwapped, ControlledPlayerIndex, 0, index, reservedIndex, replaced, replacement);
    }

    public PokerGameSnapshot CaptureSnapshot()
    {
        var players = new PokerSeatSnapshot[seats.Length];
        for (int i = 0; i < seats.Length; i++)
        {
            Seat seat = seats[i];
            players[i] = new PokerSeatSnapshot($"Player {i + 1}", seat.Chips, seat.Folded,
                seat.StreetContribution, seat.TotalContribution, seat.Cards);
        }
        return new PokerGameSnapshot
        {
            HandNumber = HandNumber, Version = Version, Street = Street, ActingPlayerIndex = ActingPlayerIndex,
            ControlledPlayerIndex = ControlledPlayerIndex, Pot = Pot, CurrentBet = CurrentBet, MinimumRaise = MinimumRaise,
            DeckCount = deck.CardsRemaining, BettingRoundComplete = BettingRoundComplete, IsHandOver = IsHandOver,
            WinnerByFoldIndex = WinnerByFoldIndex, CanSwap = CanSwap, CanRaise = CanRaise,
            CanCheckOrCall = CanCheckOrCall, RequiresOpeningBet = RequiresOpeningBet,
            Players = Array.AsReadOnly(players), Board = CopyCards(board), ReservedCards = CopyCards(reserved), Discards = CopyCards(discards)
        };
    }

    internal static IReadOnlyList<Card> CopyCards(IReadOnlyList<Card> cards)
    {
        var copy = new Card[cards.Count];
        for (int i = 0; i < cards.Count; i++) copy[i] = new Card(cards[i].suit, cards[i].rank, cards[i].faceUp);
        return Array.AsReadOnly(copy);
    }

    private void BeginBettingRound()
    {
        CurrentBet = 0; MinimumRaise = minimumBet; streetUntouched = true; swappedThisStreet = false;
        for (int i = 0; i < seats.Length; i++) { seats[i].StreetContribution = 0; pending[i] = CanAct(i); }
        SelectNextActor(0);
    }

    private void SelectNextActor(int start)
    {
        int active = 0, last = -1;
        for (int i = 0; i < seats.Length; i++) if (!seats[i].Folded) { active++; last = i; }
        if (active == 1)
        {
            IsHandOver = true; WinnerByFoldIndex = last;
            Array.Clear(pending, 0, pending.Length);
        }
        if (CountActionable() == 1)
            for (int i = 0; i < seats.Length; i++)
                if (CanAct(i) && seats[i].StreetContribution >= CurrentBet) pending[i] = false;
        ActingPlayerIndex = -1;
        for (int offset = 0; offset < seats.Length; offset++)
        {
            int i = (start + offset) % seats.Length;
            if (pending[i] && CanAct(i)) { ActingPlayerIndex = i; break; }
        }
    }

    private int CountActionable()
    {
        int count = 0;
        for (int i = 0; i < seats.Length; i++) if (CanAct(i)) count++;
        return count;
    }
    private bool CanAct(int i) => !seats[i].Folded && seats[i].Chips > 0;
    private Card Draw()
    {
        Card card = deck.Draw() ?? throw new InvalidOperationException("The deck is exhausted.");
        card.faceUp = true; // Authoritative debug data; viewers decide what to reveal.
        return card;
    }
    private void CommitChips(int i, int amount)
    {
        seats[i].Chips -= amount; seats[i].StreetContribution += amount;
        seats[i].TotalContribution += amount; Pot += amount;
    }
    private void ValidateIndex(int i)
    {
        if (i < 0 || i >= seats.Length) throw new ArgumentOutOfRangeException(nameof(i));
    }
    private void RequireActor(int i)
    {
        ValidateIndex(i);
        if (IsHandOver || i != ActingPlayerIndex) throw new InvalidOperationException("This player cannot act now.");
    }
    private void Publish(PokerGameEventType type, int player = -1, int amount = 0,
        int boardIndex = -1, int reservedIndex = -1, Card replaced = null, Card replacement = null)
    {
        Version++;
        StateChanged?.Invoke(new PokerGameEvent(type, HandNumber, Version, Street, player, amount,
            boardIndex, reservedIndex, replaced, replacement));
    }
}
