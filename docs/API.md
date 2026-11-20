# ClaudeModLib API reference (v0.2.0)

Namespace `ClaudeModLib`. Target: Rise of Industry 2.3.x (Unity 2018.4, Mono, .NET 4.x).
Everything here was verified against the game's own code; see [GameMechanics.md](GameMechanics.md) for the why.

To use it, add the dependency in your `desc.json` (this makes the game load the lib first) and reference
`ClaudeModLib.dll` when compiling:

```json
{ "name": "MyMod", "author": "...", "version": 1, "versionString": "1.0.0", "dependencies": ["ClaudeModLib"] }
```

---

## ModLib — registration, lifecycle

| Member | Description |
|---|---|
| `ModHandle ModLib.Register(Mod mod, LocText displayName = null)` | Registers your mod with the lib panel. Call in `OnModWasLoaded`. Returns the same handle if called twice. |
| `void ModLib.OnAllModsLoaded(Action)` | Run once all mods and their content are loaded (immediately if already done). |
| `void ModLib.OnGameReady(Action)` | Run when a game world is running (map loaded) — the place to touch scene UI. |
| `bool ModLib.inGame` | True while a map is running (a `SettlementManager` exists). |
| `ModLoader ModLib.modLoader` | The game's mod loader. |
| `string ModLib.ModNameOf(Mod)` | `name` from the mod's desc.json. |
| `IList<ModHandle> ModLib.registeredMods` | All registered mods. |

### ModHandle

| Member | Description |
|---|---|
| `Feature AddFeature(string id, LocText label, bool defaultOn, LocText description = null)` | An on/off switch in the lib panel. State persists in PlayerPrefs (`ClaudeModLib.<mod>.<id>`). |
| `LibWindow AddWindow(LocText title, Rect rect, Action<LibWindow> draw, Feature visibleWhen, bool alsoInMenus = false)` | A draggable IMGUI window, drawn while `visibleWhen.enabled` and a game is running. Coordinates are in 1080p units (scaled automatically). |

All `LocText` parameters also accept a plain `string` (same text in every language).

### Feature

`bool enabled { get; set; }` (persisted) · `event Action<bool> changed` · `LocText label, description`.

---

## Lang / LocText — English & 简体中文

The lib panel has a language switch: **Auto** (default: follows the game, `GameOptions.language.keycode`
starting with `zh` → Chinese, anything else → English), **English**, **简体中文**. Stored in PlayerPrefs
`ClaudeModLib.language`. Use it so non-Chinese players never see Chinese text.

| Member | Description |
|---|---|
| `string Lang.T(string en, string zh)` | Text for the current language (falls back to English when `zh` is null). Call it while drawing, so switching applies immediately. |
| `bool Lang.isChinese` | Current effective language. |
| `LangMode Lang.mode { get; set; }` | `Auto`, `English`, `ChineseSimplified` (setting it is what the panel does). |
| `string Lang.gameLanguageCode` | The game's language code (`"en"`, `"zh-Hans"`, `"de"` …). |
| `event Action Lang.changed` | Raised when the player switches language. |
| `Lang.Add(key, en, zh)` / `string Lang.Get(key)` | Optional keyed string table. |
| `new LocText(en, zh)` / `LocText.Of(en, zh)` / `.text` | Bilingual text for labels and titles; strings convert implicitly. |

```csharp
GUILayout.Label(Lang.T("Population", "人口") + " " + Ui.Number(pop), Ui.label);
var f = handle.AddFeature("x", LocText.Of("Show window", "显示窗口"), true);
```

### Example

```csharp
public class MyMod : Mod
{
    Feature _show;
    public override void OnModWasLoaded()
    {
        base.OnModWasLoaded();
        var h = ModLib.Register(this, "My Mod");
        _show = h.AddFeature("window", "Show my window", true);
        h.AddWindow("My Mod", new Rect(40, 140, 400, 200), w => GUILayout.Label("Hello", Ui.label), _show);
    }
}
```

---

## Data — the game database (GameData)

All products, recipes, buildings, vehicles, formulas, unlocks, categories, tags … are named assets.
**Names are asset names, not display names** (`"LightBulb"`, not `"Light Bulbs"`). Use `DevTools.DumpAssets()`.

| Member | Description |
|---|---|
| `T Get<T>(string name)` | Asset or null. |
| `T Require<T>(string name)` | Asset or `KeyNotFoundException` with a clear message. |
| `List<T> All<T>()` / `List<string> Names<T>()` | Every asset of a type. |
| `List<Building> BuildingsWith<TComp>()` | Building prefabs carrying a component, e.g. `BuildingsWith<Factory>()`, `BuildingsWith<Shop>()`. |
| `List<ProductDefinition> ProductsWithTag(params string[] tags)` | Products carrying any of the ProductTags. |
| `Recipe RecipeFor(ProductDefinition)` | First recipe producing a product. |
| `void Register(Object asset)` | Add an object to GameData (findable by name, e.g. from JSON). |

## ModAssets — the mod asset table

Files in `assets/` are registered by file name: `.png`→Texture2D, `.svg`→SVGAsset, `.sprite`→Sprite,
`.bundle`→each asset inside by its own name. JSON fields `visualization`, `spawnDust`, `svgIcon`,
`constructionBarIcon`, `headerImage` resolve **only** from this table.

| Member | Description |
|---|---|
| `T Get<T>(string name)` / `bool Has(string name)` | Read (no error spam when missing). |
| `void Register(string name, Object obj)` | Make any object (e.g. a vanilla model clone) usable from JSON. Call in `OnModWasLoaded`, which runs before your `assets/` and `content/` load. |
| `Transform PrefabRoot` | Inactive DontDestroyOnLoad container for prefab clones (their scripts never run; instances made from them are active). |
| `ICollection<string> Names()` | All mod asset names. |

