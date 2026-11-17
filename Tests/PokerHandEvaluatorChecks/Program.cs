using System.Diagnostics;

static Card[] Cards(string text) => text.Split(' ').Select(token =>
{
    int rank = "23456789TJQKA".IndexOf(token[0]) + 2;
    int suit = "hdcs".IndexOf(token[1]); // Copas, Ouros, Paus, Espadas
    return new Card((Card.Suit)suit, (Card.Rank)rank);
}).ToArray();

static PokerHandValue Hand(string text) => PokerHandEvaluator.Evaluate(Cards(text));

int assertions = 0;
void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    assertions++;
}

void Reject<T>(Action action, string message) where T : Exception
{
    try { action(); }
    catch (T) { assertions++; return; }
    throw new Exception(message);
}

var examples = new[]
{
    "As Kd 9c 7h 3s", "As Ad Kc 7h 3s", "As Ad Kc Kh 3s",
    "As Ad Ac 7h 3s", "9s 8d 7c 6h 5s", "As Js 9s 7s 3s",
    "As Ad Ac 7h 7s", "As Ad Ac Ah 3s", "As Ks Qs Js Ts"
};
for (int i = 0; i < examples.Length; i++)
{
    Check((int)Hand(examples[i]).Category == i, $"Wrong category for {examples[i]}");
    if (i > 0) Check(Hand(examples[i]) > Hand(examples[i - 1]), "Category ordering");
}

var comparisons = new (string stronger, string weaker)[]
{
    ("As Kd Qc Jh 9s", "As Kd Qc Jh 8s"), // last high-card kicker
    ("As Ad Kc Qh Js", "As Ad Kc Qh Ts"), // last pair kicker
    ("Ks Kd 2c 2h As", "Qs Qd Jc Jh As"), // higher pair first
    ("As Ad Kc Kh 2s", "As Ad Qc Qh Ks"), // lower pair before kicker
    ("As Ad Kc Kh 3s", "As Ad Kc Kh 2s"), // two-pair kicker
    ("9s 9d 9c Ah Ks", "9s 9d 9c Ah Qs"), // second trips kicker
    ("6s 5d 4c 3h 2s", "As 5d 4c 3h 2s"), // wheel is five high
    ("6s 5s 4s 3s 2s", "As 5s 4s 3s 2s"), // wheel straight flush
    ("As Js 9s 7s 4s", "As Js 9s 7s 3s"), // all flush kickers
    ("Ks Kd Kc 2h 2s", "Qs Qd Qc Ah As"), // full-house trips before pair
    ("Ks Kd Kc 3h 3s", "Ks Kd Kc 2h 2s"), // full-house pair
    ("9s 9d 9c 9h As", "9s 9d 9c 9h Ks") // quads kicker
};
foreach (var pair in comparisons)
{
    var higher = Hand(pair.stronger);
    var lower = Hand(pair.weaker);
    Check(higher > lower && lower < higher && higher.CompareTo(lower) > 0, $"Ordering: {pair}");
    Check(higher != lower && higher >= lower && lower <= higher, $"Operators: {pair}");
}

var tiedA = Hand("As Kd Qc Jh 9s");
var tiedB = Hand("Ah Ks Qd Jc 9h");
Check(tiedA == tiedB && tiedA.CompareTo(tiedB) == 0 && tiedA.Equals((object)tiedB), "Suits must not break ties");
Check(tiedA.GetHashCode() == tiedB.GetHashCode(), "Equal hashes");
Check(Hand("As 2d 3c 4h 6s").Category == PokerHandCategory.HighCard, "No wraparound straight");
Check(Hand("As Ad Ac Kh Kd Kc 2s") == Hand("As Ad Ac Kh Kd"), "Two triplets form full house");
Check(Hand("As Ad Kh Kd Qs Qd Jc") == Hand("As Ad Kh Kd Qs"), "Three pairs choose top two and best kicker");
Check(Hand("As Js 9s 7s 5s 3s 2s") == Hand("As Js 9s 7s 5s"), "Choose top five of seven suited cards");
Check(Hand("As Ks Qs Js Ts 9d 8c") == Hand("As Ks Qs Js Ts"), "Straight flush from seven");
Check(Hand("As Kd Qc Jh Ts 9d 8c") == Hand("As Kd Qc Jh Ts"), "Highest available straight");
Check(Hand("9s 9d 9c 9h As Ad Kc") == Hand("9s 9d 9c 9h As"), "Quad kicker can come from pair");
Check(Hand("As Ad Ac Ks Kd 2c") == Hand("As Ad Ac Ks Kd"), "Six-card evaluation");

