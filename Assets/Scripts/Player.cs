using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Representa um jogador na mesa: nome, mão de cartas e fichas.
/// </summary>
[System.Serializable]
public class Player
{
    public string playerName;
    public List<Card> hand = new List<Card>();
    public int chips = 1000;

    // Marca se esse jogador é o "comparsa" — útil mais pra frente
    // quando o sistema de trapaça precisar saber quem favorecer.
    public bool isAccomplice;

    public Player(string name, bool isAccomplice = false)
    {
        playerName = name;
        this.isAccomplice = isAccomplice;
    }

    public void ReceiveCard(Card card)
    {
        hand.Add(card);
    }

    public void ClearHand()
    {
        hand.Clear();
    }

    public string GetHandAsString()
    {
        if (hand.Count == 0) return "(sem cartas)";

        List<string> parts = new List<string>();
        foreach (Card c in hand)
        {
            parts.Add(c.faceUp ? c.ToShortString() : "??");
        }
        return string.Join(" ", parts);
    }
}
