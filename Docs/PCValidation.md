# PC 版验收记录

仅记录实际检查结果。以下证据核对时间为 2026-09-28；“自动测试通过”只代表模型/管理器测试，不替代鼠标键盘和完整画面的人工验收。尚未运行的交互保持“待验证”，不以旧空场景 Build 或估计值替代。

## 环境

| 项目 | 值 |
| --- | --- |
| Unity / URP | 2022.3.62f3c1 / 14.0.12 |
| 平台 / 输入 | Windows x86-64 / Input System 1.14.2 |
| 主场景 | `Assets/_Project/Scenes/PC/01_PC_Main.unity` |
| Build 目录 | `Builds/PC` |
| Build 时间 / 版本 | 2026-09-28 18:31（北京时间）/ 1.0.0；见 `Logs/CodexFinalBuild.log` |
| 测试机器 / 分辨率 | AMD Ryzen 7 8845H、NVIDIA GeForce RTX 4060 Laptop GPU（7957 MB）、15658 MB RAM / 1600×900 |
| 实测结果目录 | `C:/Users/71076/AppData/LocalLow/MRLab/JouleLab_E10/Results` |

## 验收矩阵

| 检查项 | 通过条件 | 结果 / 证据 |
| --- | --- | --- |
| 编译与构建 | 无 C# 编译错误，Windows x86-64 Build 成功 | 通过。`Logs/CodexFinalBuild.log:6034`：Succeeded、errors=0、warnings=0，Build 报告大小 96767201 bytes |
| 启动 | 主场景正常启动，无启动异常 | 通过。重新启动后未输入时为 0 A、22°C、0 W、0 kJ、0 s、完好/正常回路，仅“巡检”阶段激活，没有自动故障；实际 Player 日志无异常/缺字告警 |
| 结构 | 规范 Hierarchy、目录和唯一 PC Build 场景 | 通过静态核对。7 个规范根节点、5 个系统节点、Experiment 的4个分类和 Interaction/PC 齐全；唯一启用 Build 场景为 PC 主场景；MR/CAVE 及资源预留目录齐全并已补 `.gitkeep`。路径空格问题单列，不计为通过 |
| 巡检与设参 | 可巡检并设置线径、负载、接触电阻、保护器、故障位置 | 自动测试通过。鼠标实际验证巡检、线径从1.5切至0.5 mm²并应用到下一轮，运行时控件锁定；其他参数控件尚未逐个点击验收 |
| 正常/故障计算 | 正常通电和短路/接触不良的电流、温度方向合理 | 自动测试通过。实际默认正常回路约1.93 A；注入短路后50 A、温度上升，温度升高时电流逐步降低；断电后0 A、焦耳功率0 W、温度缓降。接触不良分支已模型测试，尚未单独鼠标跑通 |
| 材料与效果 | 连续变色、软化、热解、烟雾、起火和碳化表现 | 模型阶段测试通过。实际运行看到热风险颜色/发光随温度变化，事件记录软化、热解、起火、碳化，回放可恢复“起火”状态。烟火粒子近景动态观感仍需专项复核，不以状态标签代替全部视觉验收 |
| 安全操作 | 先断电/隔离，再报警、灭火；错误操作有反馈 | 自动测试完整顺序通过。实际按钮验证带电灭火被“必须先隔离电源”拦截；隔离使电流归零；报警完成；燃料已碳化无明火时灭火提示“无需喷射，可进入复盘”。尚未在明火仍持续时人工完成喷射灭火 |
| 曲线与回放 | 数值记录一致，时间轴可回放因果链 | 通过。进入复盘时实时操作锁定；鼠标拖动到66.1 s显示45.10 A、606.8°C、8.24 kJ和起火状态，曲线竖线与数字同步。确定性和复盘隔离的自动测试通过 |
| 上轮对比 | 重置后可比较上一轮 | 通过。重置后第一轮曲线保留为灰色虚线；切换0.5 mm²再运行时本轮为彩色实线，与上一轮同时显示。重复空重置保留对照的自动测试通过 |
| CSV | `persistentDataPath/Results` 有可读 CSV | 通过。鼠标点击导出得到 `JouleExperiment_20260928_184401_981_ea52b2.csv`（220514 bytes），15列带单位，可读取正常/短路/断电冷却记录；结束样本210.708 s、0 A、632.687°C。导出后重置多轮仍保留 |
| Reset | 任意状态恢复可重新开始，历史 CSV 不删除 | 通过自动测试及实际多轮。复盘/通电故障状态按重置按钮恢复0 A、22°C、0 s、完好/正常回路，保留选定参数、上轮对照和历史CSV，可再次巡检通电 |
| 字体 | 全部文字为 TMP，使用项目内字体，无缺字 | 已运行页面和提示通过：项目内 Noto Sans CJK SC、411字符静态4096图集。巡检/短路/处置/复盘/导出中文及公式显示正常，最新 Player 日志无 TMP 缺字告警 |
| 快捷键/镜头 | P/F/G/E/A/X/T/R/Space/Home、右拖/滚轮有效 | 键盘自动注入 F/R/Home 未获得可靠响应，未判定为通过；场景PCInput启用且管理器/相机绑定正确，程序集包含键盘处理。需物理键盘独立复核；右拖/滚轮也保留为待专项验收。所有实验操作有鼠标按钮 |
| 物理趋势测试 | 保护、线径、电阻变化趋势测试通过 | 通过。`Logs/JouleModelTests.json`：2026-09-28T10:31:19Z，12 passed / 0 failed |
| 帧率 | 目标机器达到 PC 60 FPS 开发目标 | 本机已测静态实验室场景通过：平均 164.51 FPS，P95 6.07 ms，预热 5 秒后采样 30 秒，共 4936 帧。烟火高负载场景未单独基准测试，不据此推断 MR/CAVE 或其他机器性能 |

