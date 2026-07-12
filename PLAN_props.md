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

## GM / 测试快捷键 ✅ 已加（Editor only，commit `bcc3700`）
仅 `#if UNITY_EDITOR`，release 构建自动剔除。对局中（Playing）生效。用纯字母键避开 F 键与 A/D/W/S/P：
- **F8** 触发色彩挑战（原有，项目自带）
- **O** 再撒一批障碍（`InitialObstacleCount` 个）
- **K** 全场障碍各扣 1 hp（看 红→琥珀→绿→破 全周期，不用凑真消除）
- **L** 清空所有障碍
- 每次操作在 Console 打印当前障碍块数（`board.CountObstacleBlocks()`）
- 阶段 2 做炸弹时，再往这里加字母键（如 B=手动放炸弹）

## 阶段 2 — 干扰炸弹 ⏳ 待开工
- `material=Bomb`，`auxGrid` 存**按「新方块数」倒计时**（比真实秒可控、玩家可数步）
- 调度：`MatchCoordinator` 每次生成新块递减炸弹计数；归零→`DetonateBomb(i)` 细网格圆形炸坑 + 重力重结算
- **拆弹奖励**：炸弹所在连通块先完成左右贯通消除 → 转正向清屏奖励
- 不直接判负。渲染加闪烁倒计时。最优先验证「炸弹送入贯通区反向变奖励」这个爽点

---

## 暂不做（记录备查）
- 彩虹/通配：已由 SuperMixed 覆盖，出待办
- GPT 长方案里的风向系统 / 沙瀑连锁 / 危险线反击 / 颜色任务 / 蓄水闸门 / 硬化沙 / 落点 QTE：都不依赖 material 层，作为独立可选料，主线跑通后再挑
