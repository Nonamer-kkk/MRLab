# 项目开始前准备清单

依据《课程项目统一开发规范 V1.3》第17章检查。

| 序号 | 准备项目 | 状态 | 说明 |
| --- | --- | --- | --- |
| 1 | 安装 Unity 中国版 2022.3.62f3c1 | 已完成 | 当前工程版本为 2022.3.62f3c1。 |
| 2 | 使用该版本创建 URP 项目 | 已完成 | 已安装并配置 URP 14.0.12。 |
| 3 | 项目完整路径全部为英文 | 部分完成 | 路径字符为英文，但 `E:\Unity Project` 含空格，需迁移后完全合规。 |
| 4 | 按规则命名工程 | 已完成 | `MRLab_E10_24371119`。 |
| 5 | 建立统一 Assets 目录 | 已完成 | 已补齐 `_Project` 与 `ThirdParty` 目录结构。 |
| 6 | 建立 PC、MR、CAVE 场景目录 | 已完成 | 位于 `Assets/_Project/Scenes`。 |
| 7 | 建立 PC、MR、CAVE 交互脚本目录 | 已完成 | 位于 `Assets/_Project/Scripts/Interaction`。 |
| 8 | 建立 PC、MR、CAVE Build 目录 | 已完成 | 位于 `Builds`。 |
| 9 | 配置 URP 管线配置文件 | 已完成 | 配置文件位于 `Assets/_Project/Settings`。 |
| 10 | 设置 Color Space 为 Linear | 已完成 | Player Settings 已保存为 Linear。 |
| 11 | 配置 Unity Input System | 已完成 | Input System 1.14.2，Active Input Handling 为新输入系统。 |
| 12 | 导入 TMP Essential Resources | 已完成 | 资源位于 `Assets/TextMesh Pro`。 |
| 13 | 建立项目中文 TMP 字体资源 | 已完成 | 项目内置 Noto Sans CJK SC，不依赖开发电脑系统字体。 |
| 14 | 创建 01_PC_Main | 已完成 | 位于 `Assets/_Project/Scenes/PC`。 |
| 15 | 按规范建立 PC 场景 Hierarchy | 已完成 | 已建立 Systems、Environment、Experiment、Interaction、UI、Camera、Debug。 |
| 16 | 在 Systems 中建立系统对象位置 | 已完成 | 已建立 ExperimentManager、UIManager、AudioManager、InputManager、ResetManager。 |
| 17 | 建立 Docs 目录及规定文档 | 已完成 | 已建立 ExperimentDesign、AssetList、ThirdPartyAssets、README。 |
| 18 | 建立 SourceAssets 目录 | 已完成 | 已建立 Models、Images、Audio、Reference。 |
| 19 | 建立本地 Git 仓库 | 已完成 | 本地仓库已初始化。 |
| 20 | 配置 Unity 项目 .gitignore | 已完成 | 已忽略 Library、Temp、Logs、obj、Builds 等生成内容。 |
| 21 | 完成第一次 Git 提交 | 已完成 | 初始化整理完成后提交。 |
| 22 | 完成 Windows x86-64 Build | 已完成 | 输出位于 `Builds/PC`。 |
| 23 | 确认 Build 能够正常启动 | 已完成 | 已实际启动，进程响应正常且 Player 日志无启动错误。 |
| 24 | 检查无误后开始实验开发 | 待填写实验设计 | 工程准备环境已就绪；实验名称、目标、流程和创新点仍需填写。 |
