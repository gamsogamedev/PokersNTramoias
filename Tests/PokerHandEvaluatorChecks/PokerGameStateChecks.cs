static class PokerGameStateChecks
{
    public static int Run()
    {
        int assertions = 0;
        void Check(bool ok, string message)
        {
            if (!ok) throw new Exception(message);
            assertions++;
        }
        void Reject<T>(Action action, string message) where T : Exception
        {
            try { action(); }
            catch (T) { assertions++; return; }
            throw new Exception(message);
        }
        string Id(Card card) => $"{card.suit}:{card.rank}";
        void Invariants(PokerGameState game)
        {
            var s = game.CaptureSnapshot();
            var cards = s.Players.SelectMany(p => p.HoleCards).Concat(s.Board).Concat(s.ReservedCards).Concat(s.Discards).ToArray();
            Check(cards.Length + s.DeckCount == 52, "All 52 cards accounted for");
            Check(cards.Select(Id).Distinct().Count() == cards.Length, "Card locations remain disjoint");
            Check(s.Players.Sum(p => p.Chips) + s.Pot == s.Players.Count * 1000, "Chips conserved");
            Check(s.Players.Sum(p => p.TotalContribution) == s.Pot, "Pot tracks every contribution");
        }
        void CheckThrough(PokerGameState game)
        {
            int actions = 0;
            while (game.ActingPlayerIndex >= 0)
            {
                if (++actions > 10) throw new Exception("Betting did not finish");
                if (!game.CanCheckOrCall) game.BetOrRaiseTo(game.ActingPlayerIndex, game.CurrentBet + game.MinimumRaise);
                else game.CheckOrCall(game.ActingPlayerIndex);
            }
        }
        var game = new PokerGameState(random: new Random(42));
        var events = new List<PokerGameEvent>();
        game.StateChanged += events.Add;
        Reject<InvalidOperationException>(game.AdvanceStreet, "Must start a hand");
        game.StartNewHand();
        var initial = game.CaptureSnapshot();
        Check(initial.Street == PokerStreet.Preflop && initial.Board.Count == 0, "Start preflop");
        Check(initial.ReservedCards.Count == 2 && initial.DeckCount == 44, "Deal six hole cards plus two reserved");
        Check(initial.Players.All(p => p.HoleCards.Count == 2), "Exactly two hole cards per seat");
        Check(initial.RequiresOpeningBet && !initial.CanCheckOrCall && initial.CanRaise, "Default rules require an opening bet");
        int beforeIllegalCheck = game.Version;
        Reject<InvalidOperationException>(() => game.CheckOrCall(0), "Cannot check before opening bet");
        Check(game.Version == beforeIllegalCheck && game.ActingPlayerIndex == 0 && game.Pot == 0,
            "Rejected opening check preserves state and turn");
        Reject<InvalidOperationException>(game.AdvanceStreet, "Cannot skip outstanding preflop actions");
        Reject<InvalidOperationException>(() => game.CheckOrCall(1), "Enforce action order");
        Reject<InvalidOperationException>(() => game.SwapCurrentStreetCard(0), "Cannot swap preflop");
        int version = game.Version;
        Reject<InvalidOperationException>(() => game.BetOrRaiseTo(0, 19), "Minimum bet enforced");
        Reject<InvalidOperationException>(() => game.BetOrRaiseTo(0, 1001), "Cannot overspend");
        Check(game.Version == version && game.Pot == 0, "Invalid actions do not mutate or publish");
        initial.Players[0].HoleCards[0].rank = (Card.Rank)99;
        Check((int)game.CaptureSnapshot().Players[0].HoleCards[0].rank <= 14, "Snapshot cards cannot mutate game");
        CheckThrough(game); game.AdvanceStreet();
        Check(game.Street == PokerStreet.Flop && game.CaptureSnapshot().Board.Count == 3, "Reveal flop together");
        Check(game.CanCheckOrCall, "Flop permits checking without an opening bet");
        Reject<InvalidOperationException>(() => game.SwapCurrentStreetCard(0), "Flop swaps disallowed");
        var flop = game.CaptureSnapshot();
        var hands = flop.Players.Select(p => p.HoleCards).ToArray();
        Check(PokerEquityCalculator.Calculate(hands, flop.Board, flop.ReservedCards).Outcomes == 820, "Reserve blockers reduce flop enumeration");
        CheckThrough(game); game.AdvanceStreet();
        var turn = game.CaptureSnapshot();
        Check(turn.CanCheckOrCall, "Turn permits checking without an opening bet");
        Check(turn.CanSwap && turn.Board.Count == 4, "Fresh turn allows swap");
        Reject<ArgumentOutOfRangeException>(() => game.SwapCurrentStreetCard(2), "Invalid reserve index rejected");
        game.SwapCurrentStreetCard(0);
        var swapped = game.CaptureSnapshot();
        Check(Id(swapped.Board[3]) == Id(turn.ReservedCards[0]), "Reserved card becomes turn");
        Check(Id(swapped.Discards[0]) == Id(turn.Board[3]), "Old turn is discarded");
        Check(swapped.ReservedCards.Count == 1 && swapped.DeckCount == turn.DeckCount, "Swap consumes reserve without drawing");
        Check(events.Last().Type == PokerGameEventType.CardSwapped && events.Last().BoardIndex == 3
            && Id(events.Last().ReplacedCard) == Id(turn.Board[3]), "Explicit swap event carries identities");
        events.Last().ReplacementCard.rank = (Card.Rank)99;
        Check((int)game.CaptureSnapshot().Board[3].rank <= 14, "Event cannot mutate game");
        Reject<InvalidOperationException>(() => game.SwapCurrentStreetCard(0), "Only one swap per street");
        CheckThrough(game); game.AdvanceStreet();
        Check(game.CanCheckOrCall, "River permits checking without an opening bet");
        game.SwapCurrentStreetCard(0);
        Check(game.CaptureSnapshot().ReservedCards.Count == 0 && game.CaptureSnapshot().Discards.Count == 2, "Consume second reserve on river");
        Invariants(game);
        CheckThrough(game); game.AdvanceStreet();
        Check(game.IsHandOver && game.Street == PokerStreet.Showdown, "River betting finishes at showdown");
        Reject<InvalidOperationException>(game.AdvanceStreet, "No sixth community card");
        Check(events.Select(e => e.Version).SequenceEqual(Enumerable.Range(1, events.Count)), "Monotonic event versions");

        game.StartNewHand();
        Check(game.HandNumber == 2 && game.CaptureSnapshot().Discards.Count == 0, "New hand clears discards");
        game.BetOrRaiseTo(0, 20);
        Check(game.ActingPlayerIndex == 1 && game.AmountToCall(1) == 20, "Raise creates a call amount");
        game.CheckOrCall(1);
        game.BetOrRaiseTo(2, 60);
        Check(game.ActingPlayerIndex == 0 && game.MinimumRaise == 40, "Full raise reopens action");
        Reject<InvalidOperationException>(() => game.BetOrRaiseTo(0, 80), "Minimum re-raise enforced");
        game.CheckOrCall(0); game.Fold(1);
        Check(game.BettingRoundComplete && game.Pot == 140, "Call/raise/fold pot accounting");
        var betting = game.CaptureSnapshot();
        Check(betting.Players[0].Chips == 940 && betting.Players[1].Chips == 980 && betting.Players[2].Chips == 940, "Chip balances");
        game.AdvanceStreet();
        var newStreet = game.CaptureSnapshot();
        Check(newStreet.CurrentBet == 0 && newStreet.Players.All(p => p.StreetContribution == 0) && newStreet.Pot == 140,
            "Street resets bets but preserves pot");
        Check(newStreet.Players[1].HasFolded && game.ActingPlayerIndex == 0, "Folded seats skipped");
        Reject<InvalidOperationException>(() => game.CheckOrCall(1), "Folded player cannot act");
        game.Fold(0);
        Check(game.IsHandOver && game.WinnerByFoldIndex == 2 && game.ActingPlayerIndex == -1, "Last active player wins by folds");
        Reject<InvalidOperationException>(game.AdvanceStreet, "Fold win ends hand immediately");
        Invariants(game);

        var allIn = new PokerGameState(startingChips: 20, random: new Random(8));
        allIn.StartNewHand(); allIn.BetOrRaiseTo(0, 20); allIn.CheckOrCall(1); allIn.CheckOrCall(2);
        Check(allIn.BettingRoundComplete && allIn.CaptureSnapshot().Players.All(p => p.IsAllIn), "All-in seats need no more actions");
        while (allIn.CanAdvanceStreet) allIn.AdvanceStreet();
        Check(allIn.Street == PokerStreet.Showdown && allIn.Pot == 60, "All-in runout reaches showdown");

        // A check does not remove the obligation to respond to a later opening bet/raise.
        var reopened = new PokerGameState(random: new Random(17));
        reopened.StartNewHand();
        CheckThrough(reopened);
        reopened.AdvanceStreet();
        int potBeforeChecks = reopened.Pot;
        int chipsBeforeChecks = reopened.CaptureSnapshot().Players[0].Chips;
        reopened.CheckOrCall(0);
        reopened.CheckOrCall(1);
        Check(reopened.Pot == potBeforeChecks && reopened.CurrentBet == 0, "Postflop checks cost no chips");
        reopened.BetOrRaiseTo(2, 20);
        Check(reopened.ActingPlayerIndex == 0 && reopened.AmountToCall(0) == 20,
            "Opening bet returns action to the first checker");
        Reject<InvalidOperationException>(reopened.AdvanceStreet, "Cannot reveal while earlier checkers owe responses");
        reopened.CheckOrCall(0);
        Check(reopened.CaptureSnapshot().Players[0].Chips == chipsBeforeChecks - 20 && reopened.ActingPlayerIndex == 1,
            "Returning checker pays the bet and action reaches the second checker");
        reopened.BetOrRaiseTo(1, 40);
        Check(reopened.ActingPlayerIndex == 2 && reopened.AmountToCall(2) == 20,
            "Re-raise returns action to the original bettor");
        reopened.CheckOrCall(2);
        Check(reopened.ActingPlayerIndex == 0 && reopened.AmountToCall(0) == 20,
            "Re-raise also returns action to the earlier caller");
        reopened.Fold(0);
        Check(reopened.BettingRoundComplete && reopened.Pot == potBeforeChecks + 100,
            "Round closes only after every remaining participant responds");

        var deals = new HashSet<string>();
        for (int hand = 0; hand < 100; hand++)
        {
            game.StartNewHand();
            deals.Add(string.Join(" ", game.CaptureSnapshot().Players.SelectMany(p => p.HoleCards).Select(Id)));
            while (!game.IsHandOver)
            {
                Invariants(game);
                if (game.CanSwap)
                {
                    game.SwapCurrentStreetCard(0);
                    Invariants(game);
                }
                CheckThrough(game);
                game.AdvanceStreet();
            }
            Invariants(game);
        }
        Check(deals.Count == 100, "Fresh hands reshuffle");
        game.StartNewHand(); CheckThrough(game); game.AdvanceStreet(); CheckThrough(game); game.AdvanceStreet();
        game.BetOrRaiseTo(0, 20);
        Reject<InvalidOperationException>(() => game.SwapCurrentStreetCard(0), "Betting closes swap window");

        var deck = new Deck(new Random(7));
        Reject<ArgumentException>(() => deck.InsertAt(new Card(Card.Suit.Copas, Card.Rank.As), 0), "Deck rejects physical duplicates");
        deck.Shuffle();
        var drawn = new HashSet<string>();
        for (int i = 0; i < 52; i++) drawn.Add(Id(deck.Draw()));
        Check(drawn.Count == 52 && deck.Draw() == null, "Deck draws 52 distinct cards");
        Reject<ArgumentOutOfRangeException>(() => new PokerGameState(1), "Invalid seat count");
        Reject<ArgumentOutOfRangeException>(() => new PokerGameState(controlledPlayerIndex: 3), "Invalid controlled seat");
        Reject<ArgumentOutOfRangeException>(() => new PokerGameState(startingChips: int.MaxValue), "Pot overflow guarded");
        return assertions;
    }
}
