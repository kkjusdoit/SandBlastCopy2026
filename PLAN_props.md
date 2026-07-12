# 道具与新机制 — 执行方案与进度

> 分支：`feature/props-and-mechanics`
> 目标：在《七彩流沙方块》上增加道具/新机制，提高趣味性和难度。
> 本文件用于对话中断后快速恢复上下文：把它连同项目丢给新的 Claude 会话即可接续。

## 恢复方式
- 续接旧会话：项目目录下 `claude --continue`（最近一次）或 `claude --resume`（选历史）。
- 会话丢了也没关系：进度都在 git commit 里 + 本文件。

## 设计基线（贯穿全程）
1. **地基优先**：先加平行 `materialGrid`，把「颜色」和「行为/耐久」解耦，再叠玩法。
2. **不碰已实现的大混杂 / SuperMixed**：它已充当「彩虹/通配块」bonus（落地跟先接触的同色参与消除），material 层与它正交，只读不改。
3. **磨损只在消除事件结算**，绝不进 `StepSand` 热路径（192×320=61,440 格 × 每帧）。
4. **每阶段独立 commit、可编译、可回退**。

## 网格规格（实测）
- 粗网格 `CoarseCols=12 × CoarseRows=20`，`GrainScale=16`
- 沙细网格 `SandCols=192 × SandRows=320` = **61,440 格**
- 定义在 `Runtime/FlowSandGameController.cs:13-15`

## 关键调用点（已核实）
| 关注点 | 位置 |
|---|---|
| 沙落流动 | `Core/FlowSandBoard.cs` `StepSand` |
| 消除连通搜索 | `Core/FlowSandBoard.cs` `FindBridgeClearCells` + `TryVisitNeighbor` |
| 清格 | `Core/FlowSandBoard.cs` `ClearCells`（调用点 `MatchCoordinator.cs`） |
| 锁块写沙 | `Core/FlowSandBoard.cs` `LockCurrentPiece` |
| 碰撞 | `Collides` / `CollidesAtFineDrop` |
| 渲染取色 | `Runtime/FlowSandBoardRenderer.cs` `GetSandByIndex` → `GetGrainColor` |
| 消除后清屏调度 | `Runtime/FlowSandMatchCoordinator.cs` |

---

## 阶段 0 — Material 平行网格重构 ✅ 已完成
Commit `2c9409c`。玩法零感知。
- 新增 `enum SandMaterial { Normal=0, Obstacle=1, Bomb=2 }`（`FlowSandTypes.cs`）
- 新增平行数组 `SandMaterial[] materialGrid` + `byte[] auxGrid`（存 hp/倒计时）
- 抽 3 个语义函数替换散落的 `== Empty` 判断：
  - `IsEmpty(i)` = 颜色空 且 material==Normal
  - `BlocksFlow(i)` = `!IsEmpty(i)`
  - `CanMatch(i)` = material==Normal 且 颜色非空
- 改造：`StepSand` / `FindBridgeClearCells` / `TryVisitNeighbor` / `Collides` / `CollidesAtFineDrop` / `ClearCells`(同步清 material+aux) / `Reset`(清两数组)
- 行为一致性：material 全 Normal 时三个函数与旧判断布尔等价 → 与 main 逐帧一致
- 验证：`mcs` 编译我的改动零错误（仅有的 2 个报错是文件原有的 readonly struct，mcs 只支持到 C#7，Unity Roslyn 支持 C#8，非问题）

---

## 阶段 1 — 障碍块 + 磨损 ✅ 已完成
Commit `1981a60`。拍板：障碍先行 + hp 颜色色阶（3红→2黄→1绿）。
- `material=Obstacle` 占整块 `16×16` 细格；`BlocksFlow=true`、`CanMatch=false`
- `auxGrid` 存 **per-grain hp**（默认 `ObstacleMaxHp=3`）→ 障碍从边缘被侵蚀、逐渐让出通道，天然防死锁
- `SpawnObstacles(count, random)`：开局在中下部 band（粗行 1/5~3/5）随机撒，只落在完全空闲的粗格；`InitialObstacleCount=4`（`FlowSandGameController.cs`）
- **磨损** `ErodeObstaclesAround(clearedIndices)`：消除事件里，对消除格 4 邻域的 Obstacle 扣 1 hp；单次事件对同一粒最多扣 1（`erosionStamps` 去重）；hp 归零→变空格。**只在消除事件跑，不进 StepSand**。钩在 `MatchCoordinator.cs` `ClearCells` 前
- 渲染：`FlowSandBoardRenderer.GetObstacleColor(hp,x,y)` 按 hp 色阶 + per-grain 抖动；绘制循环里 Obstacle 优先于颜色分支
- 级联：磨损开口后 sand 流入，settle→`FindBridgeClearCells` 重扫，可触发连锁消除（已确认调度链完整）
- 验证：`mcs` 编译 Core 零新错误（仅原有 readonly-struct 的 C#7 限制报错）
- ⏭️ 待你在 Unity 里跑：手感、障碍是否有效分流、磨损节奏是否合适（可调 `InitialObstacleCount` / `ObstacleMaxHp` / band 范围）

