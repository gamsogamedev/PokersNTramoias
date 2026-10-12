using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Banco de dados de sprites das cartas. Crie um asset disso pelo menu
/// Assets > Create > Poker > Card Sprite Library, preencha a lista no
/// Inspector arrastando as 52 imagens de carta, e arraste o asset pro
/// campo correspondente no DealerManager.
/// </summary>
[CreateAssetMenu(fileName = "CardSpriteLibrary", menuName = "Poker/Card Sprite Library")]
public class CardSpriteLibrary : ScriptableObject
{
    [System.Serializable]
    public class CardSpriteEntry
    {
        public Card.Suit suit;
        public Card.Rank rank;
        public Sprite sprite;
    }

    [Header("Sprite usado quando a carta está virada pra baixo")]
    public Sprite cardBackSprite;

    [Header("Arraste aqui as 52 entradas (naipe + valor + sprite)")]
    public List<CardSpriteEntry> entries = new List<CardSpriteEntry>();

    private Dictionary<(Card.Suit, Card.Rank), Sprite> lookup;

    private void BuildLookupIfNeeded()
    {
        if (lookup != null) return;

        lookup = new Dictionary<(Card.Suit, Card.Rank), Sprite>();
        foreach (var entry in entries)
        {
            var key = (entry.suit, entry.rank);
            if (!lookup.ContainsKey(key))
                lookup.Add(key, entry.sprite);
        }
    }

    /// <summary>
    /// Retorna o sprite correspondente a uma carta específica.
    /// </summary>
    public Sprite GetSprite(Card card)
    {
        BuildLookupIfNeeded();

        if (lookup.TryGetValue((card.suit, card.rank), out Sprite sprite))
            return sprite;

        Debug.LogWarning($"CardSpriteLibrary: sprite não encontrado para {card.ToShortString()}");
        return null;
    }
}
