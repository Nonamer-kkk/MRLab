# 项目开始前准备清单

依据《课程项目统一开发规范 V1.3》第17章。最终实验版 Build 与旧空场景验证分开记录。

| 序号 | 准备项目 | 状态 | 说明 |
| --- | --- | --- | --- |
| 1 | Unity 中国版 2022.3.62f3c1 | 已完成 | ProjectVersion 已记录指定版本 |
| 2 | 指定版本 URP 工程 | 已完成 | URP 14.0.12 |
| 3 | 英文且无空格完整路径 | 部分完成 | `E:\Unity Project` 仍含空格 |
| 4 | 工程命名 | 已完成 | MRLab_E10_24371119 |
| 5 | 统一 Assets 目录 | 已完成 | `_Project`、`ThirdParty` |
| 6 | PC/MR/CAVE 场景目录 | 已完成 | `Assets/_Project/Scenes` |
| 7 | PC/MR/CAVE 交互脚本目录 | 已完成 | `Scripts/Interaction` |
| 8 | 三端 Build 目录 | 已完成 | `Builds/PC`、`MR`、`CAVE` |
| 9 | URP 配置 | 已完成 | `Assets/_Project/Settings` |
| 10 | Linear 色彩空间 | 已完成 | `m_ActiveColorSpace = 1` |
| 11 | Input System | 已完成 | 1.14.2，`activeInputHandler = 1` |
| 12 | TMP Essential Resources | 已完成 | 已归档到 `Assets/ThirdParty/TextMesh Pro` |
| 13 | TMP 中文字体 | 已完成 | Noto 项目字体已设默认和回退，不使用系统字体 |
| 14 | 创建 PC 主场景 | 已完成 | `Scenes/PC/01_PC_Main.unity` |
| 15 | PC Hierarchy | 已完成 | 七个规范根节点及 PC 交互子节点 |
| 16 | 系统对象位置 | 已完成 | ExperimentManager、UIManager、AudioManager、InputManager、ResetManager |
| 17 | Docs 文档 | 已完成 | 四项规定文档及准备/验收记录 |
| 18 | SourceAssets | 已完成 | Models、Images、Audio、Reference |
| 19 | 本地 Git | 已完成 | 仓库已初始化 |
| 20 | .gitignore | 已完成 | 生成目录和 Builds 不纳入源工程版本管理 |
| 21 | 首次提交 | 已完成 | 已有工程初始化提交 |
| 22 | Windows x86-64 Build | 已完成 | 2026-09-28 18:31 实验版 1.0.0，0 错误 / 0 警告 |
| 23 | Windows 正常启动 | 已完成 | 实际启动主界面：0 A、22°C、完好/正常回路，无自动注入故障 |
| 24 | 基础检查后正式开发 | PC 首版已完成 | 单线路实验、实验室场景、曲线/回放/CSV 已集成，测试边界见 PCValidation |

PC 版范围按用户要求执行，MR/CAVE 只保留目录与架构。Meta XR SDK 在后续 MR 阶段接入，不阻塞当前 PC 版。
