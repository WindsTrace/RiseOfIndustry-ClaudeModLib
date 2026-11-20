# Rise of Industry 模组工作区

游戏版本 2.3.3（Unity 2018.4.11f1）。源码在 `D:\ClaudeModding`，`build.ps1` 会把 mod 安装到
`C:\Program Files (x86)\Steam\steamapps\common\RiseOfIndustry\Mods\<mod名>`。

```
D:\ClaudeModding\
├─ RoIMods\        build.ps1 / run-test.ps1 / README.md / AssetDump / StarterChain
└─ CityProgress\   城市升级进度面板（F8）
```
`build.ps1 -Mods X` 先找 `RoIMods\X`，找不到再找同级的 `D:\ClaudeModding\X`。

## 常用命令

```powershell
# 关闭游戏 → 编译安装 → 启动游戏 → 打印 mod 相关日志
powershell -ExecutionPolicy Bypass -File run-test.ps1 -Build StarterChain
# 只编译安装（游戏必须已关闭，否则 DLL 被锁）
powershell -ExecutionPolicy Bypass -File build.ps1 -Mods StarterChain
```

日志：`%USERPROFILE%\AppData\LocalLow\Dapper Penguin Studios\Rise of Industry\output_log.txt`
资源名导出：同目录 `roi_asset_dump.txt`（由 AssetDump 生成，查原版名字就看它）

## 目录

| 路径 | 作用 |
|---|---|
| `StarterChain/` | 示例生产链：2 Steel + 1 LightBulb → 1 Toy Robot，在 Toy Robot Workshop 生产 |
| `StarterChain/content/*.json` | 产品、配方、科技解锁（官方 JSON 内容系统） |
| `StarterChain/src/StarterChainMod.cs` | 建筑（克隆原版 ToyFactory）、配方挂载、自定义模型替换、产品补丁 |
| `StarterChain/assets/` | `toyrobot.svg`（产品/配方图标）等 |
| `StarterChain/ToyRobotWorkshop.json.disabled` | 纯 JSON 建筑的写法，留作参考；2.3 版里组件不全，没有启用 |
| `AssetDump/` | 开发辅助：导出所有原版资源；**同时会在每次启动时重新启用所有 mod 并把自己排第一** |

## 已验证的引擎行为（反射 + IL 确认）

- 每个 mod 加载顺序：`code`（DLL，调用 `OnModWasLoaded`）→ `assets` → `content`；全部 mod 加载完后调 `OnAllModsLoaded`。
- JSON 里的 `visualization`、`spawnDust`、`svgIcon`、`constructionBarIcon` 只从 **mod assets** 查找；
  `category`、`tags`、`priceFormula`、配方原料、车辆、公式、解锁引用的是 **原版 GameData 资源名**（见 `roi_asset_dump.txt` 的 `- 名字`，注意是资源名不是显示名，例如 `LightBulb` 不是 `Light Bulbs`）。
- 产品 JSON 设置不了 `visualRepresentation`（卡车上的货物模型），代码里补。
- 解锁 JSON 必须写 `"trees": []`，否则加载时 NullReference。
- wiki 上的过时写法：`JITIVehicleFleet`→`JITVehicleFleet`，`allowWaterResources`→`usedByWaterResourceHarvester`。
- **任何 mod 加载报错 → 游戏会禁用 mod 并中止后续加载**。AssetDump 保持在第一位，会在下次启动前自动重新启用。

## 科技树

- 新研究页：`content/RoboticsTechTree.json`（`ProjectAutomata.TechTree`，`category` 填 TechTreeCategory 名：Auxiliary / Heavy Industry / Light Industry / Prototypes / Rural）。
- 节点：解锁 JSON 的 `"trees": [{ "tree": "Robotics", "column": N }]`；连线 = `requiredUnlocks`。
- 原版前置节点（Steel、Light Bulb）出现在新页上：由 `StarterChainMod.cs` 的 `VanillaNodesOnOurTree` 追加位置。

## 商店上架（已从代码确认）

`Shop.GetValidProducts`：所有带商店 `soldTags` 任一标签的产品 → 用 `new Random(区域中心)` 洗牌 → 取前 `maxProducts` 个。
ToyStore：标签 `Toy`，原版上限 8，加上 ToyRobot 后候选 10 个。`GuaranteeShopListing = true` 会把上限提到 10。
商店在建成/初始化时选品，已有存档里的旧商店可能保持原列表，测试请新开档或看新建的玩具店。

## 新增一条链的步骤

1. 复制 `StarterChain`，改 `desc.json` 的 `name`。
2. `content/`：产品 JSON、配方 JSON（原料用原版资源名）、两个解锁 JSON。
3. `src/*.cs` 顶部常量：模板建筑（选占地和外形接近的原版工厂）、建筑名、造价、配方列表。
4. `assets/`：产品 SVG 图标（64×64，白色图形，原版风格）。
5. `run-test.ps1 -Build 新mod名`，看日志有无报错；进游戏在轻工业分类里找建筑。

## 换成自己的建筑模型

1. 安装 **Unity 2018.4.11f1**（必须与游戏一致）。
2. 用 AssetStudio 打开 `Rise of Industry_Data`，导出 `ToyFactoryModel`，在 Unity 里作为尺寸/轴心参考。
3. Blender 建模 → FBX → Unity 里做成 prefab，命名为 `ToyRobotWorkshopModel`（与代码常量 `CustomModelAssetName` 一致），只用 MeshRenderer + Standard 材质，不挂脚本。
4. 设置 AssetBundle 名，用 `BuildPipeline.BuildAssetBundles(out, BuildAssetBundleOptions.None, BuildTarget.StandaloneWindows64)` 打包，产物改名 `xxx.bundle` 放进 `assets/`。
5. 重新 `run-test.ps1`，日志出现 `custom model applied` 即生效。货物模型同理，可在 `PatchProduct()` 里换成 bundle 里的 prefab。

## 发布

Steam 工坊上传工具见 wiki（Mod Uploader 一节）：https://riseofindustry.fandom.com/wiki/Modding
发布前把 `AssetDump` 从 Mods 里移走，它只是开发工具。
