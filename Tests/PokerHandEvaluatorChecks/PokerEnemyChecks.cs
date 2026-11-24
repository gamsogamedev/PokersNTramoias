using System.Diagnostics;

internal static class PokerEnemyChecks
{
    // Keep the actor's two cards fixed. Shuffle the opponent cards, reserves and future board.
    private sealed class HiddenShuffle : Random
    {
        private readonly int[] permutation;
        internal HiddenShuffle(int seed)
        {
            permutation = Enumerable.Range(0, 52).ToArray();
            var random = new Random(seed);
            for (int i = 51; i > 0; i--)
            {
                if (i == 3) continue;
                int j;
                do { j = random.Next(1, i + 1); } while (j == 3);
                permutation[i] = j;
            }
        }
        public override int Next(int maxValue) => permutation[maxValue - 1];
    }

    internal static int Run()
    {
        int checks = 0;
        void Check(bool pass, string message)
        {
            if (!pass) throw new Exception(message);
            checks++;
        }
        PokerGameState Deal(int seed)
        {
            var game = new PokerGameState(3, 1, random: new HiddenShuffle(seed));
            game.StartNewHand();
            return game;
        }
        double Sample(EnemyObservation view, int n)
        {
            var sampler = new EnemyEquitySampler(view, new Random(392));
            for (int i = 0; i < n; i++) sampler.Sample();
            return sampler.Equity;
        }

        var first = Deal(1);
        var second = Deal(2);
        var a = first.ObserveEnemyTurn();
        var b = second.ObserveEnemyTurn();
        Check(a.SameEquityInputs(b), "Secret shuffle changed observable cards");
        Check(EnemyObservation.CardMask(first.CaptureSnapshot().ReservedCards) !=
            EnemyObservation.CardMask(second.CaptureSnapshot().ReservedCards), "Invalid hidden-card fixture");
        double equity = Sample(a, 500);
        Check(equity == Sample(b, 500), "Hidden information influenced equity");
        var d1 = PokerEnemyPolicy.Choose(a, equity, 1, 1, new Random(9));
        var d2 = PokerEnemyPolicy.Choose(b, equity, 1, 1, new Random(9));
        Check(d1.Action == d2.Action && d1.Target == d2.Target, "Hidden information influenced decision");

        first.BetOrRaiseTo(0, 1000);
        first.CheckOrCall(1);
        var allIn = first.ObserveEnemyTurn();
        Check(allIn.Opponents == 2 && allIn.FacingAllIn, "All-in seats must remain in equity");
        allIn.Board = 31UL << 47; // Royal flush on the board; hero holds two hearts.
        Check(Math.Abs(Sample(allIn, 100) - 1.0 / 3) < 1e-12, "Three-way board tie");

        var view = Deal(5).ObserveEnemyTurn();
        Check(PokerEnemyPolicy.Choose(view, 0, 1, 1, new Random(1)).Action == EnemyAction.Fold,
            "Mandatory opening over budget");
        view.CanCall = true;
        view.Chips = 860; view.Committed = 140; view.StreetContribution = 20;
        view.Pot = 400; view.Call = 30; view.CurrentBet = 50; view.MinimumTarget = 80; view.EligiblePot = 400;
        Check(PokerEnemyPolicy.HandBudget(view, 0.2, 1) < 140 && PokerEnemyPolicy.HandBudget(view, 0.5, 1) > 140,
            "Changing equity updates total hand commitment");
        int weakRaises = 0, strongRaises = 0;
        var sizes = new HashSet<int>();
        for (int i = 0; i < 600; i++)
        {
            foreach (double strength in new[] { 0.28, 0.85 })
            {
                var action = PokerEnemyPolicy.Choose(view, strength, 1, 1, new Random(i));
                int paid = action.Action == EnemyAction.Raise ? action.Target - view.StreetContribution
                    : action.Action == EnemyAction.CheckOrCall ? view.Call : 0;
                Check(paid <= view.Chips && paid + view.Committed <= PokerEnemyPolicy.HandBudget(view, strength, 1),
                    "Decision exceeded total hand budget");
                if (action.Action != EnemyAction.Raise) continue;
                Check(action.Target >= view.MinimumTarget, "Illegal raise");
                if (strength < 0.5) weakRaises++;
                else { strongRaises++; sizes.Add(action.Target); }
            }
        }
        Check(weakRaises > 0 && strongRaises > weakRaises && strongRaises < 600 && sizes.Count == 3,
            "Seed sweep should include bluffs, slow plays and varied sizes");
        view.Call = 0;
        Check(PokerEnemyPolicy.Choose(view, 0.1, 1, 1, new Random(10)).Action == EnemyAction.CheckOrCall,
            "Check when budget has fallen below prior commitment");
        view.Chips = view.Call = 5; view.MinimumTarget = 0;
        Check(PokerEnemyPolicy.Choose(view, 1, 1, 1, new Random(123)).Action == EnemyAction.CheckOrCall,
            "Affordable short all-in call");

        var clockGame = Deal(3);
        var turns = new PokerEnemyTurns(new Random(44));
        int version = clockGame.Version;
        turns.Tick(clockGame, 0);
        turns.Tick(clockGame, 1.999);
        Check(clockGame.Version == version, "Early enemy action");
        turns.Cancel();
        turns.Tick(clockGame, 4.99);
        turns.Tick(clockGame, 5.1);
        Check(clockGame.Version == version, "Cancelled delay leaked");
        turns.Tick(clockGame, 10);
        Check(clockGame.Version == version + 1 && clockGame.ActingPlayerIndex == 1, "One action by deadline");
        turns.Tick(clockGame, 20);
        Check(clockGame.Version == version + 1, "AI acted on human turn");

        var swapped = Deal(4);
        swapped.BetOrRaiseTo(0, 20);
        while (swapped.ActingPlayerIndex >= 0) swapped.CheckOrCall(swapped.ActingPlayerIndex);
        swapped.AdvanceStreet();
        while (swapped.ActingPlayerIndex >= 0) swapped.CheckOrCall(swapped.ActingPlayerIndex);
        swapped.AdvanceStreet();
        var before = swapped.ObserveEnemyTurn();
        turns.Tick(swapped, 30);
        swapped.SwapCurrentStreetCard(0);
        Check(!before.SameEquityInputs(swapped.ObserveEnemyTurn()), "Swap must invalidate estimate");
        version = swapped.Version;
        turns.Tick(swapped, 35);
        turns.Tick(swapped, 36.99);
        Check(swapped.Version == version, "Stale action after swap");
        turns.Tick(swapped, 40);
        Check(swapped.Version == version + 1, "No replacement action after swap");

        // A pending decision for hand one must also expire when a match restarts at hand one.
        var restart = new PokerGameState(2, 1, 20, 20, new Random(15));
        restart.StartNewHand();
        turns.Tick(restart, 50);
        restart.BetOrRaiseTo(0, 20);
        restart.CheckOrCall(1);
        while (!restart.IsHandOver) restart.AdvanceStreet();
        Check(restart.MatchOutcome != PokerMatchOutcome.InProgress, "All-in restart fixture");
        restart.StartNewMatch();
        version = restart.Version;
        turns.Tick(restart, 55);
        turns.Tick(restart, 56.99);
        Check(restart.Version == version, "Stale decision crossed match restart");
        turns.Tick(restart, 60);
        Check(restart.Version == version + 1, "Missing decision in new match");

        // Decisions go through the same betting API as player actions, including reopened betting.
        var series = new PokerGameState(5, 2, 100, 5, new Random(7));
        turns = new PokerEnemyTurns(new Random(17));
        for (int hand = 0; hand < 24; hand++)
        {
            if (series.MatchOutcome == PokerMatchOutcome.InProgress) series.StartNewHand();
            else series.StartNewMatch();
            int steps = 0;
            while (!series.IsHandOver && ++steps < 500)
            {
                if (series.CanAdvanceStreet) series.AdvanceStreet();
                else if (series.ActingPlayerIndex == series.ControlledPlayerIndex)
                {
                    if (series.CanCheckOrCall) series.CheckOrCall(series.ControlledPlayerIndex);
                    else series.BetOrRaiseTo(series.ControlledPlayerIndex, series.MinimumRaiseTarget);
                }
                else
                {
                    int oldVersion = series.Version;
                    turns.Tick(series, oldVersion * 10.0);
                    turns.Tick(series, oldVersion * 10.0 + 5);
                    Check(series.Version == oldVersion + 1, "Expected one legal enemy action per turn");
                }
            }
            Check(series.IsHandOver && series.CaptureSnapshot().Players.Sum(seat => seat.Chips) == 500,
                "AI hand settlement and chip conservation");
        }

        foreach (int seats in new[] { 3, 10 })
        {
            var table = new PokerGameState(seats, 1, random: new Random(24));
            table.StartNewHand();
            var timer = Stopwatch.StartNew();
            double share = Sample(table.ObserveEnemyTurn(), 10000);
            Check(share >= 0 && share <= 1, "Invalid sampled share");
            Console.WriteLine($"Enemy equity: {seats} seats, 10000 preflop outcomes in {timer.ElapsedMilliseconds}ms.");
        }
        return checks;
    }
}