var board = Cards("As Ks Qs Js Ts");
Check(PokerHandEvaluator.EvaluateHoldem(Cards("2h 3d"), board)
    == PokerHandEvaluator.EvaluateHoldem(Cards("Ah Ad"), board), "Board can play for everyone");
Check(PokerHandEvaluator.EvaluateHoldem(Cards("As 2d"), Cards("Ks Qs Js Ts 3h"))
    == Hand("As Ks Qs Js Ts"), "Use one hole card");
Check(PokerHandEvaluator.EvaluateHoldem(Cards("As Ks"), Cards("Qs Js Ts 3h 2d"))
    == Hand("As Ks Qs Js Ts"), "Use both hole cards");
Check(PokerHandEvaluator.EvaluateHoldem(Cards("As Ad"), Cards("Ac Kh Kd"))
    == Hand("As Ad Ac Kh Kd"), "Flop accepted");
Check(PokerHandEvaluator.EvaluateHoldem(Cards("As Ad"), Cards("Ac Kh Kd 2s"))
    == Hand("As Ad Ac Kh Kd"), "Turn accepted");

var original = Cards("As Ad Kc Qh Js 2c 3d");
var references = original.ToArray();
var initial = PokerHandEvaluator.Evaluate(original);
Array.Reverse(original);
Check(PokerHandEvaluator.Evaluate(original) == initial, "Order independence");
Check(original.All(card => !card.faceUp), "Do not change visibility");
foreach (Card card in original) card.faceUp = true;
Check(PokerHandEvaluator.Evaluate(original) == initial, "Visibility independence");
Check(original.SequenceEqual(references.Reverse()), "Do not reorder input");

Reject<ArgumentNullException>(() => PokerHandEvaluator.Evaluate(null), "Null list");
foreach (int count in new[] { 0, 2, 4, 8 })
    Reject<ArgumentException>(() => PokerHandEvaluator.Evaluate(new Card[count]), "Invalid size");
Reject<ArgumentException>(() => Hand("As As Kc Qh Js"), "Duplicate physical card");
Reject<ArgumentException>(() => PokerHandEvaluator.Evaluate(new Card[] { null, null, null, null, null }), "Null card");
foreach (var invalid in new[] { new Card((Card.Suit)4, Card.Rank.As), new Card((Card.Suit)(-1), Card.Rank.As),
    new Card(Card.Suit.Copas, (Card.Rank)1), new Card(Card.Suit.Copas, (Card.Rank)15) })
    Reject<ArgumentException>(() => PokerHandEvaluator.Evaluate(new[] { invalid }.Concat(Cards("2s 3s 4s 5s")).ToArray()), "Invalid enum");
Reject<ArgumentNullException>(() => PokerHandEvaluator.EvaluateHoldem(null, board), "Null holes");
Reject<ArgumentNullException>(() => PokerHandEvaluator.EvaluateHoldem(Cards("2s 3s"), null), "Null board");
Reject<ArgumentException>(() => PokerHandEvaluator.EvaluateHoldem(Cards("2s 3s 4s"), board), "Extra reserved card rejected");
Reject<ArgumentException>(() => PokerHandEvaluator.EvaluateHoldem(Cards("2s 3s"), Cards("4s 5s")), "Preflop rejected");
Reject<ArgumentException>(() => PokerHandEvaluator.EvaluateHoldem(Cards("As 3s"), board), "Duplicate across holes and board");

