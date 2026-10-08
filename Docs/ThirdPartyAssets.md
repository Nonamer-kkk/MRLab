# 第三方资源登记

| 资源名称 | 来源与作者/机构 | 版本或授权信息 | 用途 | 修改情况 | 资源路径 |
| --- | --- | --- | --- | --- | --- |
| Noto Sans CJK SC Regular | Google Noto CJK，https://github.com/notofonts/noto-cjk | SIL Open Font License 1.1；许可文件随字体保存 | 项目内中文字体源 | 字体源未修改 | `Assets/_Project/Fonts/NotoSansCJKsc-Regular.otf` |
| Noto Sans CJK SC TMP 字体 | 基于上述字体由项目生成 | 沿用 OFL 1.1；项目生成 SDF/Atlas | PC/MR/CAVE 中文显示 | 已生成 TMP 字体、Atlas 和材质 | `Assets/_Project/Fonts/NotoSansCJKsc-Regular SDF.asset` |
| TMP Essential Resources | Unity Technologies，`com.unity.textmeshpro` 包的 Package Resources | TMP 3.0.7；随包 Unity 授权，附带字体遵守各自 OFL/Attribution | Settings、Shader、默认材质和 Sprite | 已整体移到 ThirdParty；TMP Settings 默认/回退字体改为项目 Noto | `Assets/ThirdParty/TextMesh Pro` |

## 登记说明

TMP 随附 `LiberationSans` 文件保留在第三方原始结构内，项目中文 UI 使用 Noto 字体。项目不得依赖开发电脑系统字体。Noto 的许可在 `Assets/_Project/Fonts/OFL.txt`，TMP 附带字体和 Sprite 的许可/Attribution 保留在第三方资源目录。

程序生成的实验室、器材网格、材质、UI 和效果属于本项目制作资源，登记在 `AssetList.md`。本阶段不引入外部模型或音频。后续获取的外部资源必须补充来源、作者、许可、修改情况和路径。

Unity Package Manager 依赖由 `Packages/manifest.json` 与 `packages-lock.json` 锁定：URP 14.0.12、Input System 1.14.2、TMP 3.0.7。包源码保留在 Unity Package Manager 管理位置，不拆入 `_Project`；从包导入的 Unity 资产统一归档到 `ThirdParty`。
