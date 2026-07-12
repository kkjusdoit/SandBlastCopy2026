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

## 如何选择导出类型

### Release

`Export WeChat Release` 是可以直接预览、测试和发布的普通正式包。它的流程最简单、稳定性最好，适合：

- 日常功能验证和真机调试。
- 检查 `MinigameLoading` 封面插件是否正常显示和销毁。
- 暂时不需要 WASM 代码分包优化的发布版本。

当前需要验证启动封面时，应先使用 Release 导出，并用微信开发者工具打开：

```text
/Users/linkunkun/WeChatProjects/SandFlow/minigame
```

不要使用微信小游戏转换面板原来的导出按钮，否则不会执行项目在 `FlowSandWeChatBuild` 中定义的封面插件接入和导出检查。

### WASM Collection

`Export WASM Collection Package` 生成的是分析和采集包，只负责收集热函数并为后续代码分包提供数据。它不是最终运行包，不应上传体验版或正式版，也不应使用它判断正式版本的启动速度。

默认采集目录为：

```text
/Users/linkunkun/WeChatProjects/SandFlow-WasmCollection/minigame
```

### WASM Split

WASM Split 是微信官方工具完成代码分包后的最终产物。它通常具有更小的启动代码和更好的正式启动性能，但生成步骤更多，每次 WASM MD5 改变后都需要重新处理。

推荐用途：

- Release 用于快速验证功能和启动封面。
- WASM Collection 只用于采集热函数。
- 完成验证后，正式发布优先使用最终的 WASM Split 包。

不要直接上传 `SandFlow-WasmCollection`。只有经过官方分包并执行 `Integrate Official WASM Split Result` 后的最终目录，才是可验证和发布的分包版本。

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
4. 确认 `game.json` 包含 `MinigameLoading`，并在真机检查启动封面、资源下载和首局游戏
5. 上传体验版或正式版本

需要 WASM 分包优化时执行：

1. `Export WASM Collection Package`
2. 使用采集包收集热函数数据
3. 生成或更新 WASM 热函数列表
4. 使用微信官方工具生成分包结果
5. 设置 `FLOW_SAND_WASM_SPLIT_SOURCE`
6. 执行 `Integrate Official WASM Split Result`
7. 用微信开发者工具验证整合后的 `minigame`
8. 确认最终分包目录包含 `MinigameLoading` 后，再上传体验版或正式版本

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
