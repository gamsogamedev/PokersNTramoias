using System;
using System.Collections.Generic;

/// <summary>Showdown probabilities for one player. Equity includes their share of split pots.</summary>
public readonly struct PokerPlayerEquity
{
    public long Wins { get; }
    public long Ties { get; }
    public double WinProbability { get; }
    public double TieProbability { get; }
    public double Equity { get; }

    internal PokerPlayerEquity(long wins, long ties, double shares, long outcomes)
    {
        Wins = wins;
        Ties = ties;
        WinProbability = (double)wins / outcomes;
        TieProbability = (double)ties / outcomes;
        Equity = shares / outcomes;
    }
}

/// <summary>Exact results in the same order as the input hands. Probabilities range from zero to one.</summary>
public sealed class PokerEquityResult
{
    public long Outcomes { get; }
    public IReadOnlyList<PokerPlayerEquity> Players { get; }

    internal PokerEquityResult(long outcomes, PokerPlayerEquity[] players)
    {
        Outcomes = outcomes;
        Players = Array.AsReadOnly(players);
    }
}

/// <summary>
/// Exact Hold'em showdown equity for known hands on the flop, turn, or river.
/// Enumerates possible board completions uniformly, without consulting the shuffled deck.
/// Assumes no future folds or optional swaps. Does not model betting or side pots.
/// </summary>
public static class PokerEquityCalculator
{
    /// <param name="hands">Two known hole cards per player, in result order.</param>
    /// <param name="board">Three to five community cards.</param>
    /// <param name="deadCards">Known unavailable cards, including reserved swap cards or discards.</param>
    public static PokerEquityResult Calculate(IReadOnlyList<IReadOnlyList<Card>> hands,
        IReadOnlyList<Card> board, IReadOnlyList<Card> deadCards = null)
    {
        if (hands == null) throw new ArgumentNullException(nameof(hands));
        if (board == null) throw new ArgumentNullException(nameof(board));
        if (hands.Count < 2 || hands.Count > 10)
            throw new ArgumentException("Provide two to ten player hands.", nameof(hands));
        if (board.Count < 3 || board.Count > 5)
            throw new ArgumentException("Provide a flop, turn, or river board.", nameof(board));

        var seen = new bool[52];
        var completedHands = new Card[hands.Count][];
        for (int i = 0; i < hands.Count; i++)
        {
            if (hands[i] == null || hands[i].Count != 2)
                throw new ArgumentException("Each player needs exactly two hole cards.", nameof(hands));
            completedHands[i] = new Card[7];
            for (int j = 0; j < 2; j++) completedHands[i][j] = CopyKnownCard(hands[i][j], seen);
        }
        for (int i = 0; i < board.Count; i++)
        {
            Card card = CopyKnownCard(board[i], seen);
            foreach (Card[] hand in completedHands) hand[i + 2] = card;
        }
        if (deadCards != null)
            for (int i = 0; i < deadCards.Count; i++) CopyKnownCard(deadCards[i], seen);

        var remaining = new List<Card>();
        for (int rank = 2; rank <= 14; rank++)
        for (int suit = 0; suit < 4; suit++)
            if (!seen[(rank - 2) * 4 + suit])
                remaining.Add(new Card((Card.Suit)suit, (Card.Rank)rank));
        int missing = 5 - board.Count;
        if (remaining.Count < missing)
            throw new ArgumentException("Not enough available cards to complete the board.");

        var wins = new long[hands.Count];
        var ties = new long[hands.Count];
        var shares = new double[hands.Count];
        var values = new PokerHandValue[hands.Count];
        long outcomes = 0;

        void RecordOutcome()
        {
            PokerHandValue best = default;
            int winners = 0;
            for (int i = 0; i < completedHands.Length; i++)
            {
                values[i] = PokerHandEvaluator.Evaluate(completedHands[i]);
                if (i == 0 || values[i] > best) { best = values[i]; winners = 1; }
                else if (values[i] == best) winners++;
            }
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] != best) continue;
                if (winners == 1) wins[i]++;
                else ties[i]++;
                shares[i] += 1.0 / winners;
            }
            outcomes++;
        }

        // Order of turn/river does not affect final hand strength: each unordered pair has equal weight.
        if (missing == 0) RecordOutcome();
        else
        {
            for (int a = 0; a < remaining.Count - missing + 1; a++)
            {
                foreach (Card[] hand in completedHands) hand[board.Count + 2] = remaining[a];
                if (missing == 1) RecordOutcome();
                else
                {
                    for (int b = a + 1; b < remaining.Count; b++)
                    {
                        foreach (Card[] hand in completedHands) hand[6] = remaining[b];
                        RecordOutcome();
                    }
                }
            }
        }
        var results = new PokerPlayerEquity[hands.Count];
        for (int i = 0; i < results.Length; i++)
            results[i] = new PokerPlayerEquity(wins[i], ties[i], shares[i], outcomes);
        return new PokerEquityResult(outcomes, results);
    }

    private static Card CopyKnownCard(Card card, bool[] seen)
    {
        if (card == null) throw new ArgumentException("Known cards cannot contain null entries.");
        int rank = (int)card.rank;
        int suit = (int)card.suit;
        if (rank < 2 || rank > 14 || suit < 0 || suit > 3)
            throw new ArgumentException("Invalid known card rank or suit.");
        int id = (rank - 2) * 4 + suit;
        if (seen[id]) throw new ArgumentException("A physical card cannot occur more than once in the calculation.");
        seen[id] = true;
        return new Card(card.suit, card.rank);
    }
}
