# 第三方资源登记

登记所有非 Unity 内置、由外部获取的资源。每条记录至少包含资源名称、来源、版本或授权信息、用途，以及是否经过修改。

| 资源名称 | 来源 | 版本或授权信息 | 用途 | 是否修改 | 资源路径 |
| --- | --- | --- | --- | --- | --- |
| Noto Sans CJK SC Regular | Google Noto CJK | SIL Open Font License 1.1 | PC、MR、CAVE 共用的中文 TMP 字体源文件 | 否 | `Assets/_Project/Fonts/NotoSansCJKsc-Regular.otf` |
| 待填写 | 待填写 | 待填写 | 待填写 | 否 / 是 | `Assets/ThirdParty/...` |

## 登记规则

- Asset Store 资源、Unity Package、GitHub 资源、外部 Shader、外部特效和外部音频等统一登记。
- 外部资源原始目录保留在 `Assets/ThirdParty`，不要与 `Assets/_Project` 中的正式项目资源混放。
- 提交或发布前补齐每项资源的来源、授权信息、版本和修改情况。
