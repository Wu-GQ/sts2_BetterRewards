# BetterRewards

This repository is a fork of [customjack/sts2_BetterRewards](https://github.com/customjack/sts2_BetterRewards) by Jack Carlton. Gameplay is his. This fork replaces the ModManagerSettings dependency with [BaseLib](https://github.com/Alchyr/BaseLib-StS2). It is not the upstream project, and it is not published on the Steam Workshop.

The original mod is MIT licensed. Copyright (c) 2026 Jack Carlton. See [LICENSE](LICENSE).

Adds a post-Neow event where you can sacrifice HP for gold, then spend it in a special shop with extra options beyond the normal merchant.

**Features:**
- After the Neow event, choose how much HP to sacrifice for gold
- Special shop with configurable extra actions:
  - **Reroll Shop** — replace the merchant stock with a fresh roll
  - **Random Relic** — buy a random relic at configurable rarity weights
  - **Random Item** — buy a random relic, card, or potion
- Costs and weights are configurable through BaseLib's mod config screen

**Requires:** [BaseLib](https://github.com/Alchyr/BaseLib-StS2) ([Steam Workshop item 3737335127](https://steamcommunity.com/sharedfiles/filedetails/?id=3737335127)). This fork targets BaseLib 3.4.8 or newer. That release also ships `BaseLibCompatibilityHelper`, which BaseLib itself depends on. Subscribe on the Workshop, or download the GitHub release and install every file it contains.

## Install

1. Install BaseLib first (Workshop subscribe, or the GitHub release files in the game `mods` folder). Launch once and confirm BaseLib loads. BaseLib 3.4.8 will not load without `BaseLibCompatibilityHelper`.
2. Close Slay the Spire 2.
3. In Steam, right-click `Slay the Spire 2` -> `Properties` -> `Installed Files` -> `Browse`.
4. Create a `mods` folder in the game directory if it does not exist.
5. Build this fork (see below) or use a build you produced yourself. Copy the staged `dist/BetterRewards` folder to `mods/BetterRewards`.
6. Confirm these files are present in `mods/BetterRewards`:
   - `BetterRewards.dll`
   - `BetterRewards.json`
   - `BetterRewards.pck` (mod icon; BaseLib's config list expects it)
   - `mod_image.png`
7. Launch Slay the Spire 2. If prompted to enable mods, accept and relaunch.
8. In-game, enable `BetterRewards`. Open BaseLib's mod config (main menu or the in-game settings entry BaseLib adds) and select BetterRewards.

Do not publish this fork to the Steam Workshop from this tree.

## Usage

After defeating Neow, a prompt appears letting you sacrifice HP for gold. The amount you can sacrifice and the gold conversion rate are both configurable. Once you have gold, you can spend it in the special shop at any point during the run.

Open the shop from the in-run menu and use the extra action buttons to reroll, buy a random relic, or buy a random item.

## Configuration

Settings are BaseLib `SimpleModConfig` sliders. They are grouped into Event, shop reroll, random relic, and random item sections. Weight groups start collapsed.

BaseLib saves them to `mod_configs/BetterRewards.cfg` in the game's user data folder. On Windows that is `%AppData%\SlayTheSpire2\mod_configs\BetterRewards.cfg`. Values saved by ModManagerSettings are not imported; set them again once.

| Setting | Default | Range |
|---|---:|---|
| HP Sacrifice % | 0.20 | 0.00–1.00, step 0.01 |
| Sacrifice Repeats | 4 | 1–999 |
| Score → Gold % | 0.25 | 0.00–100.00, step 0.05 |
| Minimum Previous Score | 1000 | 0–100000, step 50 |
| Reroll Base Cost | 150 | 0–9999, step 5 |
| Reroll Cost Scaling | 1.10 | 1.00–5.00, step 0.05 |
| Random Relic Base Cost | 300 | 0–9999, step 5 |
| Random Relic Cost Scaling | 1.10 | 1.00–5.00, step 0.05 |
| Relic weights (Common / Uncommon / Rare / Shop / Ancient) | 10 / 20 / 35 / 10 / 25 | 0–1000 |
| Random Item Base Cost | 75 | 0–9999, step 5 |
| Random Item Cost Scaling | 1.10 | 1.00–5.00, step 0.05 |
| Item type weights (Relic / Card / Potion) | 15 / 50 / 35 | 0–1000 |
| Card weights (Common / Uncommon / Rare / Colorless / Curse) | 55 / 25 / 10 / 5 / 5 | 0–1000 |
| Random-item relic weights (Common / Uncommon / Rare / Shop / Ancient) | 50 / 33 / 17 / 5 / 2 | 0–1000 |
| Potion rarity weights (Common / Uncommon / Rare) | 65 / 25 / 10 | 0–1000 |

The potion rarity weights are kept so the old settings screen is fully represented. The random-item potion button still asks the game's potion factory for a potion and does not apply those weights. That was already the case before this port.

### Multiplayer

ModManagerSettings marked every numeric setting with host overwrite. BaseLib 3.4.8 stores each player's config locally and does not sync it to the host or the clients. This fork does not add a separate sync system. In multiplayer, each player uses their own saved costs and weights. Previous-run score sharing is unchanged.

## Developer notes

**Requirements:** .NET 9 SDK, a local Slay the Spire 2 install (public 0.107.x or newer; the manifest asks for `0.107.1`), and BaseLib 3.4.8 or newer. This tree does not vendor `sts2.dll` or `BaseLib.dll`.

**Setup:**
1. Copy `.env.example` to `.env`.
2. Set `STS2_INSTALL_DIR` to the game install (the folder that contains `data_sts2_windows_x86_64`).
3. Install BaseLib so `BaseLib.dll` is at `$STS2_INSTALL_DIR/mods/BaseLib/BaseLib.dll` or `$STS2_INSTALL_DIR/mods/BaseLib.dll`. If it lives somewhere else, set `BASELIB_DIR` to that folder.

**Build and install:**
```bash
./scripts/bash/build_and_stage.sh
./scripts/bash/install_to_game.sh
```

`build_and_stage.sh` compiles `BetterRewards.dll` against the game assemblies and `BaseLib.dll`, then copies the dll, `BetterRewards.json`, `BetterRewards.pck`, and `mod_image.png` into `dist/BetterRewards`. The pack only contains the mod icon. Rebuild it with `./scripts/bash/make_pck.sh` if a newer game rejects the committed pack. That script needs Godot.

**What to test in game:**
1. The mod list shows BetterRewards, and BaseLib's config screen lists it with the existing labels (HP Sacrifice %, shop costs, weight sections).
2. Change Minimum Previous Score, restart, and confirm the value survived in `mod_configs/BetterRewards.cfg`.
3. Start a run, take the Neow bargain, and confirm HP loss and gold still follow the sliders.
4. Open the special shop and use reroll, random relic, and random item. Costs should scale from the configured base and multiplier.
5. In a multiplayer lobby, confirm each client keeps its own config. Do not expect the host's settings to overwrite yours.

Harmony patches for the Neow option, the special shop, and previous-run score sync are unchanged aside from reading the BaseLib-backed properties.

## License

MIT — Copyright (c) 2026 Jack Carlton. See [LICENSE](LICENSE).
