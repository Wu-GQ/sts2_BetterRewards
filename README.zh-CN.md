# BetterRewards

[English](README.md) | **简体中文**

本仓库是 Jack Carlton 的 [customjack/sts2_BetterRewards](https://github.com/customjack/sts2_BetterRewards) 的 fork。玩法仍属于原作者。本 fork 使用 [RitsuLib](https://github.com/BAKAOLC/STS2-RitsuLib) 管理设置和联机同步。它不是上游项目，也尚未发布到 Steam 创意工坊。

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

1. 先在创意工坊订阅 RitsuLib。启动一次游戏，确认 `STS2-RitsuLib` 已加载。
2. 关闭《杀戮尖塔 2》。
3. 在 Steam 中，右键 `Slay the Spire 2` -> `属性` -> `已安装文件` -> `浏览`。
4. 如果游戏目录下还没有 `mods` 文件夹，请先创建。
5. 编译本 fork（见下文），或使用你自己打出的构建。将暂存目录 `dist/BetterRewards` 复制到 `mods/BetterRewards`。
6. 确认 `mods/BetterRewards` 中包含这些文件：
   - `BetterRewards.dll`
   - `BetterRewards.json`
   - `BetterRewards.pck`（模组图标）
   - `mod_image.png`
7. 启动《杀戮尖塔 2》。如果提示启用模组，请接受并重新启动。
8. 在游戏中启用 `BetterRewards`。打开 RitsuLib 的模组设置（主菜单快捷入口或暂停菜单中的入口），然后选择 BetterRewards。

请不要从这个目录把本 fork 发布到 Steam 创意工坊。

## 使用

在本局开局先古之民处，会出现一项交易选项，可用生命换取金币。可牺牲的次数和金币兑换比例都可以配置。交易结束后，你会进入一家特殊商人商店，其中有刷新、购买随机遗物、购买随机物品的额外按钮。

## 配置

设置项是 RitsuLib 滑条，分为四个分区：事件、商店刷新、随机遗物、随机物品。

RitsuLib 会把它们保存为游戏用户数据目录下的 `settings.json`。在 Windows 上路径是 `%AppData%\SlayTheSpire2\steam\<你的 Steam ID>\mod_data\BetterRewards\settings.json`。切换后不会自动导入原有的 BaseLib `mod_configs/BetterRewards.cfg`，需要重新设置滑条。
| 设置项 | 默认值 | 范围 |
|---|---:|---|
| 生命牺牲比例 | 0.20 | 0.00–1.00，步长 0.01 |
| 可牺牲次数 | 4 | 1–10 |
| 得分兑换金币比例 | 0.25 | 0.00–1.00，步长 0.01 |
| 最低上一局得分 | 1000 | 1000–10000，步长 50 |
| 刷新基础花费 | 150 | 50–1000，步长 5 |
| 刷新花费加值 | 50 | 0–500，步长 5 |
| 随机遗物基础花费 | 300 | 50–1000，步长 5 |
| 随机遗物花费加值 | 50 | 0–500，步长 5 |
| 随机物品基础花费 | 75 | 50–1000，步长 5 |
| 随机物品花费加值 | 25 | 0–500，步长 5 |

随机遗物和随机物品的售价是可配置的基础花费，每次购买后增加固定加值，而不是抽到物品的官方售价。

### 多人游戏

联机时，开局会把房主的设置写入本局数据，并通过 RitsuLib 同步给其他玩家。客户端在本局中使用这份房主快照计算牺牲和商店价格，不会改写他们本地的设置文件。上一局得分同步的方式没有变化。

## 开发说明

**环境要求：** .NET 9 SDK、本地安装的《杀戮尖塔 2》（公开版 0.107.x 或更新；清单要求 `0.107.1`），以及 RitsuLib 0.6.14 或更高版本。本仓库不附带 `sts2.dll` 或 RitsuLib 程序集。

**准备：**
1. 将 `.env.example` 复制为 `.env`。
2. 把 `STS2_INSTALL_DIR` 设为游戏安装目录（其中应包含 `data_sts2_windows_x86_64`）。
3. 安装 RitsuLib，使 `RitsuLib.References.props` 位于 `$STS2_INSTALL_DIR/mods/STS2-RitsuLib/RitsuLib.References.props`，或 Steam 创意工坊物品 3747602295 的下载目录。如果文件在其他位置，请将 `RITSULIB_DIR` 设为该文件夹。

**编译与安装：**
```bash
./scripts/bash/build_and_stage.sh
./scripts/bash/install_to_game.sh
```

`build_and_stage.sh` 会针对游戏程序集和 RitsuLib 编译 `BetterRewards.dll`，然后将 dll、`BetterRewards.json`、`BetterRewards.pck` 和 `mod_image.png` 复制到 `dist/BetterRewards`。pck 包里只有模组图标。如果更新后的游戏拒绝已提交的 pck，可用 `./scripts/bash/make_pck.sh` 重新打包。该脚本需要 Godot。

**建议在游戏中测试：**
1. 模组列表中能看到 BetterRewards，RitsuLib 的设置界面中能看到事件、商店刷新、随机遗物、随机物品这些滑条。
2. 修改「最低上一局得分」，重启后确认 `mod_data/BetterRewards/settings.json` 中的值仍然保留。
3. 开始一局，选择开局先古之民的交易，确认生命损失和金币仍按滑条设置生效。
4. 打开特殊商店，使用刷新、随机遗物和随机物品。花费应从设定的基础价格开始，并在每次使用后按设定的加值上涨。
5. 在多人游戏中，确认客户端使用房主的牺牲和商店价格设置。客户端本地的设置文件应保持不变。

开局先古之民选项、特殊商店，以及上一局得分同步的 Harmony 补丁，现在都会读取由 RitsuLib 提供的设置；联机时则使用本局的房主快照。

## 许可

MIT — Copyright (c) 2026 Jack Carlton。详见 [LICENSE](LICENSE)。
