# Evaluator and game-state test scene

Open `Assets/Scenes/PokerEvaluatorTest.unity` and enter Play mode. The interface is
built at runtime and needs no Inspector wiring or card sprite assets.

## Walk through a hand

1. The scene starts **preflop**: three random two-card hands, two reserved cards for
   Player 1, and an empty board. Everyone starts with 1,000 chips.
2. Open with **Apostar 20** (or fold). This custom mode disables checking before
   anyone bets. Subsequent seats can **Pagar**, raise, or fold. The status line shows
   whose turn it is. After all responses, **Flop** becomes available.
3. On the flop, turn, and river, players may **Check** while no bet is owed. Use
   **Apostar**, **Pagar**, **Aumentar para …**, or **Desistir** as appropriate to finish
   that betting round. Reveals are blocked until all outstanding actions are complete.
   Every raise returns action to all eligible earlier participants, including anyone
   who already called. They must pay the difference, raise again, or fold.
4. Reveal the turn. Before taking any betting action, optionally click **Use card 1**
   or **Use card 2** to replace the turn with a reserved card. The displaced card goes
   to discards, the reserve is consumed, and percentages recalculate immediately.
5. Finish turn betting, reveal the river, and optionally use a remaining reserved
   card to replace the river. Only one replacement is allowed per street.
6. Finish river betting and click **Showdown**. If everyone but one seat folds earlier,
   the hand ends immediately and that player receives 100% equity.
7. **New random hand** reshuffles, resets stacks/pot/discards/folds, deals two new
   reserved cards, and starts preflop again. It may be used at any time in this sandbox.

The betting controls deliberately act for **every current seat** for manual testing.
There is no enemy AI choosing actions yet. Chips and bets are tracked but not paid
out at showdown; side pots and persistent bankrolls remain future work.

## Reading the results

Each player panel shows their cards, remaining chips, street contribution, current
hand category, **equity**, **outright win probability**, and **tie probability**.
Green panels mark current hand leaders (including ties), which can differ from the
highest-equity player before the river. Folded players have zero equity.

Equity includes split-pot shares: a guaranteed three-way tie has 33.3% equity, 0%
outright wins, and 100% ties for each player. These values assume active players
reach showdown; they do not predict future folds or discretionary swaps. Percentages
are rounded to one decimal place. Preflop shows placeholders until sampling is added.

Calculations are exact from the flop onward and use all actual hands. Known reserved
cards, discards, and folded hands are unavailable for future draws. With three active
players and two reserved cards, the flop has **820** possible final boards and the
turn has **40** rivers. The calculation label displays the count. No calculation
consults the shuffled deck's actual future order.

The controller is `Assets/Scripts/Poker/PokerEvaluatorTestScene.cs`, backed by the
same `PokerGameState` as `DealerManager`. See [Game state](GameState.md) for rules,
public methods, events, knowledge boundaries, and current limits.

## Reusing the calculator

`PokerEquityCalculator.Calculate(hands, board, deadCards)` accepts two to ten known
two-card hands, three to five board cards, and optional known unavailable cards.
Results preserve input player order and provide probabilities from zero to one,
win/tie counts, and the total number of evaluated final boards.

```csharp
IReadOnlyList<Card>[] hands = { playerA.hand, playerB.hand, playerC.hand };
PokerEquityResult result = PokerEquityCalculator.Calculate(hands, table.communityCards);
double equityPercent = result.Players[0].Equity * 100;
```

For state snapshots, include only non-folded hands as participants and put reserved
cards, discards, and known folded hands into `deadCards`. A card may appear only once
across all inputs. Handle a sole remaining participant directly as an uncontested
win. The calculator copies cards, does not alter gameplay state, and returns immutable
results suitable for future event history. It has no Unity dependency.
