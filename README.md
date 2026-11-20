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

Get the zips from the [**latest release**](../../releases/latest):

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

- **Toolbar button + mod panel** — feature switches (persisted), list of all mods with loaded state and
  enable/disable for the next launch, one-click dump of all game asset names.
- **Languages / 语言:** *Auto* (follows the game language), *English*, *简体中文* — switch in the panel.
  Non-Chinese players never see Chinese text. Mods built on the lib get the same switch via `Lang` / `LocText`.
- **Rebindable hotkeys** through the game's own input system.
- **Typed APIs** for game data, cities (tier / threshold / progress), shops, tech trees, building clones,
  mod assets, reflection, IMGUI widgets, dev tools — see [docs/API.md](docs/API.md).

Included example mods (in the *with-mods* zip):

- **CityProgress** — window with every city's tier and population progress to the next tier, with status
  (growing / at limit / waiting for sponsor / upgrading). Toggle it in the panel.
- **StarterChain** — example production chain: 2 Steel + 1 Light Bulb → Toy Robot in a new *Toy Robot Workshop*,
  researched on a new *Robotics* tech-tree page.

## Repository layout

| Path | What |
|---|---|
| [`ClaudeModLib/`](ClaudeModLib) | The library. |
| [`CityProgress/`](CityProgress) | City progress window (example mod). |
| [`RoIMods/StarterChain/`](RoIMods/StarterChain) | Example production chain. |
| [`RoIMods/AssetDump/`](RoIMods/AssetDump) | Dev harness used by `run-test.ps1` (dumps assets at start-up, keeps dev mods enabled). Not shipped. |
| [`RoIMods/build.ps1`](RoIMods/build.ps1) · [`run-test.ps1`](RoIMods/run-test.ps1) · [`package.ps1`](RoIMods/package.ps1) | Build with the .NET Framework `csc.exe` (no Visual Studio needed) and install into the game · restart the game and print mod log lines · build the two release zips into `dist/`. |
| [`docs/API.md`](docs/API.md) | ClaudeModLib API reference. |
| [`docs/GameMechanics.md`](docs/GameMechanics.md) | Verified engine facts: mod loading, JSON content pitfalls, shops, city growth/upgrades, toolbar. |
| [`tools/`](tools) | PowerShell API discovery: list type members, read method IL calls, check whether an old mod DLL still matches the current game. |

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

- Verified: all mods load without errors in 2.3.3; lib hotkey registers with the game's InputManager;
  StarterChain building/tech tree/shop listing work through the lib API (checked via asset dump and logs).
- Needs in-game confirmation: toolbar button placement/icon, panel and window behaviour during play.