## Content — what JSON cannot fully do

| Member | Description |
|---|---|
| `Building CloneBuilding(string template, string newName, Action<Building> configure = null)` | Clone a vanilla building (all components, footprint, roads, parking) and register it. Call in `OnModWasLoaded`. |
| `void SetBuildingInfo(Building, title, panelTitle, description, baseCost)` | Texts and cost (`null`/`0` = keep). |
| `void SetFactoryRecipes(Building, params string[] recipes)` | Recipes of a Factory/GathererHub; first = initial. Call in `OnAllModsLoaded`. |
| `bool ReplaceModel(Building, GameObject prefab)` | Replace the model under the `Visuals` child (e.g. prefab from your `.bundle`). |
| `void CopyProductPresentation(string product, string templateProduct)` | Cargo model on trucks + factory UI category, which product JSON cannot set. |
| `void AddUnlockToTree(string unlock, string tree, int column)` | Put an existing unlock on a tech-tree page (draws lines from vanilla prerequisites on your page). |
| `void RefreshTree(string tree)` / `List<string> DescribeTree(string tree)` | Rebuild / inspect a page (`"Steel@0"`). |

## Cities — settlement growth

| Member | Description |
|---|---|
| `List<SettlementBase> All()` | Settlements of the running game. |
| `IActor Player` | The human player (for sales/delivery queries). |
| `CityInfo GetInfo(SettlementBase)` | Snapshot: `name, population, tier, nextTier, tierThreshold, nextTierThreshold, progress (0..1), remaining, status, shopCount`. Thresholds are the city's own rolled values. |
| `bool HasSupplied(SettlementBase, IActor)` | Any shop of the city has sales/deliveries by that actor. |
| `List<SettlementTier> Tiers()` | Tier table ordered by id. |

`CityStatus`: `Growing, Stagnant, LimitReached, WaitingForSponsor, Advancing, MaxTier, Dead`.

## Shops — product selection

| Member | Description |
|---|---|
| `ProductTag[] SoldTags(Shop)` / `int MaxProducts(Shop)` / `SetMaxProducts(Shop, int)` | Shop prefab configuration. |
| `List<ProductDefinition> Candidates(Shop)` | Products the shop may pick (tag match) before shuffling. |
| `List<ProductDefinition> Stocked(Shop)` | What a placed shop actually sells. |
| `List<Building> ShopsSelling(ProductDefinition)` | Shop prefabs that would consider a product. |
| `List<string> GuaranteeListing(string product)` | Raise `maxProducts` so the product is always stocked (changes vanilla balance). |
| `int Demand(Shop, ProductDefinition, IActor)` | Current demand. |

## Mods — enable state

| Member | Description |
|---|---|
| `List<ModEntry> List()` | `name, author, versionString, path, enabled/loaded (this session), enabledNextLaunch, usesLib, dependencies`. |
| `void SetEnabledNextLaunch(string mod, bool)` | Game applies it on next start-up. |

## Hotkeys — rebindable keys via the game's input system

Declare the key in content JSON (it shows in the game's key-binding settings):

```json
{ "type": "ProjectAutomata.ContentCreationModels.CCInputButtonModel",
  "object": { "name": "MyModToggle", "category": "UI", "displayName": "My Mod: toggle",
              "description": "...", "uiOrder": 1000, "sensitivity": 1, "keys": [ { "keyCode": "F10" } ] } }
```

Note the explicit model type: `"ProjectAutomata.InputButton"` fails ("model type could not be found").
`keys[]`: `{ "keyCode": <UnityEngine.KeyCode>, "axisType": "None|MouseX|MouseY|ScrollWheel", "modifier": { "keyCode": "LeftControl" } }`.

| Member | Description |
|---|---|
| `bool Pressed(string)` / `bool Held(string)` | Poll in `Update()`. Late-registered buttons are added to the InputManager automatically. |
| `string Describe(string)` | Current binding as text (reflects user rebinding). |

## Ui — IMGUI helpers

`Ui.label, small, bold, header, button` (GUIStyles with a CJK-capable OS font) ·
`Ui.ProgressBar(float value01, Color, float height = 14)` · `Ui.ColoredLabel(text, color)` ·
`Ui.Number(int)` (`238400 → "238.4k"`) · colours `Ui.Green, Yellow, Orange, Blue, Grey, Red` · `Ui.scale`.

`bool Ui.Switch(bool value, string label, params GUILayoutOption[] labelOptions)` — on/off slider
(green **ON/开**, red **OFF/关**) followed by a label in the same colour; clicking either flips it.
Use it instead of `GUILayout.Toggle` so the state is obvious:

```csharp
_feature.enabled = Ui.Switch(_feature.enabled, Lang.T("Show window", "显示窗口"));
```

## Reflect — private members

`Get(obj, name)`, `Get<T>`, `GetOr<T>(obj, name, fallback)`, `Set(obj, name, value)`, `GetStatic(type, name)`,
`Call(obj, method, args…)`, `FindField`, `FindProperty`. Searches the whole class hierarchy; fields first.

## DevTools

| Member | Description |
|---|---|
| `string DumpAssets(string path = null)` | Every GameData asset by type, details for key types → `claudemodlib_asset_dump.txt` in the game's LocalLow folder. Also a button in the lib panel. |
| `string DescribeObject(object)` | All fields incl. private/backing fields. |
| `string DescribeHierarchy(GameObject, int maxDepth = 6)` | GameObject tree with component names (find UI elements). |
| `string Describe(object)` | One-line value description. |

## Log

`Log.Info / Warn / Error(string)` → `output_log.txt` with prefix `[ClaudeModLib]`.
