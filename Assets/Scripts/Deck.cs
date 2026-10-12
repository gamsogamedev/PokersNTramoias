using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gerencia o baralho de 52 cartas: montagem, shuffle e distribuição.
/// </summary>
public class Deck
{
    private List<Card> cards = new List<Card>();

    public int CardsRemaining => cards.Count;

    public Deck()
    {
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
            int j = Random.Range(0, i + 1);
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
            Debug.LogWarning("Deck: tentativa de puxar carta com o baralho vazio.");
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
        position = Mathf.Clamp(position, 0, cards.Count);
        cards.Insert(position, card);
    }
}
