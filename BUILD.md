# Flow Sand 微信小游戏 Build 使用说明

本文说明如何通过 Unity 顶部菜单 `Flow Sand > Build` 导出微信小游戏。

## 准备工作

1. 使用项目指定的 Unity 版本打开工程。
2. 在 `微信小游戏` 导出设置中确认 AppID、CDN 和导出目录正确。
3. 等待 Unity 完成资源导入和脚本编译，确保 Console 中没有编译错误。
4. 正式导出前建议关闭微信开发者工具，避免旧文件被占用。

默认导出目录由微信小游戏配置中的 `DST` 决定。当前项目配置示例：

```text
/Users/linkunkun/WeChatProjects/SandFlow
```

也可以通过环境变量临时指定输出目录：

```bash
export FLOW_SAND_WX_OUTPUT=/absolute/path/to/output
```

设置环境变量后，需要从同一终端启动 Unity，Unity 才能读取该变量。

## 导出正式发布包

在 Unity 顶部菜单选择：

```text
Flow Sand > Build > Export WeChat Release
```

该命令会：

1. 使用 Release 配置导出 WebGL 和微信小游戏文件。
2. 生成 `minigame/wasmcode` 中的 WASM 压缩文件。
3. 将 `MinigameLoading` 插件和启动封面集成到小游戏产物。
4. 检查 `game.json`、WASM 文件和 symbols 文件是否存在。
5. 导出结束后恢复原来的微信小游戏 Build 配置。

导出成功后，使用微信开发者工具打开输出目录中的 `minigame`：

```text
<输出目录>/minigame
```

不要只导入 `webgl` 子目录。

## 导出 WASM 采集包

需要生成 WASM 热函数采集包时，选择：

```text
Flow Sand > Build > Export WASM Collection Package
```

采集包默认输出到正式目录名称加 `-WasmCollection` 的目录。例如：

```text
/Users/linkunkun/WeChatProjects/SandFlow-WasmCollection
```

该导出会保留函数分析所需信息，并在 `minigame` 目录生成：

```text
FlowSand-Wasm-HotFunctions.txt
```

采集包用于收集和生成 WASM 热函数列表，不应直接作为最终正式发布包。

## 其他菜单

### Generate WASM Hot Function List

```text
Flow Sand > Build > Generate WASM Hot Function List
```

从已有采集包的 symbols 文件重新生成热函数列表。使用前应先成功导出 WASM 采集包。

### Integrate Official WASM Split Result

```text
Flow Sand > Build > Integrate Official WASM Split Result
```

将官方工具生成的 WASM 分包结果复制并整合到项目输出目录。执行前设置源目录：

```bash
export FLOW_SAND_WASM_SPLIT_SOURCE=/absolute/path/to/official/split/minigame
```

源路径可以指向分包结果根目录，也可以直接指向其中的 `minigame` 目录。

## 推荐发布流程

普通版本直接执行：

1. `Export WeChat Release`
2. 用微信开发者工具打开 `<输出目录>/minigame`
3. 清缓存并重新编译
4. 在模拟器和真机检查启动、加载页、资源下载和首局游戏
5. 上传体验版或正式版本

需要 WASM 分包优化时执行：

1. `Export WASM Collection Package`
2. 使用采集包收集热函数数据
3. 生成或更新 WASM 热函数列表
4. 使用微信官方工具生成分包结果
5. 设置 `FLOW_SAND_WASM_SPLIT_SOURCE`
6. 执行 `Integrate Official WASM Split Result`
7. 用微信开发者工具验证整合后的 `minigame`

## 常见问题

### `open: No such file or directory`

如果错误路径位于 `minigame/wasmcode`，通常表示 WASM 输出目录没有创建。项目代码现在会在 Brotli 压缩前自动创建该目录。若仍出现此错误，先删除不完整的导出目录，再重新执行完整导出。

### `Unable to find the plugins object in exported game.json`

这是旧版后处理逻辑依赖 `game.json` 固定文本格式导致的。当前代码使用 JSON 解析，并会在缺少 `plugins` 时自动创建。如果再次出现，确认 Unity 已完成脚本重新编译后再导出。

### 导出很久后失败

先查看 Console 中最早出现的异常。最后显示的 `GUI Error: Invalid GUILayout state` 通常只是导出异常中断窗口后的连带错误，不是根因。

失败后建议：

1. 记录 Console 中第一条异常及完整堆栈。
2. 删除对应的不完整输出目录。
3. 确认磁盘空间充足，且输出路径具有写权限。
4. 重新执行目标菜单，不要在失败产物上手动混合多次导出。

## 导出后的检查清单

- `minigame/game.json` 存在且包含 `plugins.MinigameLoading`。
- `minigame/minigame-loading.js` 存在。
- `minigame/images/minigame-loading-cover.jpg` 存在。
- `minigame/wasmcode` 中存在 `.wasm.br` 文件。
- `minigame/webgl.wasm.symbols.unityweb` 存在。
- 微信开发者工具可以正常编译并进入游戏。
- 真机能够显示加载页并完成资源加载。
