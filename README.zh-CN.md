# BetterRewards

[English](README.md) | **简体中文**

This repository is a fork of Jack Carlton's [customjack/sts2_BetterRewards](https://github.com/customjack/sts2_BetterRewards). Gameplay is his. This fork uses [RitsuLib](https://github.com/BAKAOLC/STS2-RitsuLib) for settings and in-run multiplayer sync. It is not the upstream project.

整个玩法概念来自 [Skrelpoid](https://github.com/Skrelpoid) 的[杀戮尖塔 1 版 BetterRewards](https://steamcommunity.com/sharedfiles/filedetails/?id=1613185732)（[源码](https://github.com/Skrelpoid/BetterRewardsMod)）：用尼欧的奖励换取金币并进入特殊商店。本 fork 基于 Jack Carlton 的《杀戮尖塔 2》版本。

欢迎提交 issue 与 pull request：Bug 反馈、平衡建议、翻译修正、新点子。反馈崩溃或联机分歧时请附上 `godot.log`。

原模组采用 MIT 许可。Copyright (c) 2026 Jack Carlton。详见 [LICENSE](LICENSE)。

在开局先古之民处增加一项交易：可以牺牲生命换取金币，然后在特殊商店中消费。

**功能：**
- 在开局先古之民处，选择牺牲多少生命来换取金币
- 特殊商店提供额外操作：
  - **刷新商店** — 重新生成商人货架
  - **随机遗物** — 购买一件随机遗物，售价为可配置的基础价格，每次购买后增加固定加值
  - **随机物品** — 购买一件随机遗物、卡牌或药水，售价同样是可配置的基础价格，每次购买后增加固定加值
- 刷新、随机遗物、随机物品的花费，以及牺牲比例，都可在 RitsuLib 的模组设置界面中调整
- 事件、设置和商店文本会跟随游戏的官方语言

**前置依赖：** [RitsuLib](https://github.com/BAKAOLC/STS2-RitsuLib)（[Steam 创意工坊物品 3747602295](https://steamcommunity.com/sharedfiles/filedetails/?id=3747602295)）。本 fork 需要 RitsuLib 0.6.14 或更高版本。请在创意工坊订阅，然后在模组列表中启用 `STS2-RitsuLib`。

## 安装

**从创意工坊安装（推荐）：** 订阅 [创意工坊上的 BetterRewards](https://steamcommunity.com/sharedfiles/filedetails/?id=3747602295)——RitsuLib 会作为依赖一并安装——然后在游戏内模组列表中同时启用两者。

**从本地构建安装：**
1. 先在创意工坊订阅 RitsuLib。启动一次游戏，确认 `STS2-RitsuLib` 已加载。
2. 关闭《杀戮尖塔 2》。
3. 在 Steam 中，右键 `Slay the Spire 2` -> `属性` -> `已安装文件` -> `浏览`。
4. 如果游戏目录下还没有 `mods` 文件夹，请先创建。
5. 编译本 fork（见下文），或使用你自己打出的构建。将暂存目录 `dist/BetterRewards` 复制到 `mods/BetterRewards`。
6. 确认 `mods/BetterRewards` 中包含这些文件：
   - `BetterRewards.dll`
   - `BetterRewards.json`
   - `BetterRewards.pck`（模组图标，以导入后的 `.ctex` 形式打包）
7. 启动《杀戮尖塔 2》。如果提示启用模组，请接受并重新启动。
8. 在游戏中启用 `BetterRewards`。打开 RitsuLib 的模组设置（主菜单快捷入口或暂停菜单中的入口），然后选择 BetterRewards。

## 使用

在本局开局先古之民处，会出现一项交易选项，可用生命换取金币。可牺牲的次数和金币兑换比例都可以配置。交易结束后，你会进入一家特殊商人商店，其中有刷新、购买随机遗物、购买随机物品的额外按钮。

## 配置

设置项是 RitsuLib 滑条，分为四个分区：事件、商店刷新、随机遗物、随机物品。

RitsuLib 会把它们保存为游戏用户数据目录下的 `settings.json`。在 Windows 上路径是 `%AppData%\SlayTheSpire2\steam\<你的 Steam ID>\mod_data\BetterRewards\settings.json`。切换后不会自动导入原有的 BaseLib `mod_configs/BetterRewards.cfg`，需要重新设置滑条。
| 设置项 | 默认值 | 范围 |
|---|---:|---|
| 生命牺牲比例 | 0.20 | 0.00–1.00，步长 0.01 |
| 可牺牲次数 | 4 | 1–10 |
| 得分兑换金币比例 | 0.25 | 0.01–1.00，步长 0.01 |
| 最低上一局得分 | 1000 | 0–2000，步长 50 |
| 刷新基础花费 | 150 | 50–1000，步长 5 |
| 刷新花费加值 | 50 | 0–500，步长 5 |
| 随机遗物基础花费 | 300 | 50–1000，步长 5 |
| 随机遗物花费加值 | 50 | 0–500，步长 5 |
| 随机物品基础花费 | 75 | 50–1000，步长 5 |
| 随机物品花费加值 | 25 | 0–500，步长 5 |

随机遗物和随机物品的售价是可配置的基础花费，每次购买后增加固定加值，而不是抽到物品的官方售价。

### 多人游戏

联机时，开局会把房主的设置写入本局数据，并通过 RitsuLib 同步给其他玩家。客户端在本局中使用这份房主快照计算牺牲和商店价格，不会改写他们本地的设置文件。上一局得分同步的方式没有变化。

**每个玩家的商店是独立的。**《杀戮尖塔 2》会按玩家分别创建 `MerchantInventory`，牌池来自该角色的卡池，货架由该玩家自己的 `PlayerRng.Shops` 掷出，所以两名玩家看到的货架内容和布局本来就不同。本 mod 遵循这一点：点"刷新 / 随机遗物 / 随机物品"只改变**你自己**的货架，价格阶梯也按玩家单独记录。别人看到的仍是他们自己的货架和价格。

真正需要全端一致、因而会在每个客户端重放的，是购买所触及的局内状态：花掉的金币、你实际获得的遗物／卡牌／药水、价格阶梯背后的使用次数，以及本次动作从遗物袋里抽走的遗物 id。游戏会对每个玩家的遗物袋做校验和，所以这些 id 必须随消息传递而不能靠各端重算，否则遗物袋会漂移并被判定为状态分歧。

## 开发说明

**环境要求：** .NET 9 SDK、本地安装的《杀戮尖塔 2》（清单要求 `0.113.1`；本 fork 在 `v0.113.1` 上编译并测试），以及 RitsuLib 0.6.14 或更高版本。本仓库不附带 `sts2.dll` 或 RitsuLib 程序集。

**准备：**
1. 将 `.env.example` 复制为 `.env`。
2. 把 `STS2_INSTALL_DIR` 设为游戏安装目录（其中应包含 `data_sts2_windows_x86_64`）。
3. 安装 RitsuLib，使 `RitsuLib.References.props` 位于 `$STS2_INSTALL_DIR/mods/STS2-RitsuLib/RitsuLib.References.props`，或 Steam 创意工坊物品 3747602295 的下载目录。如果文件在其他位置，请将 `RITSULIB_DIR` 设为该文件夹。

**编译与安装：**
```bash
./scripts/bash/build_and_stage.sh
./scripts/bash/install_to_game.sh
```

`build_and_stage.sh` 会针对游戏程序集和 RitsuLib 编译 `BetterRewards.dll`，然后将 dll、`BetterRewards.json` 和 `BetterRewards.pck` 复制到 `dist/BetterRewards`。pck 里用 `.import` 重映射加导入后的 `.ctex` 承载模组图标，Godot 正是通过这条重映射解析 `res://BetterRewards/mod_image.png`，因此不再打包源 PNG，也不再随包附带散装的 `mod_image.png`（没有任何代码读它）。改动 `mod_image.png` 之后，或遇到更新后的游戏拒绝已提交的 pck 时，可用 `./scripts/bash/make_pck.sh` 重新打包。导入器采用 0.85 的有损 WebP，在模组界面 603x380 的实际显示尺寸下肉眼无差别，而 pck 从约 680 KB 降到约 60 KB。该脚本需要 Godot 4.5。

`resources/` 里的内嵌图标刻意做成 256 px 并做调色板量化，因为商店按钮只把图标画在 80x80 的控件里，事件选项图标更是 60x60。新增图标后请运行 `python scripts/tools/optimize_icons.py`，加 `--check` 可只查看当前尺寸。

**建议在游戏中测试：**
1. 模组列表中能看到 BetterRewards，RitsuLib 的设置界面中能看到事件、商店刷新、随机遗物、随机物品这些滑条。
2. 修改「最低上一局得分」，重启后确认 `mod_data/BetterRewards/settings.json` 中的值仍然保留。
3. 开始一局，选择开局先古之民的交易，确认生命损失和金币仍按滑条设置生效。
4. 打开特殊商店，使用刷新、随机遗物和随机物品。花费应从设定的基础价格开始，并在每次使用后按设定的加值上涨。
5. 在多人游戏中，确认客户端使用房主的牺牲和商店价格设置。客户端本地的设置文件应保持不变。

开局先古之民选项、特殊商店，以及上一局得分同步的 Harmony 补丁，现在都会读取由 RitsuLib 提供的设置；联机时则使用本局的房主快照。

## 许可

MIT — Copyright (c) 2026 Jack Carlton。详见 [LICENSE](LICENSE)。