IReadOnlyList<Card>[] KnownHands(params string[] hands) => hands.Select(hand => (IReadOnlyList<Card>)Cards(hand)).ToArray();
void Near(double actual, double expectedValue, string message) => Check(Math.Abs(actual - expectedValue) < 1e-10, message);
var known = KnownHands("Ah Ad", "Kh Kd", "Qh Qd");
var turnBoard = Cards("2c 7s 9h Jd");
var turnOdds = PokerEquityCalculator.Calculate(known, turnBoard);
Check(turnOdds.Outcomes == 42, "Three-player turn outcome count");
Near(turnOdds.Players[0].Equity, 38.0 / 42, "Aces against four set outs");
Near(turnOdds.Players[1].WinProbability, 2.0 / 42, "Kings have two outs");
Near(turnOdds.Players[2].WinProbability, 2.0 / 42, "Queens have two outs");
Check(turnOdds.Players.All(p => p.Ties == 0), "No ties in pair fixture");

var blocked = PokerEquityCalculator.Calculate(known, turnBoard, Cards("Kc Ks"));
Check(blocked.Outcomes == 40, "Known dead cards reduce river pool");
Near(blocked.Players[1].Equity, 0, "Dead kings cannot be drawn");
Near(blocked.Players[0].Equity, 38.0 / 40, "Equity respects dead-card blockers");
var headsUp = PokerEquityCalculator.Calculate(KnownHands("Ah Ad", "Kh Kd"), turnBoard);
Check(headsUp.Outcomes == 44, "Heads-up turn outcome count");
Near(headsUp.Players[0].Equity, 42.0 / 44, "Heads-up aces equity");

var riverOdds = PokerEquityCalculator.Calculate(known, Cards("2c 7s 9h Jd Kc"));
Check(riverOdds.Outcomes == 1 && riverOdds.Players[1].Wins == 1, "River evaluates exactly once");
Near(riverOdds.Players[1].Equity, 1, "River winner gets whole pot");
Near(riverOdds.Players[0].Equity, 0, "River loser equity");
var split = PokerEquityCalculator.Calculate(KnownHands("2h 3d", "4h 5d", "6h 7d"), Cards("As Ks Qs Js Ts"));
foreach (var player in split.Players)
{
    Near(player.Equity, 1.0 / 3, "Three-way split equity");
    Near(player.WinProbability, 0, "Split is not an outright win");
    Near(player.TieProbability, 1, "Every participant ties on royal board");
}
var twoWaySplit = PokerEquityCalculator.Calculate(KnownHands("Ah Kc", "Ad Qc", "9h 9d"), Cards("2c 3d 4h 5s 9c"));
Near(twoWaySplit.Players[0].Equity, 0.5, "Two-way split share");
Near(twoWaySplit.Players[1].Equity, 0.5, "Second split share");
Near(twoWaySplit.Players[2].Equity, 0, "Losing third player gets no split share");

var flopBoard = Cards("2c 7s 9h");
var flopOdds = PokerEquityCalculator.Calculate(known, flopBoard);
Check(flopOdds.Outcomes == 903, "Three-player flop enumerates C(43,2)");
Near(flopOdds.Players.Sum(p => p.Equity), 1, "Total equity conserves one pot");
Check(PokerEquityCalculator.Calculate(known, flopBoard, Cards("3c 4c")).Outcomes == 820, "Reserved cards reduce flop boards to C(41,2)");
var reversedOdds = PokerEquityCalculator.Calculate(known.Reverse().ToArray(), flopBoard);
for (int i = 0; i < known.Length; i++) Near(reversedOdds.Players[2 - i].Equity, flopOdds.Players[i].Equity, "Player permutation symmetry");

