# Game state and street progression

`PokerGameState` is the authoritative game model. `DealerManager` adapts it to the
main scene's optional TextMeshPro labels and buttons. `PokerEvaluatorTestScene` uses
the same model for an interactive debug view. Existing `Player` and `Table` classes
remain available for older code; they are not authoritative copies of the new state.

## Progression

`StartNewHand()` shuffles 52 cards, deals two hole cards per funded seat, reserves two more
cards for the controlled player, and starts preflop with an empty board. The default
table has three players, 1,000 chips each, player zero controlled, and a 20-chip
minimum opening bet. Constructor parameters allow two to ten players and a seeded
`System.Random` for deterministic tests.

The default custom rule requires an opening bet **only preflop**.
On preflop, `CanCheckOrCall` is false until someone bets; attempting that action in the model
throws without changing chips or turn order. The UI disables the button and shows
`Aguardando aposta`, with `Apostar 20` available instead. After an opening bet, the
button shows `Pagar` with the amount still owed. Folding remains available.
On the flop, turn, and river, players may check normally if nothing is owed. A later
bet or raise returns action to previous checkers/callers before the street can end.
For standard no-bet checking in tests or another mode, construct the model with
`requiresOpeningBet: false`.

Each street starts action at the first non-folded seat with chips (seat zero when
eligible). `CheckOrCall`, `BetOrRaiseTo`, and `Fold` enforce action order. Full raises
reopen action for other eligible players. The next street is blocked until everyone
has responded to the latest bet/raise, including players who previously checked or
called. Returning callers pay only the difference from their street contribution.
Folded and all-in seats are skipped. A sole remaining player ends
the hand immediately. With no further betting possible, streets can run out through
the next-street control. Preflop advances to a three-card flop, then one turn, one
river, and finally showdown after river betting.

The model tracks street contributions separately from total contributions. Street
bets reset on a reveal; the pot and total contributions persist. A raise target is
the seat's **total contribution on this street**, not the extra amount to pay.

```csharp
var game = new PokerGameState();
game.StateChanged += change => RefreshUI(game.CaptureSnapshot());
game.StartNewHand();
game.BetOrRaiseTo(0, 20);
game.CheckOrCall(1);
game.CheckOrCall(2);
game.AdvanceStreet(); // flop; new betting round
```

## Card ownership and swaps

Live cards are private to the model. `CaptureSnapshot()` returns detached card
copies and read-only collections of seats, board, reserved cards, and discards.
Editing a snapshot's mutable `Card` fields cannot change the game. The deck is
private, draws each physical card once, and receives no discarded or reserved cards.
Hole cards remain assigned to folded seats. Known cards plus deck count total 52.

`SwapCurrentStreetCard(reservedIndex)` is available only on the fresh turn or river,
before the first betting action, at most once on each street. It consumes the chosen
reserved card and moves the replaced board card to discards. Later reserved indices
shift down when a card is consumed. The controlled seat must still be in the hand.
A swap does not spend chips, change turn order, or draw from the deck.

Every successful mutation raises `StateChanged` with hand number, monotonically
increasing state version, street, event type, player index, and paid amount. Swap
events additionally carry board index, reserved index, replacement, and replaced
card copies. Invalid actions throw before mutation and do not emit events.

These events are the extension point for step four's before/after equity history.
Snapshots include shared suspicion, successful swap count, match outcome, elimination flags and the most recent settled payouts. Scoring and payouts finish before the action event reaches listeners.

## Match scoring and payouts

Each match starts at 0/100 suspicion. Successful swap number *n* adds *n*
points. Two swaps cost 3 total. One swap per hand over ten hands costs 55.
Both counters carry forward without decay. A swap reaching 100 ends play on
that street, with the unfinished pot still on the table.

At showdown, score each losing enemy once, even on an untouched board or when
another enemy wins. Folding or winning any share of a contested pot exempts that
seat. A refund of an uncalled bet leaves the hand eligible for scoring.

| Losing enemy hand | Suspicion |
| --- | ---: |
| High card through two pair | 0 |
| Three of a kind | 5 |
| Straight | 6 |
| Flush | 7 |
| Full house | 8 |
| Four of a kind | 9 |
| Straight flush | 10 |

A fold win awards the entire pot immediately. At showdown, each distinct
contribution level forms a pot layer. Every contributor funds the layer up to
that level, including folded seats. The strongest eligible live hands share it
evenly, with odd chips going to tied winners in ascending seat order. A
single-contributor layer returns to its owner. Settlement credits the stacks,
empties the pot, and retains the hand's contributions for inspection.

After payouts and cooler scoring, first award victory if all enemies are
bankrupt, then resolve a full suspicion meter as caught, then resolve a
bankrupt controlled seat as a loss. Suspicion caps at 100. A short stack
can open preflop all-in below the table minimum.

`StartNewHand()` carries stacks forward and skips bankrupt seats for cards
and actions while keeping their indices. After a terminal outcome,
`StartNewMatch()` restores every starting stack, clears suspicion, swap count
and payouts, and deals hand one. Match state lives in memory.

## Unity controls

`DealerManager.PlayRound()` starts a fresh preflop hand. Its `AdvanceStreet()`,
`CheckOrCall()`, `RaiseMinimum()`, `FoldCurrentPlayer()`, and `SwapReservedCard(int)`
methods can be connected to UI. Optional button fields are wired at startup. Assign
only those fields or Inspector event listeners, not both. Debug context-menu commands
on the component allow advancing and taking controlled-player actions without scene buttons. Existing labels
continue displaying hands and board, and optional fields add street, reserved cards,
discards, pot, and acting-player information.

The main `NagaTeste` scene had two DealerManager components on the dealer object;
the unused duplicate was removed. Existing game UI references remain on the retained
component. It now starts with no community cards rather than dealing all five.

For the full interactive controls, use [the evaluator test scene](EvaluatorTestScene.md).

## Equity and knowledge

The test scene recalculates after state changes. It removes folded players from
the comparison and includes their known cards, reserved cards, and discards in the
dead-card list. With three active players and two reserved cards, the flop has 820
possible final boards and the turn has 40 possible rivers. A consumed reserve becomes
a board card while the displaced card remains dead. Fold winners have 100% equity;
folded players have zero. Preflop displays a placeholder until sampling is added.

Snapshots and events contain authoritative secret information. The test view is
deliberately omniscient. Enemy turns use a separate filtered observation with the acting seat’s
hole cards, visible board, live opponents and public betting information. Card `faceUp` flags are debug data, not access control.

## Current prototype limits

- No blinds, antes, dealer rotation, or burn cards.
- Full minimum raises and all-in calls are supported; short all-in raises are not.
- The evaluator scene defaults to automatic enemy turns. Its **Controle: IA** button
  switches to manual testing for every seat.
- Equity is showdown share, not a payout calculation for differing pot eligibility.
- Equity-event history is still pending.

Run `dotnet run --project Tests/PokerHandEvaluatorChecks -c Release` with .NET 10 for
state, betting, swap, equity, and hand-comparison checks, including 100 random complete
hands and conservation of all cards and chips.
