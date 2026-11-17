using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Ferramenta de editor que preenche automaticamente um asset CardSpriteLibrary
/// varrendo a pasta de sprites e casando os nomes de arquivo com naipe/valor,
/// baseado na numeração sequencial usada nos arquivos.
///
/// Uso: Tools > Poker > Auto-Fill Card Sprite Library (menu no topo do Unity)
/// </summary>
public class CardSpriteAutoImporter
{
    // Ajuste esse caminho se sua pasta "cartas" estiver em outro lugar dentro de Assets.
    private const string PastaSprites = "Assets/Art/Cartas";

    // Prefixo comum a todos os arquivos, ex: "updated-card-2026_2", "updated-card-2026_17"
    private const string PrefixoArquivo = "updated-card-2026_";

    // Ordem das cartas dentro de cada naipe: Ás primeiro, depois 2 a 10, Valete, Dama, Rei.
    // (baseado no que foi descrito: A de copas = primeiro número da faixa)
    private static readonly Card.Rank[] OrdemDasCartas = new Card.Rank[]
    {
        Card.Rank.As, Card.Rank.Dois, Card.Rank.Tres, Card.Rank.Quatro,
        Card.Rank.Cinco, Card.Rank.Seis, Card.Rank.Sete, Card.Rank.Oito,
        Card.Rank.Nove, Card.Rank.Dez, Card.Rank.Valete, Card.Rank.Dama, Card.Rank.Rei
    };

    // Número inicial do arquivo de cada naipe (a partir daí, 13 números sequenciais).
    private static readonly (Card.Suit suit, int inicio)[] InicioPorNaipe = new (Card.Suit, int)[]
    {
        (Card.Suit.Copas,   2),
        (Card.Suit.Espadas, 17),
        (Card.Suit.Ouros,   32),
        (Card.Suit.Paus,    47),
    };

    [MenuItem("Tools/Poker/Auto-Fill Card Sprite Library")]
    public static void AutoFill()
    {
        // Localiza o asset CardSpriteLibrary existente no projeto.
        string[] guids = AssetDatabase.FindAssets("t:CardSpriteLibrary");
        if (guids.Length == 0)
        {
            Debug.LogError("Nenhum asset CardSpriteLibrary encontrado. Crie um primeiro " +
                            "(Assets > Create > Poker > Card Sprite Library) antes de rodar essa ferramenta.");
            return;
        }

        string libraryPath = AssetDatabase.GUIDToAssetPath(guids[0]);
        CardSpriteLibrary library = AssetDatabase.LoadAssetAtPath<CardSpriteLibrary>(libraryPath);

        // Monta um dicionário nome-do-arquivo (sem extensão) -> Sprite,
        // varrendo todos os sprites dentro da pasta configurada.
        Dictionary<string, Sprite> spritesPorNome = new Dictionary<string, Sprite>();
        string[] spriteGuids = AssetDatabase.FindAssets("t:Sprite", new[] { PastaSprites });

        foreach (string guid in spriteGuids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);

            // LoadAllAssetsAtPath devolve TODOS os sprites do arquivo: funciona tanto
            // para imagens individuais quanto para spritesheets fatiadas (Multiple),
            // onde cada sprite tem seu próprio nome (ex: "updated-card-2026_2").
            Object[] todos = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            foreach (Object obj in todos)
            {
                if (obj is Sprite sprite && !spritesPorNome.ContainsKey(sprite.name))
                    spritesPorNome.Add(sprite.name, sprite);
            }
        }

        if (spritesPorNome.Count == 0)
        {
            Debug.LogError($"Nenhum sprite encontrado em '{PastaSprites}'. Confira se o caminho está " +
                            "correto e se as imagens estão com Texture Type = Sprite (2D and UI).");
            return;
        }

        library.entries.Clear();
        int totalEncontrado = 0;
        int totalEsperado = 0;
        List<string> faltando = new List<string>();

        foreach (var (suit, inicio) in InicioPorNaipe)
        {
            for (int i = 0; i < OrdemDasCartas.Length; i++)
            {
                int numeroArquivo = inicio + i;
                string nomeArquivo = $"{PrefixoArquivo}{numeroArquivo}";
                totalEsperado++;

                if (spritesPorNome.TryGetValue(nomeArquivo, out Sprite sprite))
                {
                    library.entries.Add(new CardSpriteLibrary.CardSpriteEntry
                    {
                        suit = suit,
                        rank = OrdemDasCartas[i],
                        sprite = sprite
                    });
                    totalEncontrado++;
                }
                else
                {
                    faltando.Add($"{nomeArquivo} ({suit} - {OrdemDasCartas[i]})");
                }
            }
        }

        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();

        Debug.Log($"CardSpriteLibrary preenchida: {totalEncontrado}/{totalEsperado} sprites atribuídos " +
                  $"({spritesPorNome.Count} sprites encontrados na pasta).");

        // Diagnóstico: se nada casou, mostra como os sprites realmente se chamam
        if (totalEncontrado == 0)
        {
            List<string> amostra = new List<string>(spritesPorNome.Keys);
            amostra.Sort();
            if (amostra.Count > 15) amostra = amostra.GetRange(0, 15);

            Debug.LogWarning("Nenhum nome bateu com o padrão esperado " +
                             $"('{PrefixoArquivo}N'). Exemplos de nomes reais encontrados:\n" +
                             string.Join("\n", amostra) +
                             "\n\nAjuste a constante PrefixoArquivo no script para bater com esses nomes.");
        }

        if (faltando.Count > 0)
        {
            Debug.LogWarning($"{faltando.Count} sprite(s) NÃO encontrado(s):\n" + string.Join("\n", faltando));
        }
    }
}