using System;
using System.Collections.Generic;

/// <summary>Standard high-poker categories, ordered from weakest to strongest.</summary>
public enum PokerHandCategory
{
    HighCard, OnePair, TwoPair, ThreeOfAKind, Straight, Flush,
    FullHouse, FourOfAKind, StraightFlush
}

/// <summary>
/// An immutable comparison value. Higher values win; equal values split the pot.
/// Suits never break ties. A royal flush is an ace-high StraightFlush.
/// </summary>
public readonly struct PokerHandValue : IComparable<PokerHandValue>, IEquatable<PokerHandValue>
{
    private readonly int score;

    public PokerHandCategory Category => (PokerHandCategory)(score >> 20);

    internal PokerHandValue(int score) { this.score = score; }

    public int CompareTo(PokerHandValue other) => score.CompareTo(other.score);
    public bool Equals(PokerHandValue other) => score == other.score;
    public override bool Equals(object obj) => obj is PokerHandValue other && Equals(other);
    public override int GetHashCode() => score;
    public override string ToString() => Category.ToString();

    public static bool operator ==(PokerHandValue a, PokerHandValue b) => a.Equals(b);
    public static bool operator !=(PokerHandValue a, PokerHandValue b) => !a.Equals(b);
    public static bool operator >(PokerHandValue a, PokerHandValue b) => a.score > b.score;
    public static bool operator <(PokerHandValue a, PokerHandValue b) => a.score < b.score;
    public static bool operator >=(PokerHandValue a, PokerHandValue b) => a.score >= b.score;
    public static bool operator <=(PokerHandValue a, PokerHandValue b) => a.score <= b.score;
}

/// <summary>
/// Evaluates the best five-card hand from five to seven distinct standard-deck cards.
/// No Unity API, randomness, or shared mutable state. Inputs are never modified.
/// faceUp affects presentation only and is deliberately ignored here.
/// </summary>
public static class PokerHandEvaluator
{
    public static PokerHandValue Evaluate(IReadOnlyList<Card> cards)
    {
        if (cards == null) throw new ArgumentNullException(nameof(cards));
        if (cards.Count < 5 || cards.Count > 7)
            throw new ArgumentException("Provide five to seven cards.", nameof(cards));

        ulong seen = 0;
        for (int i = 0; i < cards.Count; i++)
        {
            Card card = cards[i];
            if (card == null) throw new ArgumentException("Cards cannot be null.", nameof(cards));
            int rank = (int)card.rank;
            int suit = (int)card.suit;
            if (rank < 2 || rank > 14 || suit < 0 || suit > 3)
                throw new ArgumentException("Invalid card rank or suit.", nameof(cards));
            ulong bit = 1UL << ((rank - 2) * 4 + suit);
            if ((seen & bit) != 0)
                throw new ArgumentException("Duplicate cards are not allowed.", nameof(cards));
            seen |= bit;
        }

        return EvaluateDistinct(cards, new int[15]);
    }

    // The sampler deals distinct cards and reuses its own rank-count buffer.
    internal static PokerHandValue EvaluateDistinct(IReadOnlyList<Card> cards, int[] counts)
    {
        int best = 0;
        // At most 21 combinations. Hold'em permits using zero, one, or two hole cards.
        for (int a = 0; a < cards.Count - 4; a++)
        for (int b = a + 1; b < cards.Count - 3; b++)
        for (int c = b + 1; c < cards.Count - 2; c++)
        for (int d = c + 1; d < cards.Count - 1; d++)
        for (int e = d + 1; e < cards.Count; e++)
        {
            int value = EvaluateFive(cards[a], cards[b], cards[c], cards[d], cards[e], counts);
            if (value > best) best = value;
        }
        return new PokerHandValue(best);
    }

    /// <summary>
    /// Evaluates two hole cards with a flop, turn, or river board.
    /// Reserved swap cards must not be included until actually placed on the board.
    /// </summary>
    public static PokerHandValue EvaluateHoldem(IReadOnlyList<Card> holeCards, IReadOnlyList<Card> board)
    {
        if (holeCards == null) throw new ArgumentNullException(nameof(holeCards));
        if (board == null) throw new ArgumentNullException(nameof(board));
        if (holeCards.Count != 2)
            throw new ArgumentException("Hold'em requires exactly two hole cards.", nameof(holeCards));
        if (board.Count < 3 || board.Count > 5)
            throw new ArgumentException("Provide three to five community cards.", nameof(board));

        var cards = new Card[2 + board.Count];
        cards[0] = holeCards[0];
        cards[1] = holeCards[1];
        for (int i = 0; i < board.Count; i++) cards[i + 2] = board[i];
        return Evaluate(cards);
    }

    private static int EvaluateFive(Card a, Card b, Card c, Card d, Card e, int[] counts)
    {
        Array.Clear(counts, 0, counts.Length);
        counts[(int)a.rank]++;
        counts[(int)b.rank]++;
        counts[(int)c.rank]++;
        counts[(int)d.rank]++;
        counts[(int)e.rank]++;

        bool flush = a.suit == b.suit && a.suit == c.suit && a.suit == d.suit && a.suit == e.suit;
        int straightHigh = 0, run = 0;
        int four = 0, three = 0, highPair = 0, lowPair = 0;
        int singles = 0, singleCount = 0;
        for (int rank = 14; rank >= 2; rank--)
        {
            run = counts[rank] > 0 ? run + 1 : 0;
            if (run == 5) straightHigh = rank + 4;
            switch (counts[rank])
            {
                case 4: four = rank; break;
                case 3: three = rank; break;
                case 2:
                    if (highPair == 0) highPair = rank;
                    else lowPair = rank;
                    break;
                case 1:
                    singles = (singles << 4) | rank;
                    singleCount++;
                    break;
            }
        }
        if (straightHigh == 0 && counts[14] == 1 && counts[5] == 1 && counts[4] == 1
            && counts[3] == 1 && counts[2] == 1)
            straightHigh = 5;

        // Category occupies the high bits; tie breakers use descending four-bit ranks.
        if (flush && straightHigh > 0) return Score(PokerHandCategory.StraightFlush, straightHigh << 16);
        if (four > 0) return Score(PokerHandCategory.FourOfAKind, (four << 16) | (singles << 12));
        if (three > 0 && highPair > 0)
            return Score(PokerHandCategory.FullHouse, (three << 16) | (highPair << 12));
        if (flush) return Score(PokerHandCategory.Flush, singles);
        if (straightHigh > 0) return Score(PokerHandCategory.Straight, straightHigh << 16);
        if (three > 0) return Score(PokerHandCategory.ThreeOfAKind, (three << 16) | (singles << 8));
        if (lowPair > 0)
            return Score(PokerHandCategory.TwoPair, (highPair << 16) | (lowPair << 12) | (singles << 8));
        if (highPair > 0) return Score(PokerHandCategory.OnePair, (highPair << 16) | (singles << 4));
        return Score(PokerHandCategory.HighCard, singles << (4 * (5 - singleCount)));
    }

    private static int Score(PokerHandCategory category, int tieBreakers) => ((int)category << 20) | tieBreakers;
}
