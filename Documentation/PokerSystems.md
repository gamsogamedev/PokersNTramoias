# Poker systems implementation plan

## Scope and game rules

The player controls their own betting actions and receives two extra random cards
that can replace the turn or river. Each swap recalculates the participants' equity.
The table shares one suspicion meter across all hands of a match. Enemies bet automatically in both playable views. The evaluator scene includes a
manual-seat toggle for testing.

Implemented prototype swap rules: consume one reserved card to replace the newly
revealed turn or river, once on each street, before any betting action on that street.
The replaced community card is discarded and unavailable for later draws. A folded
controlled player cannot swap. These defaults live in `PokerGameState`, independently
of the evaluator, and can be changed as the game design develops.

## Architecture and implementation order

1. **Hand comparisons (implemented):** `Assets/Scripts/Poker/PokerHandEvaluator.cs`
   evaluates the best five cards out of five to seven. Standard high-poker ordering,
   all kickers, ace-low straights, and ties are supported. Suits do not break ties.
   Royal flush is an ace-high straight flush. Evaluation is independent of Unity,
   random sampling, betting, and card visibility. Validate with edge cases and the
   complete five-card category distribution.
2. **State and street progression (implemented):** `PokerGameState` owns the shuffled
   deck, two hole cards per seat, two reserved cards, board, discards, folds, stacks,
   pot, contributions, and acting seat. `DealerManager.PlayRound` now starts preflop;
   later streets wait for betting to complete. Both the main adapter and the test scene
   use the same state class. Swaps are explicit versioned events. See
   [Game state and street progression](GameState.md) for APIs, controls, and limits.
3. **Equity service (postflop implemented):** `PokerEquityCalculator.Calculate` copies
   known hands, board, and optional dead cards and returns immutable per-player
   win/tie probabilities and equity. It exactly enumerates remaining boards on the
   flop, turn, and river and is connected to the test scene. Enemy decisions use a separate sampled estimate from preflop through river.
   Spectator preflop estimates and asynchronous numerical snapshots remain future work.
   Never inspect the actual future
   deck order unless intentionally modeling a known rigged deck. Evaluate active
   players only; model folded/dead cards according to the calculation's knowledge.
4. **Swap events and history (events implemented; history pending):** calculate the before and after positions, store the
   event and snapshots, then update the UI. Recalculate on board reveals, swaps, and
   folds; invalidate results when the relevant state or assumed ranges change.
5. **Suspicion and settlement (implemented)**. Successful swaps and losing enemy
   hands score against the same meter. Stacks carry forward after payouts.
6. **Enemy betting (implemented):** consume the enemy's equity estimate, legal actions, pot, amount
   to call, stack, position, betting history, and personality. Player actions remain
   under player control.

## Hand comparison API

```csharp
PokerHandValue first = PokerHandEvaluator.EvaluateHoldem(players[0].hand, table.communityCards);
PokerHandValue second = PokerHandEvaluator.EvaluateHoldem(players[1].hand, table.communityCards);
int comparison = first.CompareTo(second); // positive: first wins; zero: tie
PokerHandCategory category = first.Category;
```

`Evaluate(cards)` accepts five to seven distinct valid cards. `EvaluateHoldem` accepts
exactly two hole cards and three to five board cards, allowing zero, one, or both hole
cards in the winning five. Neither method computes preflop strength or win probability.
Null entries, duplicate cards, invalid enums, and invalid counts throw argument errors.
`faceUp` is ignored. Reserved cards are not hole cards: include one only after it
replaces a community card. The default `PokerHandValue` is an uninitialized value;
use values returned by the evaluator for comparisons.

## Percentages and information boundaries

Keep outright win probability, tie probability, and equity as separate quantities.
For each completed outcome, a sole winner receives one equity unit; each of N tied
winners receives 1/N. Divide accumulated units by outcome count. Equity describes
expected showdown pot share, not match-winning probability or future folds. Side pots
will require separate eligibility and payout calculations.

- **Spectator/debug view:** all actual hole and reserved cards may be known. Exclude
  them and known discards from hypothetical future draws.
- **Enemy betting view:** own hole cards, public board, observable betting, and estimated
  opponent ranges. Do not remove secret cards by consulting authoritative state.
  Unknown reserved, folded, or discarded cards remain unobserved possibilities.
