# BetterRewards

This repository is a fork of [customjack/sts2_BetterRewards](https://github.com/customjack/sts2_BetterRewards) by Jack Carlton. Gameplay is his. This fork replaces the ModManagerSettings dependency with [BaseLib](https://github.com/Alchyr/BaseLib-StS2). It is not the upstream project, and it is not published on the Steam Workshop.

The original mod is MIT licensed. Copyright (c) 2026 Jack Carlton. See [LICENSE](LICENSE).

Adds a bargain on the opening ancient (not only Neow) where you can sacrifice HP for gold, then spend it in a special shop with extra options beyond the normal merchant.

**Features:**
- On the opening ancient, choose how much HP to sacrifice for gold
- Special shop with extra actions:
  - **Reroll Shop** — replace the merchant stock with a fresh roll
  - **Random Relic** — buy a random relic at the regular shop's rarity odds, with a base price that increases by a fixed amount after each purchase
  - **Random Item** — buy a random relic, card, or potion at the regular shop's stock ratios and rarity odds, with a base price that increases by a fixed amount after each purchase
- Reroll, random relic, and random item costs, and the sacrifice rate, are configurable through BaseLib's mod config screen
- Event, settings, and shop text follow the game's official language

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

On the run's opening ancient, a bargain option appears letting you sacrifice HP for gold, even if another mod has replaced Neow. The amount you can sacrifice and the gold conversion rate are both configurable. After the bargain, you enter a special merchant shop with extra buttons to reroll, buy a random relic, or buy a random item.

## Configuration

Settings are BaseLib `SimpleModConfig` sliders in four sections: Event, Shop reroll, Random relic, and Random item.

BaseLib saves them to `mod_configs/BetterRewards.cfg` in the game's user data folder. On Windows that is `%AppData%\SlayTheSpire2\mod_configs\BetterRewards.cfg`. Values saved by ModManagerSettings are not imported; set them again once.

| Setting | Default | Range |
|---|---:|---|
| HP Sacrifice % | 0.20 | 0.00–1.00, step 0.01 |
| Sacrifice Repeats | 4 | 1–10 |
| Score → Gold % | 0.25 | 0.00–1.00, step 0.01 |
| Minimum Previous Score | 1000 | 1000–10000, step 50 |
| Reroll Base Cost | 150 | 50–1000, step 5 |
| Reroll Cost Increment | 50 | 0–500, step 5 |
| Random Relic Base Cost | 300 | 50–1000, step 5 |
| Random Relic Cost Increment | 50 | 0–500, step 5 |
| Random Item Base Cost | 75 | 50–1000, step 5 |
| Random Item Cost Increment | 25 | 0–500, step 5 |

Random relic and random item rarity still follows the regular merchant shop, and random item types still use the 3:7:3 stock ratio. Their prices are a configurable base cost that increases by a fixed amount after each purchase, not the official price of the rolled item.

### Multiplayer

ModManagerSettings marked every numeric setting with host overwrite. BaseLib 3.4.8 stores each player's config locally and does not sync it to the host or the clients. This fork does not add a separate sync system. In multiplayer, each player uses their own saved reroll, random relic, random item, and sacrifice values. Previous-run score sharing is unchanged.

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
1. The mod list shows BetterRewards, and BaseLib's config screen lists the Event, Shop reroll, Random relic, and Random item sliders.
2. Change Minimum Previous Score, restart, and confirm the value survived in `mod_configs/BetterRewards.cfg`.
3. Start a run, take the opening-ancient bargain, and confirm HP loss and gold still follow the sliders. The option should appear even if another mod replaces Neow.
4. Open the special shop and use reroll, random relic, and random item. Costs should start at the configured base and rise by the configured increment after each use.
5. In a multiplayer lobby, confirm each client keeps its own config. Do not expect the host's settings to overwrite yours.

Harmony patches for the opening-ancient option, the special shop, and previous-run score sync now read the BaseLib-backed properties.

## License

MIT — Copyright (c) 2026 Jack Carlton. See [LICENSE](LICENSE).
