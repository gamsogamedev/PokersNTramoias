using System.Collections.Generic;

/// <summary>
/// Representa as cartas comunitárias na mesa (flop, turn, river).
/// </summary>
public class Table
{
    public List<Card> communityCards = new List<Card>();

    public void ReceiveCard(Card card)
    {
        communityCards.Add(card);
    }

    public void Clear()
    {
        communityCards.Clear();
    }

    public string GetCardsAsString()
    {
        if (communityCards.Count == 0) return "(vazia)";

        List<string> parts = new List<string>();
        foreach (Card c in communityCards)
        {
            parts.Add(c.faceUp ? c.ToShortString() : "??");
        }
        return string.Join(" ", parts);
    }
}
