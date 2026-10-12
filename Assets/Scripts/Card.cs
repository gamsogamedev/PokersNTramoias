using UnityEngine;

/// <summary>
/// Representa uma carta individual do baralho.
/// </summary>
[System.Serializable]
public class Card
{
    public enum Suit { Copas, Ouros, Paus, Espadas }

    // 2 a 10, depois Valete(11), Dama(12), Rei(13), Ás(14)
    public enum Rank
    {
        Dois = 2, Tres = 3, Quatro = 4, Cinco = 5, Seis = 6,
        Sete = 7, Oito = 8, Nove = 9, Dez = 10,
        Valete = 11, Dama = 12, Rei = 13, As = 14
    }

    public Suit suit;
    public Rank rank;

    // Controla se a carta está visível (útil pro dealer "ver" antes de revelar)
    public bool faceUp;

    public Card(Suit suit, Rank rank, bool faceUp = false)
    {
        this.suit = suit;
        this.rank = rank;
        this.faceUp = faceUp;
    }

    /// <summary>
    /// Retorna uma representação curta, ex: "K♠", "10♥", "A♦"
    /// </summary>
    public string ToShortString()
    {
        string rankStr = rank switch
        {
            Rank.Valete => "J",
            Rank.Dama => "Q",
            Rank.Rei => "K",
            Rank.As => "A",
            _ => ((int)rank).ToString()
        };

        string suitStr = suit switch
        {
            Suit.Copas => "♥",
            Suit.Ouros => "♦",
            Suit.Paus => "♣",
            Suit.Espadas => "♠",
            _ => "?"
        };

        return rankStr + suitStr;
    }

    public override string ToString() => ToShortString();
}
