using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Componente que fica no prefab visual de uma carta. Controla a exibição
/// da imagem (frente ou verso) baseado nos dados de um Card.
/// Coloque este script num prefab que tenha um componente Image como
/// referência (pode ser o próprio objeto raiz do prefab).
/// </summary>
[RequireComponent(typeof(Image))]
public class CardVisual : MonoBehaviour
{
    private Image image;
    private Card cardData;
    private CardSpriteLibrary spriteLibrary;

    private void Awake()
    {
        image = GetComponent<Image>();
    }

    /// <summary>
    /// Configura essa carta visual com os dados da carta e a biblioteca de sprites.
    /// </summary>
    public void Setup(Card card, CardSpriteLibrary library)
    {
        cardData = card;
        spriteLibrary = library;
        Refresh();
    }

    /// <summary>
    /// Atualiza a imagem exibida de acordo com o estado atual (faceUp ou não).
    /// Chame isso de novo se você virar a carta depois de já ter criado o visual.
    /// </summary>
    public void Refresh()
    {
        if (cardData == null || spriteLibrary == null) return;

        if (cardData.faceUp)
        {
            image.sprite = spriteLibrary.GetSprite(cardData);
        }
        else
        {
            image.sprite = spriteLibrary.cardBackSprite;
        }
    }

    /// <summary>
    /// Vira a carta pra cima (revela) e atualiza o visual.
    /// </summary>
    public void Reveal()
    {
        if (cardData != null)
        {
            cardData.faceUp = true;
            Refresh();
        }
    }
}
