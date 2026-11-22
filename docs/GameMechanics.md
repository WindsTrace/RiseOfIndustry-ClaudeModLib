# Rise of Industry modding: verified engine facts (game 2.3.3)

Everything below was established by reading the game's assemblies (reflection + IL) and by test-loading
mods, not from memory. Official wiki: https://riseofindustry.fandom.com/wiki/Modding (written for Alpha 8;
several details are outdated — marked ⚠ below).

## Mod loading

- Mod folders: `<game>\Mods\<name>\` (local), Steam Workshop items, and `Rise of Industry_Data\StreamingAssets\DevMods`.
  Each needs `desc.json` (`author, name, version, versionString, dependencies`). A folder in `Mods\` **without**
  `desc.json` makes pre-loading fail and **no mod loads at all**.
- Per mod, in order: `code\*.dll` (loaded with `Assembly.LoadFile`; every `Mod` subclass gets its own GameObject
  and `OnModWasLoaded()` is called) → `assets\` → `content\`. After all mods: `OnAllModsLoaded()`.
- `dependencies` are sorted first (topological sort) — **but** `ModLoader.OrderMods` then re-sorts the list by the
  saved `ModOrder_<name>` PlayerPrefs numbers (default 0; the in-game mod manager writes 0..n). An explicit number can
  therefore put a mod *before* a library it depends on → `ReflectionTypeLoadException` and the rest stops loading.
  ClaudeModLib pins its own number to -100 every time it loads; dev tools must not renumber other mods.
- Because of `LoadFile`, a mod referencing another mod's DLL needs an `AppDomain.AssemblyResolve` handler
  (ClaudeModLib installs one for itself).
- `CodeLoader.LoadDLL` scans each mod assembly for new **content creation models** and **asset loaders**:
  mods can extend the JSON system and asset formats.
- **Any exception while loading a mod** → that mod is disabled (PlayerPrefs `ProjectAutomata.ModLoader.DISABLED_MODS_PPKEY`)
  and loading of the remaining mods stops. Enabling/disabling only applies on the next launch.
- Enabling mods disables achievements. Game log: `%USERPROFILE%\AppData\LocalLow\Dapper Penguin Studios\Rise of Industry\output_log.txt`.
- The game ships Harmony 1.x (`0Harmony.dll`); `modLoader.harmony` is an instance with id `com.riseofindustry.dapperpenguinstudios`.

## JSON content (`content\*.json`)

Format: `{ "type": "<entity or model type>", "object": { … }, "components": [ … ] }`, optional `"override": "<vanilla name>"`
(replaces a vanilla asset). Models live in `ProjectAutomata.ContentCreationModels` (`CC<Type>Model`, components `CCC<Type>Model`).

- Resolution: visual references (`visualization`, `spawnDust`, `svgIcon`, `constructionBarIcon`, `headerImage`)
  come **only from mod assets**; data references (`category`, `tags`, `priceFormula`, recipe products, vehicles,
  formulas, unlocks, restrictions) come **from GameData by asset name**.
- `ProductDefinition`: `name, displayName, category, tags, icon (Sprite from GameData), svgIcon, priceFormula, isEndgameProduct, disableContracts`.
  ⚠ No `description` field; `visualRepresentation` is declared but never applied → set it in code.
  Factory products in vanilla have no fixed category but `categoryProvider = FactoryProductCategoryOverride` (row by recipe tier).
- `Recipe`: `name, displayName, svgIcon, gameDays, ingredients[{productName, amount}], result[…], requiredModules,
  requiredResourceNodeProducts, excludeInRecipeGraph, usedByWaterResourceHarvester` (⚠ wiki: `allowWaterResources`).
- `TechTreeBuildingUnlock` / `TechTreeRecipeUnlock`: `displayName, description, svgIcon, unlockedByDefault, tier,
  requiredUnlocks, includedUnlocks, researchCost, researchTime ("Research Cost"/"Research Time" formulas),
  trees: [{ "tree": "<TechTree>", "column": n }]` + `building` / `recipes`. **`trees` must be present** (`[]` at least) —
  omitting it throws `ArgumentNullException`. Lines between nodes = `requiredUnlocks`.
- `TechTree` (a research page): `name, displayName, svgIcon, category (TechTreeCategory: Auxiliary, Heavy Industry,
  Light Industry, Prototypes, Rural), uiOrder` (vanilla Toy page: Light Industry, 5000).
  A node can be on several pages (vanilla does this to draw lines from prerequisites).
- `InputButton` (hotkey): use type `ProjectAutomata.ContentCreationModels.CCInputButtonModel`
  (the entity type has no lookup registered). Appears in the key-binding settings.
- ⚠ `Building` via JSON: built on `Resources/Prototypes/BuildingPrototype` (10 components). A vanilla factory has ~40;
  `BuildingTransportManager` and others have no JSON model, and `ManualDestinationManager` throws when added.
  Clone a vanilla building in code instead (`Content.CloneBuilding`). ⚠ wiki typo `JITIVehicleFleet` → `JITVehicleFleet`.

## Assets (`assets\`)

Loaded by file name: `.png` Texture2D, `.svg` SVGAsset (UI icons are SVG), `.sprite` (JSON: `textureName, rectPos,
rectSize, pivot`), `.sae`/`.cae` audio events, `.bundle` Unity AssetBundle (each asset by its own name).
Bundles must be built with **Unity 2018.4.x** (game: 2018.4.11f1; ⚠ wiki says 2018.1).
Vanilla building model layout: `<Building>/Visuals/<Model>`, plus `ParkingSpot` and `Smoke` children.

## Shops

`Shop.GetValidProducts`: all products having **any** of the shop's `soldTags` → shuffled with
`new System.Random(region.center)` (deterministic per region) → first `maxProducts` (vanilla 8) are stocked.
Selection happens when the shop initializes; existing shops in a save may keep their lists.
Shop prefabs and the city tiers they appear in:

| Shop | Tag | Tiers |
|---|---|---|
| FarmersStore, HardwareStore | FarmersMarket, Hardware | Hamlet |
| GroceryStore, ClothingStore, ToyStore, IronmongeryStore, ConstructionStore | GroceryStore, Clothing, Toy, Ironmongery, ConstructionGoods | Village, Town |
| Bookstore, CarStore, DinerStore, FurnitureStore, LiquorStore | BookStore, CarStore, Diner, HomeGoods, Alcohol | City |
| ShowroomStore | Showroom | Metropolis |

Demand (`Shop.UpdateDemand`): formula from the shop's formula category with population, `demandModifier`,
recipe tier/days and `SettlementConfig.shops.demandTierModifiers [10, 9, 4.5, 4.5, 4.5]`, capped per product.

## City growth and upgrades

`DefaultSettlementConfig.growth`: `consumeProductsInterval 15` days, `minGrowthPerConsumedProduct 130`,
`maxGrowthPerConsumedProduct 330`, `maxProductsAffectingGrowth 5`, `growthThreshold 1`, `prosperityThreshold 4`,
`maxAllowedPollution 0.25`.

1. Every 15 days shops consume delivered goods → `SettlementGrowth.TryGrow`. Per consumed product:
   `random(130..330) × ProductCategory.growthMultiplier` (Row1 ×2, Row2 ×3, Row3 ×4; raw/farm/components ×1),
   at most 5 products count. Population is also capped by housing capacity (the city expands itself).
2. Each city rolls its own threshold per tier at start (`SettlementTier.threshold`, `RandomInRange`).
   `growth.GetTierThreshold(tier)` returns it; `populationLimit` = next tier's threshold.

| Tier | id | Threshold | Upgrade contracts need | Shops placed |
|---|---|---|---|---|
| Hamlet | 100 | 0 | – | 2 |
| Village | 200 | 125k–175k | Wall Panels 5–10, Bricks 5–10 | 1 |
| Town | 300 | 275k–325k | Wall Panels 10–15, Bricks 10–15 | 1 |
| City | 400 | 475k–525k | Reinforced Wall 10–20, Bricks 10–20 | 2 |
| Metropolis | 500 | 725k–775k | Reinforced Wall 15–25, Bricks 15–25 | 1 |
| Bloated | 1000 | 950k–999k | – | 1 |

3. At the limit, if the city has a next tier and **someone owns the region permit**, `SettlementAdvancement.StartAdvancement(permitOwner)`
   starts (4 advancement contracts, `SettlementConfig.advancement.advancementContracts`) and a new shop is chosen.
   Without a permit owner the city waits for a sponsor (`IsWaitingForSponsor`) and stops growing.

Tier names: the game's Chinese translation shows **"村庄" for both Hamlet and Village** — compare `tierId`, never `tierName`.

### Upgrade state machine (`SettlementAdvancement`, private nested classes)

| State | Class | What happens |
|---|---|---|
| `GROW` | `GrowState` | Normal growth. `StartAdvancement(actor)` → `ACCEPT`. |
| `ACCEPT` | `AcceptState` | `Initialize`: possible shops = `nextTier` shops of the city type; contracts created from `nextTier.neededProducts` (random amounts); timeout = today + `timeoutRange`; accept icon over the town. The sponsor accepts (`AcceptAdvancement`) → `DELIVER`. **On timeout without acceptance: a random shop is picked and `Advance` runs (no contracts).** |
| `DELIVER` | `DeliverState` | Contracts shown; the player picks a shop (`SelectShop`). When contracts are done **and** a shop is selected → `Advance`. If the contract list is empty, `SelectShop` advances immediately. |

`AdvanacementStateBase.Advance` (sic): `tier = tier.nextTier`, `RequestBuilding(selectedShop)`, `OnAdvancementCompleted`
(fx + world event "Settlement Leveled Up"), back to `GROW`. State, contracts, timeout and selected shop are saved by the game.

### Map generation and the starting village

- `SettlementsWGOperation` assigns initial populations from `SettlementGeneratorConfig.initialPopulation` ranges;
  `Settlement.Initialize` sets the tier from the population. In practice one city starts as a Village.
- Initial build (`UrbanAreaInitialBuilder.PerformInitialBuild` → `BuildHouses`): for each tier up to the city's
  tier, houses then `BuildTierShops(tier)` = `RandomItemAndRemove` from `SettlementType.GetShops(tier)` until
  `placedShopsCount`. This is where the starting village's random second-tier shop comes from.
- The player's HQ does not exist yet at that point (it is placed after the map is shown; `Player.hqBuilt`).
  FirstVillageStoreChoice holds that shop back and later uses the upgrade state machine above (no contracts,
  no timeout) so the player picks it in the native panel.

## World events (right-side banners)

`StaticWorldEventData` (JSON model `CCWorldEventDataModel`: displayName, description, svgIcon, type Good/Neutral/Bad,
when, easy/normal/hard `{durationMin, durationMax, effects}`). Triggered with
`WorldEventManager.instance.TriggerStaticEvent(data, actor.worldEvents, WorldEventCreationParams{region, duration, forceEvent})`.
`WorldEventDataBase.triggerMode` (`AUTO`/`MANUAL`) is not settable from JSON — set it to `MANUAL` in code or the
random scheduler may fire the event. 65 vanilla static events exist (seasons, disasters, "Settlement Leveled Up" …).

## Toolbar / UI

- Top-right toolbar: `ProjectAutomata.MainUIToolbar` collects `MainUIToolButton` children in `Awake` and toggles their
  visibility each frame. Icons are `SVGImporter.SVGImage` with `SVGAsset`. ClaudeModLib clones a button, strips
  `ProjectAutomata.*` scripts from the clone and wires its own `onClick`.
- Building panel tabs: JSON `ProjectAutomata.BuildingPanelModule` (see wiki "Extending the Building Panel").

## Tooling used (no Visual Studio needed)

- Compiler: `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /langversion:5` against `Rise of Industry_Data\Managed\*.dll`
  (C# 5: no `$""`, `?.`, pattern matching). See `RoIMods\build.ps1`.
- API discovery: PowerShell reflection over `Assembly-CSharp.dll` + a minimal IL call lister; runtime data via
  `DevTools.DumpAssets`.