For three players with known hole cards and no extra reserved/dead cards, the flop
has C(43,2)=903 possible remaining boards and the turn has 42 rivers. Two known
reserved cards reduce those counts to C(41,2)=820 and 40, before further discards.
Compute counts from the actual eligible cards, not hardcoded constants. Begin preflop
sampling around 10,000 outcomes and profile quality and latency. Future optional swaps
require a separate model of the player's swap policy; ordinary equity treats the
current board as fixed and completes the remaining board without discretionary swaps.

For asynchronous calculations, pass copied card IDs rather than mutable `Card`
references. Apply UI changes on Unity's main thread. Give requests state versions and
discard stale results. Use deterministic seeds for reproducible debugging; use shared
samples where appropriate to reduce noise in before/after estimates.

## Event history and suspicion

Store hand ID, event ID, street, event type, before/after board, active player IDs,
per-player win/tie/equity estimates, viewpoint, assumed ranges, sample count, and
whether results are exact or sampled. Record pot, bets, raises, call amounts, and
newly revealed information alongside events. Keep authoritative cheat details out of
enemy observation inputs.

Compare original and replacement cards at the **same street** when measuring a swap's
effect. Comparing pre-turn with post-swap turn also measures the ordinary reveal.

## Match rules

One successful swap adds its sequence number across the match. The first costs 1,
the next 2. Suspicion and the sequence persist in memory until a new match.

| Losing enemy at showdown | Trips | Straight | Flush | Full house | Quads | Straight flush |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Suspicion added | 5 | 6 | 7 | 8 | 9 | 10 |

Score every qualifying enemy once, including losses to another enemy on a natural
board. Folded seats and winners of any contested pot share score zero. A lone
contributor receives a refund and can still score for a losing hand.

Fold wins pay immediately. At showdown, divide contributions into main and side
pots by contribution level. Folded chips fund each applicable layer. Eligible
live winners split each layer evenly, with odd chips awarded in ascending seat
order. Bankrupt seats keep their indices and sit out subsequent deals.

A swap reaching 100 ends the match immediately. After showdown payouts and all
coolers, resolve enemy bankruptcy victory first, reaching 100 suspicion second,
and controlled-player bankruptcy third. The meter caps at 100.

## Enemy betting

Each enemy turn draws a fresh 2–5 second delay. During that delay, the sampler
draws uniformly random live opponent hands and completes the visible board without
replacement. All-in opponents participate and ties split the showdown share. Work
runs in roughly 1 ms slices per frame, up to 10,000 outcomes. At the deadline the
policy uses the completed estimate. The same seat reuses its estimate on a re-raise
if its cards, board and live opponents still match. A swap restarts a pending turn.

The hand ceiling in chips is the stack at the deal multiplied by
`min(1, equity² × live player count × risk)`, rounded down. Risk combines a
match-long seat preference with a small adjustment sampled once per hand. Subtract
all chips already committed, including earlier streets, to find the remaining
budget. Lower equity can leave an enemy able only to check or fold.

`PokerEnemyPolicy` weights legal actions using equity, the payable call divided
by the eligible pot after calling, aggression, seat position, and raises on the
street. The bluff weight falls with more opponents and betting pressure, and
vanishes against an all-in opponent. Raises may use the legal minimum, half the
pot after calling, or the full pot after calling, subject to stack and budget.
The random source for enemy play is independent of the shuffled game deck.

## OMPEval and performance

Start with the C# evaluator's at-most-21 five-card combinations per seven-card hand.
Profile the equity workload before optimizing or introducing native builds. OMPEval
is a useful independent comparison oracle or a future backend. Its equity calculator
supports up to six players. Unity integration would need a C ABI wrapper, platform
libraries, and C# interop; keep the equity interface independent of that choice.
Retain OMPEval and bundled dependency notices if distributing their code.

References:
- https://github.com/zekyll/OMPEval
- https://docs.unity.com/en-us/engine/6000.0/manual/programming-environment/plug-ins/native

## Running comparison checks

For an interactive check in Unity, open `Assets/Scenes/PokerEvaluatorTest.unity` and
enter Play mode. See [Evaluator test scene](EvaluatorTestScene.md) for the controls.

With the .NET 10 SDK, run `dotnet run --project Tests/PokerHandEvaluatorChecks -c Release`
from the project root. This dependency-free console harness compiles the production evaluator and
`Card.cs` directly, with an empty Unity namespace stub for Card's unused import.
It checks comparisons, equity, state transitions, betting, swaps, card/chip
conservation, input validation, and all 2,598,960 five-card hands.
It does not launch Unity or exercise scene/UI integration.
