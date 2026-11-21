# BetterRewards

**English** | [简体中文](README.zh-CN.md)

This repository is a fork of [customjack/sts2_BetterRewards](https://github.com/customjack/sts2_BetterRewards) by Jack Carlton. Gameplay is his. This fork uses [RitsuLib](https://github.com/BAKAOLC/STS2-RitsuLib) for settings and in-run multiplayer sync. It is not the upstream project.

The whole concept comes from [Skrelpoid](https://github.com/Skrelpoid)'s [BetterRewards for Slay the Spire 1](https://steamcommunity.com/sharedfiles/filedetails/?id=1613185732) ([source](https://github.com/Skrelpoid/BetterRewardsMod)): trading your Neow reward for gold and a special starting shop. Jack Carlton's STS2 version is what this fork builds on.

Issues and pull requests are welcome: bug reports, balance feedback, translation fixes and new ideas. Please attach `godot.log` when reporting a crash or a desync.

The original mod is MIT licensed. Copyright (c) 2026 Jack Carlton. See [LICENSE](LICENSE).

Adds a bargain on the opening ancient where you can sacrifice HP for gold, then spend it in a special shop.

**Features:**
- On the opening ancient, choose how much HP to sacrifice for gold
- Special shop with extra actions:
  - **Reroll Shop** — replace the merchant stock with a fresh roll
  - **Random Relic** — buy a random relic at a base price that increases by a fixed amount after each purchase
  - **Random Item** — buy a random relic, card, or potion at a base price that increases by a fixed amount after each purchase
- Reroll, random relic, and random item costs, and the sacrifice rate, are configurable through RitsuLib's mod settings screen
- Event, settings, and shop text follow the game's official language

**Requires:** [RitsuLib](https://github.com/BAKAOLC/STS2-RitsuLib) ([Steam Workshop item 3747602295](https://steamcommunity.com/sharedfiles/filedetails/?id=3747602295)). This fork targets RitsuLib 0.6.14 or newer. Subscribe on the Workshop, then enable `STS2-RitsuLib` in the mod list.

## Install

**From the Steam Workshop (recommended):** subscribe to [BetterRewards on the Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3747602295) — RitsuLib comes with it as a dependency — then enable both mods in the in-game mod list.

**From a local build:**
1. Subscribe to RitsuLib on the Workshop first. Launch once and confirm `STS2-RitsuLib` loads.
2. Close Slay the Spire 2.
3. In Steam, right-click `Slay the Spire 2` -> `Properties` -> `Installed Files` -> `Browse`.
4. Create a `mods` folder in the game directory if it does not exist.
5. Build this fork (see below) or use a build you produced yourself. Copy the staged `dist/BetterRewards` folder to `mods/BetterRewards`.
6. Confirm these files are present in `mods/BetterRewards`:
   - `BetterRewards.dll`
   - `BetterRewards.json`
   - `BetterRewards.pck` (mod icon)
   - `mod_image.png`
7. Launch Slay the Spire 2. If prompted to enable mods, accept and relaunch.
8. In-game, enable `BetterRewards`. Open RitsuLib's mod settings (the main-menu shortcut or the pause-menu entry) and select BetterRewards.

## Usage

On the run's opening ancient, a bargain option appears letting you sacrifice HP for gold. The amount you can sacrifice and the gold conversion rate are both configurable. After the bargain, you enter a special merchant shop with extra buttons to reroll, buy a random relic, or buy a random item.

## Configuration

Settings are RitsuLib sliders in four sections: Event, Shop reroll, Random relic, and Random item.

RitsuLib saves them as `settings.json` in the game's user data folder. On Windows that is `%AppData%\SlayTheSpire2\steam\<your Steam ID>\mod_data\BetterRewards\settings.json`. Existing BaseLib `mod_configs/BetterRewards.cfg` files are not imported automatically; set the sliders again after switching.
| Setting | Default | Range |
|---|---:|---|
| HP Sacrifice % | 0.20 | 0.00–1.00, step 0.01 |
| Sacrifice Repeats | 4 | 1–10 |
| Score → Gold % | 0.50 | 0.01–1.00, step 0.01 |
| Minimum Previous Score | 1000 | 0–2000, step 50 |
| Reroll Base Cost | 150 | 50–1000, step 5 |
| Reroll Cost Increment | 50 | 0–500, step 5 |
| Random Relic Base Cost | 300 | 50–1000, step 5 |
| Random Relic Cost Increment | 50 | 0–500, step 5 |
| Random Item Base Cost | 75 | 50–1000, step 5 |
| Random Item Cost Increment | 25 | 0–500, step 5 |

Random relic and random item prices are a configurable base cost that increases by a fixed amount after each purchase, not the official price of the rolled item.

### Multiplayer

In multiplayer, the host's settings are copied into the run when it starts and then shared through RitsuLib. Clients use that host snapshot for sacrifice and shop prices during the run; their local settings files are not overwritten. Previous-run score sharing is unchanged.

**Each player keeps their own shop.** Slay the Spire 2 builds a separate `MerchantInventory` per player from that character's card pool and that player's `PlayerRng.Shops`, so two players see different stock and have different shelf layouts. The special shop follows that: pressing reroll, random relic, or random item only changes *your* shelves, and the price ladder is tracked per player. Other players still see their own prices and their own stock.

What is shared, and therefore replayed on every peer, is the run state the purchase touches: gold spent, the relic or card or potion you received, the use counts behind your price ladder, and the relic ids the action pulled out of the grab bags. The game checksums the per-player relic grab bag, so those ids travel with the action instead of being re-derived; otherwise the bags would drift apart and the session could be flagged as diverged.

## Developer notes

**Requirements:** .NET 9 SDK, a local Slay the Spire 2 install (the manifest asks for `0.113.1`; this fork was built and tested on `v0.113.1`), and RitsuLib 0.6.14 or newer. This tree does not vendor `sts2.dll` or RitsuLib assemblies.

**Setup:**
1. Copy `.env.example` to `.env`.
2. Set `STS2_INSTALL_DIR` to the game install (the folder that contains `data_sts2_windows_x86_64`).
3. Install RitsuLib so `RitsuLib.References.props` is at `$STS2_INSTALL_DIR/mods/STS2-RitsuLib/RitsuLib.References.props`, or in the Steam Workshop download for item 3747602295. If it lives somewhere else, set `RITSULIB_DIR` to that folder.

**Build and install:**
```bash
./scripts/bash/build_and_stage.sh
./scripts/bash/install_to_game.sh
```

`build_and_stage.sh` compiles `BetterRewards.dll` against the game assemblies and RitsuLib, then copies the dll, `BetterRewards.json`, `BetterRewards.pck`, and `mod_image.png` into `dist/BetterRewards`. The pack holds only the mod icon, at the one path the game reads it from (`res://BetterRewards/mod_image.png`). Rebuild it with `./scripts/bash/make_pck.sh` after changing `mod_image.png`, or if a newer game rejects the committed pack. That script needs Godot 4.5.

**What to test in game:**
1. The mod list shows BetterRewards, and RitsuLib's settings screen lists the Event, Shop reroll, Random relic, and Random item sliders.
2. Change Minimum Previous Score, restart, and confirm the value survived in `mod_data/BetterRewards/settings.json`.
3. Start a run, take the opening-ancient bargain, and confirm HP loss and gold still follow the sliders.
4. Open the special shop and use reroll, random relic, and random item. Costs should start at the configured base and rise by the configured increment after each use.
5. In a multiplayer run, confirm clients use the host's sacrifice and shop-price settings. Local settings files on clients should stay unchanged.

Harmony patches for the opening-ancient option, the special shop, and previous-run score sync now read the RitsuLib-backed settings, and in multiplayer they follow the host snapshot for that run.

## License

MIT — Copyright (c) 2026 Jack Carlton. See [LICENSE](LICENSE).
