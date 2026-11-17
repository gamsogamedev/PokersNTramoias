using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>A self-contained, randomly dealt comparison sandbox. Builds its UI on entering Play mode.</summary>
public sealed class PokerEvaluatorTestScene : MonoBehaviour
{
    public PokerGameState Game { get; private set; }
    private readonly Text[] boardCards = new Text[5];
    private readonly Text[,] holeCards = new Text[3, 2];
    private readonly Text[] rankings = new Text[3];
    private readonly Text[] equities = new Text[3];
    private readonly Text[] probabilities = new Text[3];
    private readonly Image[] playerPanels = new Image[3];
    private readonly Text[] seatLabels = new Text[3];
    private readonly Text[] reservedCards = new Text[2];
    private readonly Button[] swapButtons = new Button[2];
    private Text callLabel, raiseLabel;
    private Button checkButton, raiseButton, foldButton;
    private Text streetLabel;
    private Button nextButton;
    private Text nextLabel;
    private Font font;

    private static readonly CultureInfo Portuguese = CultureInfo.GetCultureInfo("pt-BR");

    private static readonly Color Background = new Color32(13, 27, 28, 255);
    private static readonly Color Panel = new Color32(25, 47, 48, 255);
    private static readonly Color LeaderPanel = new Color32(48, 85, 66, 255);
    private static readonly Color Accent = new Color32(175, 235, 171, 255);
    private static readonly Color Muted = new Color32(160, 184, 181, 255);

    private void Start()
    {
        BuildUI();
        NewHand();
    }

    public void NewHand()
    {
        if (Game == null)
        {
            Game = new PokerGameState();
            Game.StateChanged += OnStateChanged;
        }
        Game.StartNewHand();
    }

    public void RevealNextCard() { if (Game.CanAdvanceStreet) Game.AdvanceStreet(); }
    public void CheckOrCall() { if (Game.CanCheckOrCall) Game.CheckOrCall(Game.ActingPlayerIndex); }
    public void RaiseMinimum()
    {
        if (Game.CanRaise) Game.BetOrRaiseTo(Game.ActingPlayerIndex, Game.CurrentBet + Game.MinimumRaise);
    }
    public void FoldCurrentPlayer() { if (Game.ActingPlayerIndex >= 0) Game.Fold(Game.ActingPlayerIndex); }
    public void SwapReservedCard(int index)
    {
        if (Game.CanSwap && index >= 0 && index < Game.CaptureSnapshot().ReservedCards.Count) Game.SwapCurrentStreetCard(index);
    }
    private void OnStateChanged(PokerGameEvent change) => Refresh();
    private void OnDestroy() { if (Game != null) Game.StateChanged -= OnStateChanged; }

