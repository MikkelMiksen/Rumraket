# UI contract

Element names are what the UI class queries with `Q<T>("name")`. Class names are toggled from code.
Names are unique per page. Inside a template instance (cards, readouts, rows), query from the instance root: `root.Q("fuelCard").Q<Label>("cardName")`.

## Setup
- Folder: `Assets/UI/` with `Pages/` and `Templates/` beside `Main.uxml` and `Theme.uss`.
- UIDocument Source Asset: `Main.uxml`. Panel Settings: Scale With Screen Size, reference 1920x1080, Match 0.5. Leave Clear Color off so the 3D scene shows through.

## Pages (children of `root` in Main.uxml)
Show or hide a page by toggling the class `page--hidden`. Start state: only `pageTitle` visible.

| State | Page element | Elements to bind |
|---|---|---|
| Title | `pageTitle` | `playButton` (click) |
| LaunchPad | `pageLaunchPad` | `upgradesButton`, `launchButton` (click); `creditsLabel`; journey: `journeyText`, `journeyProgress` (0..100), `journeyFromLabel`, `journeyToLabel` |
| UpgradeHub | `pageUpgradeHub` | `backButton` (click); `creditsLabel`; `statsLabel`; cards `fuelCard`, `engineCard` |
| Flying | `pageHud` | readouts `speedReadout`, `heightReadout` (each has `readoutLabel`, `readoutValue`); journey: `journeyText`, `journeyProgress`, `journeyFromLabel`, `journeyToLabel`; `fuelBar` (0..100), `fuelValue` |
| RunSummary | `pageSummary` (shown on top of `pageHud`) | `distanceRow`, `speedRow` (each has `rowLabel`, `rowRaw`, `rowPoints`); `newBestLabel` (toggle `hidden`); `totalLabel`; `creditsEarnedLabel`; `continueButton` (click) |

## Upgrade card (template `UpgradeCard`)
`cardName`, `cardEffect`, `cardCost`, `cardMessage` (labels), `buyButton` (click), five `.pip` elements inside `cardPips`.
Level display: add the class `pip--filled` to the first N pips (query `.pip`).

## Adding things later
- New HUD readout: add `Readout.uxml` as a new instance inside `readoutList` (or clone the template from code).
- New upgrade: add another `UpgradeCard` instance, or clone the template into a card column.
- New breakdown row: add a `BreakdownRow` instance inside `breakdownList`.
- New page: new file in `Pages/`, one `Template` and one `Instance` line in `Main.uxml`.