## GM / 测试快捷键 ✅ 已加（Editor only，commit `bcc3700` + `53c7e09` + `dcfca60`）
仅 `#if UNITY_EDITOR`，release 构建自动剔除。对局中（Playing）生效。用纯字母键避开 F 键与 A/D/W/S/P：
- **F8** 触发色彩挑战（原有，项目自带）
- **O** 再撒一批障碍（`InitialObstacleCount` 个）
- **K** 全场障碍各扣 1 hp（看 红→琥珀→绿→破 全周期，不用凑真消除）
- **L** 清空所有障碍
- **B** 排队一个**天降炸弹方块**（下一块变炸弹，可左右移动/瞄准落点，落地变地雷）
- **N** 直接往场地埋一颗**静态地雷**（不用瞄准）
- 每次操作在 Console 打印状态

## 阶段 2 — 干扰炸弹 ✅ 已完成
Commit `a4d7a4b`（board 核心）+ `53c7e09`（runtime 接线）+ `dcfca60`（天降炸弹方块）。

**炸弹有两种形态，共享同一套地雷逻辑：**
- **静态地雷**（场地陷阱，玩家不可控）：开局撒 1 颗（`InitialBombCount`），`SpawnBomb` 直接埋在中下部
- **天降炸弹方块**（玩家可控，`ActivePiece.IsBomb`）：单格 Mono，从顶部落下可移动/旋转/瞄准；落地锁块时 `LockBombPiece` 写入**同一个地雷材质+fuse**，所以后续 tick/引爆/拆弹全部复用。下落中和 Next 预览都渲染成"已布防的地雷"

**共享的地雷机制：**
- `material=Bomb` 占整块 `16×16` 细格；`auxGrid` 存**共享 fuse**（`BombInitialFuse=5`，按"新方块数"计）
- **倒计时**：`TickBombFuses()` 每生成一个新块 −1；归零→`DetonateBombAt` 圆形炸坑（`BombBlastRadius=26` grain），清沙+清其他炸弹，**但不炸障碍**（障碍只吃磨损）
- 引爆后 `TickBombsOnSpawn` 置 `waitingForSandToSettle` → 碎沙沉降 → 重扫 bridge，可连锁；活动块与沙网格独立，落块与沉降互不干扰
- **拆弹奖励**：`DefuseBombsAround` 在消除事件里（挨着 erosion），炸弹紧邻一次消除→拆除不引爆，`+defusedBombs × BombDefuseReward(20) × Combo`
- `HasBombs`/`MinimumBombFuse` 摘要由 `RefreshBombSummary` 在 spawn/lock/defuse/detonate 后统一维护（防止新炸弹不 tick 的坑）
- 渲染：`GetBombColor` 炭黑本体 + 随 fuse 越低越红的脉冲（借用 flash 开关）+ 稀疏亮点
- 验证：`mcs` 编译 Core 零新错误/零警告
- ⏭️ 待 Unity 验：天降炸弹瞄准手感、落地变雷、炸坑、拆弹奖励、不判负；可调 `BombInitialFuse`/`BombBlastRadius`/`BombDefuseReward`/`InitialBombCount`

### 设计理念（炸弹为什么这样做）
- **静态地雷 = 场地干扰源**：埋在你经营的沙堆区，压力来自"我的地盘里有雷"，对应你最初"倒计时炸开沙堆"的描述
- **天降炸弹 = 玩家武器**：可瞄准落点，把威胁主动丢到最该炸的杂色堆；落地变雷后仍可拆可躲
- **倒计时用"方块数"不用"秒"**：变成可算计的博弈（"我还有几步拆弹？"），拆弹奖励奖励的是主动处理干扰

---

## 全部主线完成 🎉
阶段 0（material 地基）→ 阶段 1（障碍+磨损）→ 阶段 2（炸弹）已全部落地，等整体验收。

## 暂不做（记录备查）
- 彩虹/通配：已由 SuperMixed 覆盖，出待办
- GPT 长方案里的风向系统 / 沙瀑连锁 / 危险线反击 / 颜色任务 / 蓄水闸门 / 硬化沙 / 落点 QTE：都不依赖 material 层，作为独立可选料，主线跑通后再挑
