# ClaudeModding — Rise of Industry modding toolkit

A shared library, example mods, verified engine documentation and discovery tools for
**Rise of Industry 2.3.x** (Unity 2018.4, Mono).

> **Credits:** all code, tools and documentation in this repository were written by **Claude** (an AI model by Anthropic).
> Ideas, direction, in-game testing — and the tokens — by **[@WindsTrace](https://github.com/WindsTrace)**. 😄
>
> 本仓库的全部代码、工具和文档均由 **Claude**（Anthropic 的 AI 模型）编写；
> 思路、方向、游戏内测试以及 token 由 **[@WindsTrace](https://github.com/WindsTrace)** 提供。

Not affiliated with Dapper Penguin Studios / Kasedo Games. No game files are included.

## What's inside

| Path | What |
|---|---|
| [`ClaudeModLib/`](ClaudeModLib) | The library: toolbar button + mod panel, persisted feature switches, rebindable hotkeys, typed APIs for game data, cities, shops, tech trees, building clones, reflection and dev tools. |
| [`CityProgress/`](CityProgress) | Window showing each city's tier and population progress to the next tier (built on the lib). |
| [`RoIMods/StarterChain/`](RoIMods/StarterChain) | Example production chain: new product, recipe, building, tech-tree page (built on the lib). |
| [`RoIMods/AssetDump/`](RoIMods/AssetDump) | Dev harness used by `run-test.ps1` (dumps assets at start-up, keeps dev mods enabled). |
| [`RoIMods/build.ps1`](RoIMods/build.ps1), [`run-test.ps1`](RoIMods/run-test.ps1) | Build with the .NET Framework `csc.exe` (no Visual Studio needed), install into the game, restart it and print mod log lines. |
| [`docs/API.md`](docs/API.md) | ClaudeModLib API reference. |
| [`docs/GameMechanics.md`](docs/GameMechanics.md) | Verified engine facts: mod loading, JSON content pitfalls, shops, city growth/upgrades, toolbar. |
| [`tools/`](tools) | PowerShell API discovery: list type members, read method IL calls, check whether an old mod DLL still matches the current game. |

## Quick start (players)

1. Copy `ClaudeModLib` and any mods you want into `<Steam>\steamapps\common\RiseOfIndustry\Mods\`
   (each folder with `desc.json`, `code\`, `assets\`, `content\`).
2. Start the game. A gear button appears in the top-right toolbar (or press **F9**, rebindable in the key settings).
3. In the panel: switch features on/off (e.g. *City progress window*), see which mods are loaded,
   enable/disable mods for the next launch.

Mods disable achievements (game rule).

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
        var h = ModLib.Register(this, "My Mod");
        _show = h.AddFeature("window", "Show my window", true);
        h.AddWindow("Cities", new Rect(40, 140, 420, 300), w =>
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
dependency, and installs into the game's `Mods` folder. See [docs/API.md](docs/API.md) and
[docs/GameMechanics.md](docs/GameMechanics.md).

## Status

- Verified: all mods load without errors in 2.3.3; lib hotkey registers with the game's InputManager;
  StarterChain building/tech tree/shop listing work through the lib API (checked via asset dump and logs).
- Needs in-game confirmation: toolbar button placement/icon, panel and window behaviour during play.
