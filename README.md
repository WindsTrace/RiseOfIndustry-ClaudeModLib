# Rise of Industry — ClaudeModLib (modding toolkit)

A shared mod library, example mods, verified engine documentation and discovery tools for the game
**Rise of Industry 2.3.x** (Unity 2018.4, Mono). 《工业崛起》(Rise of Industry) 模组工具包。

> **Credits:** all code, tools and documentation in this repository were written by **Claude** (an AI model by Anthropic).
> Ideas, direction, in-game testing — and the tokens — by **[@WindsTrace](https://github.com/WindsTrace)**. 😄
>
> 本仓库的全部代码、工具和文档均由 **Claude**（Anthropic 的 AI 模型）编写；
> 思路、方向、游戏内测试以及 token 由 **[@WindsTrace](https://github.com/WindsTrace)** 提供。

Not affiliated with Dapper Penguin Studios / Kasedo Games. No game files are included.

## Download / 下载

**Steam Workshop (easiest / 最简单):** subscribe and you're done — CityProgress lists ClaudeModLib as a required item.
订阅即可使用，订阅 CityProgress 时 Steam 会提示一起订阅 ClaudeModLib。

| Workshop item | |
|---|---|
| [**ClaudeModLib** — modding toolkit](https://steamcommunity.com/sharedfiles/filedetails/?id=3843162963) | the toolkit / 工具包 |
| [**CityProgress** — city upgrade progress](https://steamcommunity.com/sharedfiles/filedetails/?id=3843163232) | requires ClaudeModLib / 需要工具包 |
| [**FirstVillageStoreChoice** — choose the starting village's shop](https://steamcommunity.com/sharedfiles/filedetails/?id=3844222083) | requires ClaudeModLib / 需要工具包 |

Don't keep a manual copy in `Mods\` and a Workshop subscription of the same mod at the same time (same name → conflict).
不要同时保留 `Mods\` 里的手动安装版和创意工坊订阅版（同名会冲突）。

**Manual install / 手动安装** — get the zips from the [**latest release**](../../releases/latest):

| File | Contents | |
|---|---|---|
| **`ClaudeModLib-v<ver>-toolkit.zip`** | **Toolkit only** — the library and its panel. For players who want a clean base and for mod developers. 纯工具包。 | ⭐ default / 推荐 |
| `ClaudeModLib-v<ver>-with-mods.zip` | Toolkit **+ CityProgress + StarterChain** (the two example mods below). 工具包 + 两个示例 mod。 | optional / 可选 |

**Install / 安装:** copy the folders from the zip into `<Steam>\steamapps\common\RiseOfIndustry\Mods\`
(create `Mods` if missing). Start the game, open a map, click the **gear button** in the top-right toolbar
or press **F9** (rebindable in the game's key settings).
进入对局后点右上角工具栏的**齿轮按钮**或按 **F9**（可在游戏按键设置里改键）。

Enabling mods disables Steam achievements (game rule). 启用 mod 会禁用成就（游戏规则）。

## Features / 功能

- **Toolbar button + mod panel** in the game's native look (cream / orange, editable `config/theme.json`) —
  feature switches, list of all mods with loaded state and enable/disable for the next launch, asset and UI dumps,
  and an in-game **Steam Workshop publisher** for mod authors.
- **Languages / 语言:** *Auto* (follows the game language), *English*, *简体中文* — switch in the panel.
  Non-Chinese players never see Chinese text. Mods built on the lib get the same switch via `Lang` / `LocText`.
- **Rebindable hotkeys** through the game's own input system.
- **Typed APIs** for game data, cities (tier / threshold / progress / tier shops), the player and HQ, world-event
  banners, shops, tech trees, building clones, Harmony patching, mod assets, reflection, IMGUI widgets, dev tools —
  see [docs/API.md](docs/API.md).
- **Keeps itself first in the load order** (works around a game bug that can load a mod before the library it needs).

Mods built on the lib:

- **CityProgress** — window with every city's tier and population progress to the next tier, with status
  (growing / at limit / waiting for sponsor / upgrading). Open it from the panel; close it with ×.
- **FirstVillageStoreChoice** — the one city that starts pre-upgraded no longer gets a random second-tier shop:
  place your HQ in its region, a banner appears, accept the native upgrade offer and pick the shop yourself
  (no contracts, no time limit). HQ elsewhere = the vanilla random shop. Applies to new games.
  开局已升级的村镇不再随机获得二级商店：把总部放在它的区，按横幅提示接受原生升级并自选商店；总部放在别处则与原版相同。
- **StarterChain** — example production chain: 2 Steel + 1 Light Bulb → Toy Robot in a new *Toy Robot Workshop*,
  researched on a new *Robotics* tech-tree page.

## Repository layout

| Path | What |
|---|---|
| [`ClaudeModLib/`](ClaudeModLib) | The library. |
| [`CityProgress/`](CityProgress) | City progress window (example mod). |
| [`FirstVillageStoreChoice/`](FirstVillageStoreChoice) | Choose the starting village's shop (example of Harmony patches + native upgrade flow + event banner). |
| [`workshop/`](workshop) | Steam Workshop descriptions (BBCode, English + Chinese). |
| [`RoIMods/StarterChain/`](RoIMods/StarterChain) | Example production chain. |
| [`RoIMods/AssetDump/`](RoIMods/AssetDump) | Dev harness used by `run-test.ps1` (dumps assets at start-up, keeps dev mods enabled). Not shipped. |
| [`RoIMods/build.ps1`](RoIMods/build.ps1) · [`run-test.ps1`](RoIMods/run-test.ps1) · [`package.ps1`](RoIMods/package.ps1) | Build with the .NET Framework `csc.exe` (no Visual Studio needed) and install into the game · restart the game and print mod log lines · build the two release zips into `dist/`. |
| [`docs/API.md`](docs/API.md) | ClaudeModLib API reference. |
| [`docs/GameMechanics.md`](docs/GameMechanics.md) | Verified engine facts: mod loading and its load-order bug, JSON content pitfalls, shops, city growth, the upgrade state machine, map generation, world events, toolbar. |
| [`tools/`](tools) | PowerShell, no decompiler needed: `Dump-Types` (type members), `Show-ILCalls` (what a method calls), `Find-Usages` (who calls/reads a member), `Test-ModCompat` (does an old mod DLL still fit this game version), `Make-Icon` / `Make-Previews`. |

## Quick start (mod developers)

```csharp
using ClaudeModLib;
using ProjectAutomata;
using UnityEngine;

public class MyMod : Mod
{
    Feature _show;
    public override void OnModWasLoaded()
    {
        base.OnModWasLoaded();
        var h = ModLib.Register(this, LocText.Of("My Mod", "我的模组"));
        _show = h.AddFeature("window", LocText.Of("Show my window", "显示我的窗口"), true);
        h.AddWindow(LocText.Of("Cities", "城市"), new Rect(40, 140, 420, 300), w =>
        {
            foreach (var s in Cities.All())
            {
                var c = Cities.GetInfo(s);
                GUILayout.Label(c.name + "  " + c.tierName + "  " + Ui.Number(c.population), Ui.label);
                Ui.ProgressBar(c.progress, Ui.Green);
            }
        }, _show);
    }
}
```

`desc.json`: `"dependencies": ["ClaudeModLib"]`. Build:

```powershell
powershell -ExecutionPolicy Bypass -File RoIMods\build.ps1 -Mods "ClaudeModLib,MyMod"
```

`build.ps1` finds a mod in `RoIMods\<name>` or next to it (`<repo>\<name>`), compiles `src\*.cs` with C# 5
against the game's `Managed` DLLs (adjust `$Game` at the top), references `ClaudeModLib.dll` when declared as a
dependency, and installs into the game's `Mods` folder.

## Status

- Tested in Rise of Industry 2.3.3: toolbar button, mod panel, themes, languages, hotkeys, CityProgress window
  and the in-game Workshop publisher (both Workshop items were uploaded with it).
- StarterChain (example production chain) is verified via asset dumps and logs; play-testing of its factory is pending.