    private void Refresh()
    {
        PokerGameSnapshot state = Game.CaptureSnapshot();
        streetLabel.text = $"MÃO {state.HandNumber:00}   /   {StreetName(state.Street)}   /   {state.Board.Count} / 5";
        for (int i = 0; i < boardCards.Length; i++) ShowCard(boardCards[i], i < state.Board.Count ? state.Board[i] : null);
        for (int i = 0; i < reservedCards.Length; i++)
        {
            ShowCard(reservedCards[i], i < state.ReservedCards.Count ? state.ReservedCards[i] : null);
            swapButtons[i].interactable = state.CanSwap && i < state.ReservedCards.Count;
        }
        var activeHands = new List<IReadOnlyList<Card>>();
        var activeIndices = new List<int>();
        var dead = new List<Card>(state.ReservedCards);
        dead.AddRange(state.Discards);
        var values = new PokerHandValue[3];
        PokerHandValue best = default;
        for (int i = 0; i < state.Players.Count; i++)
        {
            PokerSeatSnapshot player = state.Players[i];
            seatLabels[i].text = $"JOGADOR {i + 1} / {player.Chips} fichas / aposta {player.StreetContribution}";
            for (int j = 0; j < 2; j++) ShowCard(holeCards[i, j], player.HoleCards[j]);
            if (player.HasFolded) dead.AddRange(player.HoleCards);
            else
            {
                activeHands.Add(player.HoleCards); activeIndices.Add(i);
                if (state.Board.Count >= 3)
                {
                    values[i] = PokerHandEvaluator.EvaluateHoldem(player.HoleCards, state.Board);
                    if (values[i] > best) best = values[i];
                }
            }
            rankings[i].text = player.HasFolded ? "Foldou" : state.Board.Count < 3 ? "Aguardando o flop" : CategoryName(values[i].Category);
            equities[i].text = player.HasFolded ? "0,0% de equidade" : "Equidade: --";
            probabilities[i].text = player.HasFolded ? "Vitória 0,0% / Empate 0,0%" : "Cálculo após o flop";
        }
        PokerEquityResult equity = null;
        if (activeHands.Count > 1 && state.Board.Count >= 3)
            equity = PokerEquityCalculator.Calculate(activeHands, state.Board, dead);
        var leaders = new List<string>();
        for (int a = 0; a < activeIndices.Count; a++)
        {
            int i = activeIndices[a];
            bool leads = activeHands.Count == 1 || (state.Board.Count >= 3 && values[i] == best);
            if (leads) leaders.Add(PlayerName(i));
            if (leads) rankings[i].text += state.IsHandOver ? "\nVENCEDOR" : "\nNA LIDERANÇA";
            if (state.Players[i].IsAllIn) rankings[i].text += " / SEM FICHAS";
            if (equity != null)
            {
                PokerPlayerEquity odds = equity.Players[a];
                equities[i].text = $"{Percent(odds.Equity)} de equidade";
                probabilities[i].text = $"Vitória {Percent(odds.WinProbability)} / Empate {Percent(odds.TieProbability)}";
            }
            else if (activeHands.Count == 1)
            {
                equities[i].text = "100,0% de equidade"; probabilities[i].text = "Vitória por desistências";
            }
        }
        for (int i = 0; i < state.Players.Count; i++)
        {
            bool leads = leaders.Contains(PlayerName(i));
            playerPanels[i].color = leads ? LeaderPanel : Panel;
            rankings[i].color = leads ? Accent : Muted;
        }
        nextButton.interactable = Game.CanAdvanceStreet;
        nextLabel.text = state.IsHandOver ? "Mão encerrada" : state.Street == PokerStreet.Preflop ? "Flop"
            : state.Street == PokerStreet.Flop ? "Turn" : state.Street == PokerStreet.Turn ? "River" : "Mostrar cartas";
        bool canAct = state.ActingPlayerIndex >= 0;
        checkButton.interactable = state.CanCheckOrCall;
        foldButton.interactable = canAct;
        raiseButton.interactable = state.CanRaise;
        int call = canAct ? Game.AmountToCall(state.ActingPlayerIndex) : 0;
        callLabel.text = canAct && !state.CanCheckOrCall ? "Aguardando aposta"
            : call == 0 ? "Check" : $"Pagar {System.Math.Min(call, state.Players[state.ActingPlayerIndex].Chips)}";
        raiseLabel.text = state.CurrentBet == 0 ? $"Apostar {state.MinimumRaise}" : $"Aumentar para {state.CurrentBet + state.MinimumRaise}";

    }

    private static string PlayerName(int index) => $"Jogador {index + 1}";
    private static string Percent(double value) => (value * 100).ToString("F1", Portuguese) + "%";

    private static string StreetName(PokerStreet street)
    {
        switch (street)
        {
            case PokerStreet.Preflop: return "PRÉ-FLOP";
            case PokerStreet.Flop: return "FLOP";
            case PokerStreet.Turn: return "TURN";
            case PokerStreet.River: return "RIVER";
            case PokerStreet.Showdown: return "FINALIZAR";
            default: return "AGUARDANDO INÍCIO";
        }
    }

    private static string CategoryName(PokerHandCategory category)
    {
        switch (category)
        {
            case PokerHandCategory.HighCard: return "Carta alta";
            case PokerHandCategory.OnePair: return "Pair";
            case PokerHandCategory.TwoPair: return "Two pair";
            case PokerHandCategory.ThreeOfAKind: return "Three of a kind";
            case PokerHandCategory.Straight: return "Straight";
            case PokerHandCategory.Flush: return "Flush";
            case PokerHandCategory.FullHouse: return "Full house";
            case PokerHandCategory.FourOfAKind: return "Four of a kind";
            case PokerHandCategory.StraightFlush: return "Straight flush";
            default: return "Mão desconhecida";
        }
    }

    private void ShowCard(Text label, Card card)
    {
        Image image = label.transform.parent.GetComponent<Image>();
        if (card == null)
        {
            label.text = "?";
            label.color = Muted;
            image.color = new Color32(37, 65, 65, 255);
            return;
        }
        string rank = (int)card.rank <= 10 ? ((int)card.rank).ToString()
            : card.rank == Card.Rank.Valete ? "J" : card.rank == Card.Rank.Dama ? "Q"
            : card.rank == Card.Rank.Rei ? "K" : "A";
        string suit = card.suit == Card.Suit.Copas ? "COPAS" : card.suit == Card.Suit.Ouros ? "OUROS"
            : card.suit == Card.Suit.Paus ? "PAUS" : "ESPADAS";
        label.text = $"<size=36><b>{rank}</b></size>\n<size=11>{suit}</size>";
        label.color = card.suit == Card.Suit.Copas || card.suit == Card.Suit.Ouros
            ? new Color32(178, 48, 60, 255) : new Color32(25, 40, 45, 255);
        image.color = new Color32(244, 241, 230, 255);
    }

