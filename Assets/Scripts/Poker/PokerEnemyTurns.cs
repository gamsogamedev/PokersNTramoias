using System;
using System.Collections.Generic;
using System.Diagnostics;

/// <summary>Frame-driven controller shared by the game and evaluator. Only Tick submits actions.</summary>
internal sealed class PokerEnemyTurns
{
    private sealed class Personality
    {
        internal double Aggression, Risk, HandRisk, Equity;
        internal EnemyObservation LastDecision;
    }

    private readonly Random random;
    private readonly Dictionary<int, Personality> profiles = new Dictionary<int, Personality>();
    private PokerGameState activeGame;
    private int match, version;
    private EnemyObservation turn;
    private EnemyEquitySampler sampler;
    private Personality profile;
    private double deadline;
    private const int TargetSamples = 10000;

    internal PokerEnemyTurns(Random random = null) { this.random = random ?? new Random(); }

    internal void Cancel()
    {
        turn = null;
        sampler = null;
    }

    internal void Tick(PokerGameState game, double now)
    {
        if (game == null || game.ActingPlayerIndex < 0 || game.ActingPlayerIndex == game.ControlledPlayerIndex)
        {
            Cancel();
            return;
        }
        if (turn == null || game != activeGame || version != game.Version || turn.Seat != game.ActingPlayerIndex)
        {
            EnemyObservation next = game.ObserveEnemyTurn();
            if (game != activeGame || match != next.Match)
            {
                profiles.Clear();
                activeGame = game;
                match = next.Match;
            }
            if (!profiles.TryGetValue(next.Seat, out profile))
            {
                profile = new Personality { Aggression = 0.7 + 0.6 * random.NextDouble(),
                    Risk = 0.85 + 0.3 * random.NextDouble() };
                profiles.Add(next.Seat, profile);
            }
            if (profile.LastDecision == null || profile.LastDecision.Hand != next.Hand)
            {
                profile.HandRisk = profile.Risk * (0.9 + 0.2 * random.NextDouble());
                profile.LastDecision = next;
                profile.Equity = -1;
            }
            sampler = profile.Equity >= 0 && next.SameEquityInputs(profile.LastDecision) ? null
                : new EnemyEquitySampler(next, new Random(random.Next()));
            turn = next;
            version = game.Version;
            deadline = now + 2 + 3 * random.NextDouble();
        }

        // Bound main-thread work per frame, including on platforms without worker threads.
        if (sampler != null && sampler.Samples < TargetSamples && (now < deadline || sampler.Samples == 0))
        {
            long stop = Stopwatch.GetTimestamp() + Stopwatch.Frequency / 1000;
            do { sampler.Sample(); }
            while (sampler.Samples < TargetSamples && Stopwatch.GetTimestamp() < stop);
        }
        if (now < deadline) return;

        if (sampler != null) profile.Equity = sampler.Equity;
        profile.LastDecision = turn;
        var action = PokerEnemyPolicy.Choose(turn, profile.Equity, profile.HandRisk, profile.Aggression, random);
        int seat = turn.Seat;
        Cancel();
        switch (action.Action)
        {
            case EnemyAction.CheckOrCall: game.CheckOrCall(seat); break;
            case EnemyAction.Raise: game.BetOrRaiseTo(seat, action.Target); break;
            default: game.Fold(seat); break;
        }
    }
}
