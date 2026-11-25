# 杀戮尖塔 2 · 遗物升级

「遗物升级」为《杀戮尖塔 2》的单人标准模式加入了 **171 件遗物的升级形态**。战斗胜利后可以领取锻造碎片，并在火堆消耗碎片强化已经持有的遗物。每件升级遗物都有独立的名称、图标和效果，确认前可以查看升级前后的差异。

## 安装

1. 在发布页核对游戏版本和 BaseLib 要求，下载对应的 `RelicForge-*.zip`。[Mod 发布页](https://github.com/qinjiangqj/sts2-relic-upgrades/releases/latest)
2. 安装与游戏版本兼容的 BaseLib。当前发行版要求 BaseLib v3.4.8 或更新的兼容版本。[BaseLib 发布页](https://github.com/Alchyr/BaseLib-StS2/releases)
3. 解压后，将整个 `RelicForge` 文件夹放入游戏安装目录的 `mods`。更新旧版时，将 `RelicForge.dll`、`RelicForge.json` 和 `RelicForge.pck` 一并替换。
4. 重新启动游戏，在 Mod 设置中启用 BaseLib 和「遗物升级 / Relic Upgrades」。

## 锻造碎片

| 战斗类型 | 胜利奖励 |
| --- | --- |
| 普通战斗 | 0 或 1 个 |
| 精英战斗 | 1 或 2 个 |
| 第一、第二幕 Boss | 2 或 3 个 |

每种战斗的两档掉落数量等概率。碎片与金币一样，在战后的「搜刮」界面点击领取。跳过搜刮会放弃未领取的碎片。余额显示在顶部金币旁，并随本局存档保存。

## 如何升级

**在火堆升级。** 持有至少 4 个碎片和一件可升级遗物时，可选择「升级遗物」。勾选「查看升级」可在选择列表中预览新名称和图标。选定一件后，查看两种形态的图标与完整效果。确认后，升级形态会替换原遗物，并消耗 4 个碎片和本次火堆行动。预览期间可以返回重选。

**在先古处免费升级。** 击败第一或第二幕 Boss 并进入下一幕后，在先古处回血、选完祝福并点击继续，即可获得一次免费升级机会。可以直接跳过。

初始遗物可以选择本 Mod 的升级路线，也可以通过奥罗巴斯之触走原版升级路线。两条路线互斥，确认前会显示提示。磨刀石等 144 件遗物继续使用原版效果。

## 遗物示例

| 遗物 | 原版效果 | 升级效果 |
| --- | --- | --- |
| **天赋君权 → 星铸王冠**<br><img src="art/small/divine_right.png" alt="星铸王冠图标" width="68"> | 每场战斗开始时获得 3 颗星。 | 保留开场的 3 颗星。每场战斗第一次用牌花费至少 2 颗星后，返还 1 颗星。 |
| **迷你储君 → 掌中储君**<br><img src="art/small/mini_regent.png" alt="掌中储君图标" width="68"> | 每回合首次花费星星时，获得 1 点力量。 | 保留首次花费星星时的 1 点力量。如果这次花费至少 3 颗星，额外获得 4 点格挡。 |

## 重新构建

安装 .NET 9 SDK 后，在此目录运行 `./build.ps1`。脚本通过 Steam 库定位游戏和 BaseLib，也可以显式指定路径。

```powershell
./build.ps1 -GameDir $gameDir -BaseLibDir $baseLibDir
```

将 `$gameDir` 和 `$baseLibDir` 设为游戏和 BaseLib 的安装目录。构建产物在 `Mod/RelicForge`，首次构建会一并生成 `RelicForge.pck`。源码位于 `src`，图标源文件位于 `art`。

更换图标时，把透明 PNG 放进 `art/small` 和 `art/detail`。文件名使用原遗物 ID 的小写，例如 `anchor.png`。每件遗物的两种尺寸见 `art/IconLayout.json`。运行 `./tools/Pack-Art.ps1 -GameDir $gameDir` 重新生成 `RelicForge.pck`。

Mod 版本号采用 `游戏版本.更新序号`，每个游戏版本的更新序号从 `.1` 开始。游戏清单使用加载器支持的格式，例如 Mod `v0.114.0.6` 在清单中写作 `0.114.0-6`。