## 可定位证据

- 最终构建：`Logs/CodexFinalBuild.log`，主场景生成、字体烘焙、12 项测试和 Windows Player Build 均完成。
- 测试明细：`Logs/JouleModelTests.json`；测试实现位于 `Assets/_Project/Scripts/Utility/Editor/JouleModelTests.cs`。
- 最终已记录 Player 运行：`Logs/PCInteractive.log`。旧的 `Logs/PCPlayer.log` 来自字体修复前，不作为最终运行结论。
- 性能原始报告：`C:/Users/71076/AppData/LocalLow/MRLab/JouleLab_E10/Results/Performance.json`，时间 2026-09-28T10:33:02Z。
- 主界面截图：`C:/Users/71076/AppData/LocalLow/MRLab/JouleLab_E10/Results/Laboratory.png`，截图为复位后的干净状态。
- 本轮实际导出：`C:/Users/71076/AppData/LocalLow/MRLab/JouleLab_E10/Results/JouleExperiment_20260928_184401_981_ea52b2.csv`。
- 本轮交互日志：`C:/Users/71076/AppData/LocalLow/MRLab/JouleLab_E10/Player.log`。只记录 Unity 日志，不含所有鼠标事件；上述运行观察来自实际窗口验收。

## 尚待专项复核

物理键盘快捷键和镜头操作、全部参数控件、保护器/接触不良完整鼠标流程、明火期间人工灭火、烟火近景观感与高负载帧率。已完成的主流程按钮、回放、CSV、Reset和双轮对照不再列为待验证。模型与管理器对应分支已有12项自动测试覆盖，但不能替代上述专项人工验收。

工程仍位于 `E:/Unity Project/MRLab_E10_24371119`，父目录 `Unity Project` 含空格。目录存在性检查不能消除该路径不合规项；当前尚未迁移工程位置，不将本项记录为完全符合规范。

## 失败和复测

| 日期 | 检查项 | 现象 | 修复 | 复测 |
| --- | --- | --- | --- | --- |
| 2026-09-28 | 字体烘焙/首次构建 | 早期构建出现字体空引用及缺少 ∿、₂；见 `CodexBuildPC.log`、`CodexVerifyBuild2.log` | 修订字体初始化和 UI 字符，重新烘焙项目用字 | 最终 Build 411 字符烘焙成功，0 错误 / 0 警告 |
| 2026-09-28 | Player 字体 | `PCPlayer.log` 出现 Ellipsis 缺字及文本截断警告 | 修订文本溢出设置并重新生成/构建 | `PCInteractive.log` 无同类告警；最终主界面截图中文可读 |
| 2026-09-28 | 性能证据 | 旧 `PCPlayer.log` 的 12512.81 FPS 不代表本次有窗口图形运行 | 在实际 Windows 图形 Player 重测 | `PCInteractive.log` 和 `Performance.json` 一致为 164.51 FPS、P95 6.07 ms |

MR/CAVE 不是本次 PC 功能验收项。PC首版已可构建、可运行并完成主要鼠标流程；未完成的专项人工步骤不计为通过。最终 `git diff --check` 已通过（只有本机LF/CRLF转换提示）。
