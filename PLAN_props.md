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

## 阶段 1 — 障碍块 + 磨损 ⏳ 待开工（已拍板）
拍板：**障碍先行** + **hp 颜色色阶**（3红→2黄→1绿→碎裂）。
- `material=Obstacle` 占整块 `16×16` 细格；`BlocksFlow=true`、`CanMatch=false`
- `auxGrid` 存 hp（默认 3）
- 新增 `SpawnObstacles(count, region)`：开局/定时在中部随机撒
- **磨损**：`FindBridgeClearCells` 得到消除组后，遍历消除格 4 邻域，命中 Obstacle 则 `auxGrid--`；归零→变空格。**只在消除事件跑，不进 StepSand**
- 渲染：`FlowSandBoardRenderer.GetGrainColor` 加障碍分支，读 `GetMaterialByIndex`/`GetAuxByIndex`，hp→色阶
- 验收：障碍挡沙分流；临近消除逐级磨损；归零消失；不会永久堵死

## 阶段 2 — 干扰炸弹 ⏳ 待开工
- `material=Bomb`，`auxGrid` 存**按「新方块数」倒计时**（比真实秒可控、玩家可数步）
- 调度：`MatchCoordinator` 每次生成新块递减炸弹计数；归零→`DetonateBomb(i)` 细网格圆形炸坑 + 重力重结算
- **拆弹奖励**：炸弹所在连通块先完成左右贯通消除 → 转正向清屏奖励
- 不直接判负。渲染加闪烁倒计时。最优先验证「炸弹送入贯通区反向变奖励」这个爽点

---

## 暂不做（记录备查）
- 彩虹/通配：已由 SuperMixed 覆盖，出待办
- GPT 长方案里的风向系统 / 沙瀑连锁 / 危险线反击 / 颜色任务 / 蓄水闸门 / 硬化沙 / 落点 QTE：都不依赖 material 层，作为独立可选料，主线跑通后再挑
