using System.Collections.Generic;
using UnityEngine;
using TMPro; // remova essa linha se não for usar TextMeshPro

/// <summary>
/// Orquestra a distribuição de cartas: embaralha, distribui para os jogadores
/// e para a mesa. Centraliza tudo em DealCard(), que é o ponto de extensão
/// futuro pro sistema de trapaça (interceptar essa função antes de entregar
/// a carta real).
/// </summary>
public class DealerManager : MonoBehaviour
{
    [Header("Configuração da mesa")]
    public int numeroDeJogadores = 3;
    public int cartasPorJogador = 2;
    public int cartasNaMesa = 5; // Texas Hold'em: 5 (flop 3 + turn 1 + river 1)

    [Header("Referências de UI (opcional, TextMeshPro)")]
    public TMP_Text[] textosMaosJogadores;
    public TMP_Text textoMesa;

    private Deck deck;
    private Table table;
    private List<Player> players = new List<Player>();

    void Start()
    {
        SetupGame();
        PlayRound();
    }

    private void SetupGame()
    {
        deck = new Deck();
        table = new Table();
        players.Clear();

        for (int i = 0; i < numeroDeJogadores; i++)
        {
            // Marque manualmente qual índice é o seu comparsa, ex: índice 0
            bool ehComparsa = (i == 0);
            players.Add(new Player($"Jogador {i + 1}", ehComparsa));
        }
    }

    /// <summary>
    /// Executa uma rodada completa de distribuição: embaralha, distribui
    /// pros jogadores e revela a mesa.
    /// </summary>
    public void PlayRound()
    {
        deck.Reset();
        deck.Shuffle();
        table.Clear();
        foreach (var p in players) p.ClearHand();

        // Distribui cartas pros jogadores (uma de cada vez, como numa mesa real)
        for (int rodada = 0; rodada < cartasPorJogador; rodada++)
        {
            foreach (var player in players)
            {
                Card carta = DealCard();
                carta.faceUp = true; // true só pra debug; no jogo real, cartas do oponente ficam ocultas
                player.ReceiveCard(carta);
            }
        }

        // Distribui cartas pra mesa
        for (int i = 0; i < cartasNaMesa; i++)
        {
            Card carta = DealCard();
            carta.faceUp = true;
            table.ReceiveCard(carta);
        }

        AtualizarUI();
        LogEstadoAtual();
    }

    /// <summary>
    /// Ponto único de distribuição de carta. Toda carta que sai do baralho
    /// passa por aqui — é aqui que futuramente o sistema de trapaça vai
    /// interceptar e trocar a carta antes de entregar.
    /// </summary>
    private Card DealCard()
    {
        return deck.Draw();
    }

    private void AtualizarUI()
    {
        if (textosMaosJogadores != null)
        {
            for (int i = 0; i < players.Count && i < textosMaosJogadores.Length; i++)
            {
                if (textosMaosJogadores[i] != null)
                    textosMaosJogadores[i].text = $"{players[i].playerName}: {players[i].GetHandAsString()}";
            }
        }

        if (textoMesa != null)
        {
            textoMesa.text = $"Mesa: {table.GetCardsAsString()}";
        }
    }

    private void LogEstadoAtual()
    {
        foreach (var p in players)
        {
            Debug.Log($"{p.playerName}{(p.isAccomplice ? " [comparsa]" : "")}: {p.GetHandAsString()}");
        }
        Debug.Log($"Mesa: {table.GetCardsAsString()}");
    }
}