    private void BuildUI()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var cameraObject = new GameObject("Sandbox Camera", typeof(Camera));
        cameraObject.transform.SetParent(transform, false);
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Background;
        camera.cullingMask = 0;

        var canvasObject = new GameObject("Evaluator Canvas", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1100, 1120);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        RectTransform root = Rect("Table", canvasObject.transform, 0, 0, 1100, 1120);
        root.gameObject.AddComponent<Image>().color = Background;

        //Label("Title", root, "PÔQUER / LABORATÓRIO DE TESTES", 0, 503, 1000, 48, 30, Accent);
        //Label("Subtitle", root, "Três mãos. Duas cartas reservadas. Apostas manuais para todos.", 0, 460, 1000, 32, 17, Muted);
        streetLabel = Label("Street", root, "", 0, 414, 1000, 32, 16, Accent);
        for (int i = 0; i < 5; i++) boardCards[i] = CardSlot(root, $"Board {i + 1}", (i - 2) * 116, 320, 96, 126);
        //Label("Flop label", root, "FLOP", -116, 238, 320, 24, 13, Muted);
        //Label("Turn label", root, "TURN", 116, 238, 100, 24, 13, Muted);
        //Label("River label", root, "RIVER", 232, 238, 100, 24, 13, Muted);
        for (int i = 0; i < 3; i++)
        {
            RectTransform panel = Rect($"Player {i + 1}", root, (i - 1) * 336, 30, 316, 278);
            playerPanels[i] = panel.gameObject.AddComponent<Image>();
            seatLabels[i] = Label("Name", panel, "", 0, 113, 300, 28, 13, Muted);
            for (int j = 0; j < 2; j++) holeCards[i, j] = CardSlot(panel, $"Hole {j + 1}", (j - 0.5f) * 100, 43, 82, 96);
            rankings[i] = Label("Ranking", panel, "", 0, -37, 296, 54, 17, Color.white);
            equities[i] = Label("Equity", panel, "", 0, -87, 296, 32, 24, Accent);
            probabilities[i] = Label("Probabilities", panel, "", 0, -119, 296, 24, 14, Muted);
        }
        Label("Reserved", root, "JOGADOR 1 / CARTAS RESERVADAS", -300, -145, 420, 28, 16, Accent);
        for (int i = 0; i < 2; i++)
        {
            int index = i;
            reservedCards[i] = CardSlot(root, $"Reserved {i + 1}", -390 + i * 160, -218, 80, 98);
            swapButtons[i] = MakeButton(root, $"Swap {i + 1}", $"Usar carta {i + 1}", -390 + i * 160, -300, 140, () => SwapReservedCard(index));
        }
        checkButton = MakeButton(root, "Check call", "Check", -290, -405, 220, CheckOrCall);
        callLabel = checkButton.GetComponentInChildren<Text>();
        raiseButton = MakeButton(root, "Raise", "Aumentar o mínimo", 0, -405, 250, RaiseMinimum);
        raiseLabel = raiseButton.GetComponentInChildren<Text>();
        foldButton = MakeButton(root, "Fold", "Fold", 290, -405, 220, FoldCurrentPlayer);
        nextButton = MakeButton(root, "Next card", "", -145, -477, 270, RevealNextCard);
        nextLabel = nextButton.GetComponentInChildren<Text>();
        MakeButton(root, "New hand", "Nova mão aleatória", 155, -477, 270, NewHand);
        
        var eventSystem = new GameObject("Sandbox EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        eventSystem.transform.SetParent(transform, false);
    }

    private static RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }

    private Text Label(string name, Transform parent, string text, float x, float y,
        float width, float height, int size, Color color)
    {
        Text label = Rect(name, parent, x, y, width, height).gameObject.AddComponent<Text>();
        label.font = font;
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = TextAnchor.MiddleCenter;
        label.raycastTarget = false;
        label.supportRichText = true;
        return label;
    }

    private Text CardSlot(Transform parent, string name, float x, float y, float width, float height)
    {
        RectTransform rect = Rect(name, parent, x, y, width, height);
        rect.gameObject.AddComponent<Image>().raycastTarget = false;
        return Label("Face", rect, "", 0, 0, width, height, 24, Color.white);
    }

    private Button MakeButton(Transform parent, string name, string text, float x, float y,
        float width, UnityEngine.Events.UnityAction action)
    {
        RectTransform rect = Rect(name, parent, x, y, width, 52);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = Accent;
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.disabledColor = new Color(0.4f, 0.5f, 0.45f, 0.6f);
        button.colors = colors;
        button.onClick.AddListener(action);
        Label("Label", rect, text, 0, 0, width, 52, 19, Background);
        return button;
    }
}
