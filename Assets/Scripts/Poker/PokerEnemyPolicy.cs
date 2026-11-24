using System;
using System.Collections.Generic;

// Only the acting seat's cards, visible board, and public betting information cross this boundary.
internal sealed class EnemyObservation
{
    internal int Seat, Match, Hand, LiveMask, Opponents, StreetRaises;
    internal int Pot, EligiblePot, Chips, Committed, StreetContribution, Call, CurrentBet, MinimumTarget;
    internal bool CanCall, FacingAllIn;
    internal double Position;
    internal ulong Hole, Board;

    internal static ulong CardMask(IReadOnlyList<Card> cards)
    {
        ulong mask = 0;
        foreach (Card card in cards) mask |= 1UL << ((int)card.suit * 13 + (int)card.rank - 2);
        return mask;
    }

    internal bool SameEquityInputs(EnemyObservation other) =>
        Hand == other.Hand && Hole == other.Hole && Board == other.Board && LiveMask == other.LiveMask;
}

internal sealed class EnemyEquitySampler
{
    internal int Samples { get; private set; }
    internal double Equity => share / Samples;

    private readonly Random random;
    private readonly Card[] unseen;
    private readonly Card[] seven = new Card[7];
    private readonly Card[] hero = new Card[2];
    private readonly int[] rankCounts = new int[15];
    private readonly int boardCount, opponents;
    private double share;

    internal EnemyEquitySampler(EnemyObservation observation, Random random)
    {
        this.random = random;
        opponents = observation.Opponents;
        var pool = new List<Card>(50);
        int holeCount = 0;
        for (int id = 0; id < 52; id++)
        {
            var card = new Card((Card.Suit)(id / 13), (Card.Rank)(id % 13 + 2));
            ulong bit = 1UL << id;
            if ((observation.Hole & bit) != 0) hero[holeCount++] = card;
            else if ((observation.Board & bit) != 0) seven[boardCount++] = card;
            else pool.Add(card);
        }
        unseen = pool.ToArray();
    }

    // Partial Fisher-Yates: each new draw is uniform among the remaining possibilities,
    // regardless of the permutation left by the previous outcome.
    private Card Draw(ref int drawn)
    {
        int index = random.Next(drawn, unseen.Length);
        Card card = unseen[index];
        unseen[index] = unseen[drawn];
        unseen[drawn++] = card;
        return card;
    }

    internal void Sample()
    {
        int drawn = 0;
        for (int i = boardCount; i < 5; i++) seven[i] = Draw(ref drawn);
        seven[5] = hero[0];
        seven[6] = hero[1];
        var value = PokerHandEvaluator.EvaluateDistinct(seven, rankCounts);
        int winners = 1;
        for (int i = 0; i < opponents; i++)
        {
            seven[5] = Draw(ref drawn);
            seven[6] = Draw(ref drawn);
            int comparison = PokerHandEvaluator.EvaluateDistinct(seven, rankCounts).CompareTo(value);
            if (comparison > 0) { winners = 0; break; }
            if (comparison == 0) winners++;
        }
        if (winners > 0) share += 1.0 / winners;
        Samples++;
    }
}

internal enum EnemyAction { Fold, CheckOrCall, Raise }

internal readonly struct EnemyDecision
{
    internal readonly EnemyAction Action;
    internal readonly int Target;
    internal EnemyDecision(EnemyAction action, int target = 0) { Action = action; Target = target; }
}

internal static class PokerEnemyPolicy
{
    internal static int HandBudget(EnemyObservation view, double equity, double risk)
    {
        // Scale around the equal share of a multiway pot; premium hands can commit the stack.
        double fraction = Math.Min(1, equity * equity * (1 + view.Opponents) * risk);
        return (int)((view.Chips + view.Committed) * fraction);
    }

    internal static EnemyDecision Choose(EnemyObservation view, double equity, double risk,
        double aggression, Random random)
    {
        int remaining = Math.Max(0, HandBudget(view, equity, risk) - view.Committed);
        int maxTarget = view.StreetContribution + remaining;
        bool canRaise = view.MinimumTarget > 0 && maxTarget >= view.MinimumTarget;
        bool canCall = view.CanCall && view.Call <= remaining;
        bool freeCheck = canCall && view.Call == 0;

        double odds = view.Call == 0 ? 0 : (double)view.Call / (view.EligiblePot + view.Call);
        double edge = equity - odds;
        double pressure = 1 + 0.4 * view.StreetRaises;
        double foldChance = view.FacingAllIn ? 0 : (0.2 + 0.2 * view.Position) / (view.Opponents * pressure);
        double foldWeight = freeCheck ? 0 : Math.Max(0.015, 0.3 - 1.5 * edge);
        double callWeight = canCall ? Math.Max(0.1, 0.8 + edge) : 0;
        double raiseWeight = canRaise ? aggression * (3 * equity * equity + foldChance)
            * (0.8 + 0.4 * view.Position) / pressure : 0;

        double roll = random.NextDouble() * (foldWeight + callWeight + raiseWeight);
        if (roll < foldWeight) return new EnemyDecision(EnemyAction.Fold);
        if (roll < foldWeight + callWeight) return new EnemyDecision(EnemyAction.CheckOrCall);

        // Only distinct legal sizes get a draw. All targets are street totals.
        long potAfterCall = (long)view.Pot + view.Call;
        int half = (int)Math.Min(maxTarget, Math.Max(view.MinimumTarget, view.CurrentBet + potAfterCall / 2));
        int full = (int)Math.Min(maxTarget, Math.Max(view.MinimumTarget, view.CurrentBet + potAfterCall));
        int choices = 1 + (half > view.MinimumTarget ? 1 : 0) + (full > half ? 1 : 0);
        int size = random.Next(choices);
        int target = size == 0 ? view.MinimumTarget : size == 1 && half > view.MinimumTarget ? half : full;
        return new EnemyDecision(EnemyAction.Raise, target);
    }
}
