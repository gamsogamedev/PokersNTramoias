static class PokerMatchChecks
{
    // Select Fisher-Yates moves for a specified deal, using the production deck.
    private sealed class Deal : Random
    {
        readonly int[] moves = new int[53];
        public Deal(string holes, string board)
        {
            int Id(string card) => "hdcs".IndexOf(card[1]) * 13 + "23456789TJQKA".IndexOf(card[0]);
            var hands = holes.Split(',').Select(h => h.Split(' ').Select(Id).ToArray()).ToArray();
            var drawn = hands.Select(h => h[0]).Concat(hands.Select(h => h[1])).ToList();
            var community = board.Split(' ').Select(Id).ToArray();
            var rest = Enumerable.Range(0, 52).Except(drawn).Except(community).ToArray();
            var order = drawn.Concat(rest.Take(2)).Concat(community).Concat(rest.Skip(2)).ToArray();
            var deck = Enumerable.Range(0, 52).ToArray();
            for (int i = 51; i > 0; i--)
            {
                int j = Array.IndexOf(deck, order[i]);
                moves[i + 1] = j;
                (deck[i], deck[j]) = (deck[j], deck[i]);
            }
        }
        public override int Next(int maxValue) => moves[maxValue];
    }

    public static int Run()
    {
        int checks = 0;
        void Check(bool ok, string msg) { if (!ok) throw new Exception(msg); checks++; }
        PokerGameState Game(string hands, string board, int stack = 20, int minimum = 1, int controlled = 0)
        {
            var g = new PokerGameState(hands.Split(',').Length, controlled, stack, minimum,
                new Deal(hands, board), requiresOpeningBet: false);
            g.StartNewHand();
            return g;
        }
        void Advance(PokerGameState g)
        {
            while (g.ActingPlayerIndex >= 0) g.CheckOrCall(g.ActingPlayerIndex);
            g.AdvanceStreet();
        }
        void Finish(PokerGameState g) { while (!g.IsHandOver) Advance(g); }
        foreach (var (hands, board, expected) in new[] {
            ("Qc Kc,As Ad", "Ac 9c 7c 2s 3d", 5),
            ("7h 2d,2s 3d", "3h 4d 5s 6c Qd", 6),
            ("Ah 3d,Kh 4c", "2h 5h 8h Jh 9d", 7),
            ("2s 9s,9c 9h", "2h 2c 2d 8c Kd", 8),
            ("Ah 7h,Kh 4d", "2h 2s 2c 2d 8c", 9),
            ("7h 8h,Kh 4d", "2h 3h 4h 5h 6h", 10)
        })
        {
            var g = Game(hands, board);
            g.BetOrRaiseTo(0, 2);
            int events = 0;
            g.StateChanged += change => {
                if (change.Type != PokerGameEventType.Showdown) return;
                var s = g.CaptureSnapshot();
                Check(s.Payouts.SequenceEqual(new[] {4, 0}) && s.Suspicion == expected, $"Settled cooler {expected}");
                events++;
            };
            Finish(g);
            Check(events == 1, "One settled showdown notification");
        }
        var multi = Game("As Kd,Qs Jd,Ts 7d,9s 9d", "2c 2h 2d 8s 3h");
        multi.BetOrRaiseTo(0, 2);
        Finish(multi);
        Check(multi.Suspicion == 10 && multi.CaptureSnapshot().Payouts[3] == 8, "Two enemy trips lose to another enemy");

        var split = Game("As 2h,Ad 2s,Kh 9s", "3d 3h 3s Qd 8h");
        split.BetOrRaiseTo(0, 3); Advance(split);
        split.CheckOrCall(0); split.CheckOrCall(1); split.Fold(2);
        Finish(split);
        Check(split.Suspicion == 0 && split.CaptureSnapshot().Payouts.SequenceEqual(new[] {5,4,0}), "Tied trips split odd folded chips");

        var side = Game("9h 9s,As Kd,Qd Jh", "2c 2h 2d 8s 9d", minimum: 5);
        side.BetOrRaiseTo(0, 5); side.Fold(1); side.BetOrRaiseTo(2, 10); side.Fold(0);
        side.StartNewHand();
        side.BetOrRaiseTo(0, 15); Advance(side);
        side.CheckOrCall(1); side.BetOrRaiseTo(2, 10); side.CheckOrCall(1);
        Finish(side);
        Check(side.CaptureSnapshot().Payouts.SequenceEqual(new[] {45,10,5}) && side.Suspicion == 5,
            "Side winner exempt and uncalled refund still scores cooler");
        side.StartNewHand();
        Check(side.CaptureSnapshot().Players.Select(p => p.Chips).SequenceEqual(new[] {45,10,5}), "Settled stacks persist");

        var eliminated = Game("As 2d,Ks 3d,Qs 4d", "2c 2h 8d 9s Jh");
        eliminated.BetOrRaiseTo(0, 20); eliminated.CheckOrCall(1); eliminated.Fold(2); Finish(eliminated);
        eliminated.StartNewHand();
        var next = eliminated.CaptureSnapshot();
        Check(next.Players[1].IsEliminated && next.Players[1].HoleCards.Count == 0 && next.DeckCount == 46, "Skip bankrupt deal");
        eliminated.BetOrRaiseTo(0, 5);
        Check(eliminated.ActingPlayerIndex == 2, "Stable actor indices across elimination");

        foreach (bool collision in new[] {false, true})
        {
            var g = Game("Ah 7h,Kh 4d", "2h 2s 2c 2d 8c");
            for (int h = 1; h <= 13; h++)
            {
                Advance(g); Advance(g); g.SwapCurrentStreetCard(0);
                Check(g.Suspicion == h * (h + 1) / 2 && g.SuccessfulSwapCount == h, "Sequence across hands");
                g.Fold(0); g.StartNewHand();
            }
            if (collision)
            {
                g.BetOrRaiseTo(0, 20); Finish(g);
                Check(g.MatchOutcome == PokerMatchOutcome.PlayerWon && g.Suspicion == 100, "Bankruptcy win precedes cooler threshold");
            }
            else
            {
                Advance(g); Advance(g); g.SwapCurrentStreetCard(0);
                Check(g.MatchOutcome == PokerMatchOutcome.Caught && g.Suspicion == 100 && !g.CanAdvanceStreet
                    && !g.CanSwap && g.ActingPlayerIndex == -1, "Fourteenth swap ends immediately");
            }
            g.StartNewMatch();
            Check(g.HandNumber == 1 && g.Suspicion == 0 && g.SuccessfulSwapCount == 0
                && g.CaptureSnapshot().Players.All(p => p.Chips == 20 && !p.IsEliminated), "Restart clears match state");
        }
        var caughtAtShowdown = Game("Jh 3d,Kh 4d,Ah 7h", "2h 2s 2c 2d 8c");
        for (int h = 1; h <= 13; h++)
        {
            Advance(caughtAtShowdown); Advance(caughtAtShowdown);
            caughtAtShowdown.SwapCurrentStreetCard(0);
            caughtAtShowdown.Fold(0); caughtAtShowdown.Fold(1);
            caughtAtShowdown.StartNewHand();
        }
        caughtAtShowdown.BetOrRaiseTo(0, 20);
        Finish(caughtAtShowdown);
        var caught = caughtAtShowdown.CaptureSnapshot();
        Check(caught.MatchOutcome == PokerMatchOutcome.Caught && caught.Suspicion == 100
            && caught.Players[0].IsEliminated && caught.Payouts[2] == 60,
            "Showdown settles before caught takes priority over controlled-seat bankruptcy");

        var shortStack = Game("Kh 3s,Ah 3d", "2h 2c 2d 2s 8c", stack: 3, minimum: 20);
        Check(shortStack.CanRaise && shortStack.MinimumRaiseTarget == 3, "Short opening all-in available");
        shortStack.BetOrRaiseTo(0, 3); Finish(shortStack);
        Check(shortStack.MatchOutcome == PokerMatchOutcome.PlayerBankrupt, "Controlled seat bankrupt");
        return checks;
    }
}