// Independent enumeration path: flop probabilities equal the mean of all possible turn probabilities.
var allCards = (from suit in Enumerable.Range(0, 4) from rank in Enumerable.Range(2, 13)
                select new Card((Card.Suit)suit, (Card.Rank)rank)).ToArray();
var unavailable = known.SelectMany(h => h).Concat(flopBoard).Select(c => c.ToShortString()).ToHashSet();
var turnSums = new double[3];
foreach (var turn in allCards.Where(c => !unavailable.Contains(c.ToShortString())))
{
    var outcome = PokerEquityCalculator.Calculate(known, flopBoard.Concat(new[] { turn }).ToArray());
    for (int i = 0; i < 3; i++) turnSums[i] += outcome.Players[i].Equity;
}
for (int i = 0; i < 3; i++) Near(turnSums[i] / 43, flopOdds.Players[i].Equity, "Flop equals averaged turn equities");
Check(flopBoard.Length == 3 && known.All(h => h.Count == 2) && flopBoard.All(c => !c.faceUp), "Calculator preserves input state");

Reject<ArgumentNullException>(() => PokerEquityCalculator.Calculate(null, flopBoard), "Null hands rejected");
Reject<ArgumentNullException>(() => PokerEquityCalculator.Calculate(known, null), "Null board rejected");
Reject<ArgumentException>(() => PokerEquityCalculator.Calculate(KnownHands("Ah Ad"), flopBoard), "Single player rejected");
Reject<ArgumentException>(() => PokerEquityCalculator.Calculate(KnownHands("Ah Ad", "Ah Kd"), flopBoard), "Duplicate across players rejected");
Reject<ArgumentException>(() => PokerEquityCalculator.Calculate(known, Cards("Ah 2c 3c")), "Duplicate on board rejected");
Reject<ArgumentException>(() => PokerEquityCalculator.Calculate(known, flopBoard, Cards("Ah")), "Duplicate dead card rejected");
Reject<ArgumentException>(() => PokerEquityCalculator.Calculate(KnownHands("Ah", "Kh Kd"), flopBoard), "Incomplete hand rejected");
Reject<ArgumentException>(() => PokerEquityCalculator.Calculate(known, Cards("2c 3c")), "Preflop equity not yet supported");
var fullDeckDead = allCards.Where(c => !unavailable.Contains(c.ToShortString())).ToArray();
Reject<ArgumentException>(() => PokerEquityCalculator.Calculate(known, flopBoard, fullDeckDead), "Insufficient board cards rejected");

// Every possible five-card hand: independently known standard-deck category totals.
assertions += PokerGameStateChecks.Run();
var timer = Stopwatch.StartNew();
var deck = (from suit in Enumerable.Range(0, 4)
            from rank in Enumerable.Range(2, 13)
            select new Card((Card.Suit)suit, (Card.Rank)rank)).ToArray();
var totals = new int[9];
var five = new Card[5];
for (int a = 0; a < 48; a++)
for (int b = a + 1; b < 49; b++)
for (int c = b + 1; c < 50; c++)
for (int d = c + 1; d < 51; d++)
for (int e = d + 1; e < 52; e++)
{
    five[0] = deck[a]; five[1] = deck[b]; five[2] = deck[c]; five[3] = deck[d]; five[4] = deck[e];
    totals[(int)PokerHandEvaluator.Evaluate(five).Category]++;
}
int[] expected = { 1302540, 1098240, 123552, 54912, 10200, 5108, 3744, 624, 40 };
for (int i = 0; i < expected.Length; i++)
    Check(totals[i] == expected[i], $"{(PokerHandCategory)i}: expected {expected[i]}, got {totals[i]}");
Console.WriteLine($"PASS: {assertions} assertions, all {totals.Sum():N0} five-card hands ({timer.Elapsed.TotalSeconds:F2}s).");
