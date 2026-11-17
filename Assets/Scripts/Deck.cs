using System.Collections.Generic;
using System;

/// <summary>
/// Gerencia o baralho de 52 cartas: montagem, shuffle e distribuição.
/// </summary>
public class Deck
{
    private List<Card> cards = new List<Card>();
    private readonly Random random;

    public int CardsRemaining => cards.Count;

    public Deck(Random random = null)
    {
        this.random = random ?? new Random();
        Reset();
    }

    /// <summary>
    /// Reconstrói o baralho completo com as 52 cartas, sem embaralhar.
    /// </summary>
    public void Reset()
    {
        cards.Clear();
        foreach (Card.Suit suit in System.Enum.GetValues(typeof(Card.Suit)))
        {
            foreach (Card.Rank rank in System.Enum.GetValues(typeof(Card.Rank)))
            {
                cards.Add(new Card(suit, rank));
            }
        }
    }

    /// <summary>
    /// Embaralha o baralho usando o algoritmo Fisher-Yates.
    /// </summary>
    public void Shuffle()
    {
        for (int i = cards.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (cards[i], cards[j]) = (cards[j], cards[i]);
        }
    }

    /// <summary>
    /// Retira e retorna a carta do topo do baralho.
    /// </summary>
    public Card Draw()
    {
        if (cards.Count == 0)
        {
            return null;
        }

        Card top = cards[0];
        cards.RemoveAt(0);
        return top;
    }

    /// <summary>
    /// Espia a carta do topo sem removê-la (útil futuramente pro sistema de trapaça).
    /// </summary>
    public Card PeekTop()
    {
        return cards.Count > 0 ? cards[0] : null;
    }

    /// <summary>
    /// Insere uma carta em uma posição específica do baralho (útil futuramente
    /// pra "empilhar" o baralho como parte de uma trapaça).
    /// </summary>
    public void InsertAt(Card card, int position)
    {
        if (card == null) throw new ArgumentNullException(nameof(card));
        if ((int)card.rank < 2 || (int)card.rank > 14 || (int)card.suit < 0 || (int)card.suit > 3)
            throw new ArgumentException("Invalid card.", nameof(card));
        if (cards.Exists(c => c.suit == card.suit && c.rank == card.rank))
            throw new ArgumentException("This card is already in the deck.", nameof(card));
        position = Math.Max(0, Math.Min(position, cards.Count));
        cards.Insert(position, card);
    }
}
