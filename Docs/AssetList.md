# 资产清单

本项目采用程序生成的实验室和器材模型，生成资源属于本项目原创资源；TMP 与 Noto 字体另列于 `ThirdPartyAssets.md`。

| 资产基础名称 | 类型 | Unity 资源路径 | 制作来源 | 状态 |
| --- | --- | --- | --- | --- |
| NotoSansCJKsc-Regular | 字体源 | `Assets/_Project/Fonts/NotoSansCJKsc-Regular.otf` | Google Noto CJK，OFL 1.1 | 已存在 |
| NotoSansCJKsc-Regular | TMP 字体 | `Assets/_Project/Fonts/NotoSansCJKsc-Regular SDF.asset` | 由项目内字体源生成 | 已存在、默认字体 |
| TMP Essential Resources | 基础资源 | `Assets/ThirdParty/TextMesh Pro` | Unity TMP 3.0.7 | 已存在 |
| Lab | 实验室/器材网格 | `Assets/_Project/Art/Models/GeneratedLab/Lab_*` | `LabVisualFactory` 程序生成，共用 `Lab_*` 资产族 | 已生成 |
| Lab | URP 材质 | `Assets/_Project/Art/Materials/GeneratedLab/Lab_*_MAT` | 项目原创程序材质，统一 `Lab_*_MAT` 命名 | 已生成 |
| Lab_SoftParticle_Alpha | 粒子透明贴图 | `Assets/_Project/Art/Textures/GeneratedLab/Lab_SoftParticle_Alpha.asset` | 程序生成 Alpha 贴图 | 已生成 |
| LabEnvironment | 实验室 Prefab | `Assets/_Project/Prefabs/Environment` | 场景构建器生成 | 已生成 |
| ExperimentBench | 工作台/线路/设备 Prefab | `Assets/_Project/Prefabs/Devices` | 场景构建器生成 | 已生成 |
| SafetyEquipment | 断电点/报警/灭火器 Prefab | `Assets/_Project/Prefabs/Interactables` | 场景构建器生成 | 已生成 |
| JouleEffects | 烟雾/火焰/火花 | `Assets/_Project/Prefabs/Effects` | 项目原创粒子和网格效果 | 已生成 |
| JouleLabUI | TMP UI 模块 | `Assets/_Project/Prefabs/UI` | 场景构建器生成 | 已生成 |

## 命名和路径

生成网格、材质、贴图使用共享 `Lab` 资产族，并在 Models、Materials、Textures 下采用一致相对目录。网格名以 `Lab_*` 开头，材质名格式为 `Lab_*_MAT`，贴图按用途使用 `Lab_*_BaseColor`、`Lab_*_Normal`、`Lab_*_Mask`、`Lab_*_Emission` 或 `Lab_*_Alpha`；当前粒子贴图为 `Lab_SoftParticle_Alpha`。

共享模型按形状和已烘焙米制尺寸命名，例如 `Lab_Cube_3p10000_0p08000_1p38000` 是实验台台面网格，使用 `Lab_Porcelain_MAT`；`Lab_Cylinder_*` 由导线、接头与仪器旋钮复用，依用途共享 `Lab_CableRed_MAT`、`Lab_Rubber_MAT`、`Lab_Brass_MAT` 或 `Lab_BrushedSteel_MAT`。同材质外观的零件共享同一个材质资源，符合第10.9节的复用要求，不为每个实例复制材质。`Lab_Flame_MAT` 和 `Lab_Smoke_MAT` 共同引用 `Lab_SoftParticle_Alpha`。Models、Materials、Textures 的对应相对目录均为 `GeneratedLab`。

当前生成网格已统一为 `Lab_*`。生成器保留对旧 `Mesh_*` 网格的兼容迁移，使用 `AssetDatabase.MoveAsset` 改名并保留 GUID，保持场景与 Prefab 引用。烟、火与火花的实际配置保存在 `Prefabs/Effects` 的 ParticleSystem 组件中；当前没有独立 VFX Graph 或效果配置文件，`Art/VFX` 通过 `.gitkeep` 保留目录。

正式模型在 `Art/Models`，材质在 `Art/Materials`，贴图在 `Art/Textures`，动画在 `Art/Animations`，VFX 在 `Art/VFX`，音频在 `Audio`。可重复使用的环境、设备、交互、效果和 UI 必须生成对应 `Prefabs` 资产。模型使用合理米制尺寸，Prefab 根 Scale 为 `(1,1,1)`，可交互对象使用简单 Collider。

本阶段未使用外部音频或图像；如后续加入，须先更新资产清单和第三方登记。无独立贴图的程序材质标记为“无贴图”，不创建虚假的源文件记录。

## 直接操控交互资源

`CO2Extinguisher` 继续使用本项目已有程序模型，瓶身位置与喷嘴朝向由用户操作实时控制，不使用一次点击后播放的灭火动画。喷射效果由开关持续状态驱动，熄火由有效命中累计时间决定。导线几何路径由 `WirePathGeometry` 共用，显示热点和选择位置均沿实际导线计算。上述空间交互代码不依赖 PC 输入，后续 VR 可复用器材和相同的拿取/移动/瞄准/扳机接口。

`CO2Extinguisher.prefab` 的根原点调整到瓶身，包含可转向的 `HornAimPivot` / `SprayNozzle`、`TriggerLever`、柔性软管、`CO2Spray`、瞄准线和瞄准标记。`ExtinguisherInteraction` 统一控制器材姿态和触发状态。`ExtinguisherStand.prefab` 单独保存地面标记与器材归位提示。选中的故障位置由线路上的 `SelectedFaultLocation` 标记显示。

新增标记材质为 `Lab_FaultMarker_MAT` 和 `Lab_AimMarker_MAT`，仍保存在 `Art/Materials/GeneratedLab`，不使用外部纹理。CO2 喷雾与瞄准线复用已有 `Lab_Smoke_MAT` 和 `Lab_SoftParticle_Alpha.asset`。本次资源已于 2026-10-14 重新生成，并核对磁盘文件与保存场景/Prefab 的实际引用。

## 正式生成资源逐项索引

本索引对应 2026-10-14 保存的 PC 主场景和 Prefab，共登记 120 个模型网格、22 个材质、1 个贴图和 17 个 Prefab。本次相对上版新增 6 个网格、2 个材质和 1 个 Prefab。以下路径均相对工程根目录，列出的资源均已生成。模型与材质的对应关系来自主场景和 Prefab 的实际 MeshFilter、MeshRenderer 引用，材质与贴图的对应关系来自材质资源中的 Texture 引用。材质名称对应下一表中的唯一材质路径，便于追查共享关系。

### 模型及实际材质引用

共享网格按几何形状和烘焙尺寸命名；同一网格可以由多个器材或多个材质外观复用。没有为每个实例复制网格和材质。列表记录资源的实际对应关系，不把通用前缀本身当作逐资产关联证明。

| 模型资产基础名称 | 模型资源路径 | 实际引用的共享材质 |
| --- | --- | --- |
| `Lab_Cube_0p00700_0p00700_0p04500` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cube_0p00700_0p00700_0p04500.asset` | `Lab_FaultMarker_MAT` |
| `Lab_Cube_0p04500_0p00700_0p00700` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cube_0p04500_0p00700_0p00700.asset` | `Lab_FaultMarker_MAT` |
| `Lab_Cylinder_0p01100_0p03092_0p01100` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cylinder_0p01100_0p03092_0p01100.asset` | `Lab_Brass_MAT` |
| `Lab_Cylinder_0p01100_0p04757_0p01100` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cylinder_0p01100_0p04757_0p01100.asset` | `Lab_Brass_MAT` |
| `Lab_Cylinder_0p08400_0p00600_0p08400` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cylinder_0p08400_0p00600_0p08400.asset` | `Lab_Rubber_MAT` |
| `Lab_Sphere_0p02500_0p02500_0p02500` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Sphere_0p02500_0p02500_0p02500.asset` | `Lab_AimMarker_MAT` |
| `Lab_Cube_0p00300_0p01200_0p00300` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cube_0p00300_0p01200_0p00300.asset` | `Lab_Porcelain_MAT` |
| `Lab_Cube_0p00300_0p14000_0p01400` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cube_0p00300_0p14000_0p01400.asset` | `Lab_Rubber_MAT` |
| `Lab_Cube_0p00400_0p00200_0p01400` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cube_0p00400_0p00200_0p01400.asset` | `Lab_Graphite_MAT` |
| `Lab_Cube_0p00400_0p00200_0p02600` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cube_0p00400_0p00200_0p02600.asset` | `Lab_Graphite_MAT` |
| `Lab_Cube_0p00900_0p00300_8p00000` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cube_0p00900_0p00300_8p00000.asset` | `Lab_BrushedSteel_MAT` |
| `Lab_Cube_0p01200_0p05000_0p29000` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cube_0p01200_0p05000_0p29000.asset` | `Lab_BrushedSteel_MAT` |
| `Lab_Cube_0p01400_0p36000_0p01400` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cube_0p01400_0p36000_0p01400.asset` | `Lab_BrushedSteel_MAT` |
| `Lab_Cube_0p02300_0p00300_0p00400` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cube_0p02300_0p00300_0p00400.asset` | `Lab_Graphite_MAT` |
| `Lab_Cube_0p03000_1p45000_2p13000` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cube_0p03000_1p45000_2p13000.asset` | `Lab_Window_MAT` |
| `Lab_Cube_0p03500_0p03200_2p13000` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cube_0p03500_0p03200_2p13000.asset` | `Lab_Porcelain_MAT` |
| `Lab_Cube_0p03500_1p45000_0p03000` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cube_0p03500_1p45000_0p03000.asset` | `Lab_Porcelain_MAT` |
| `Lab_Cube_0p03600_0p00300_0p07400` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cube_0p03600_0p00300_0p07400.asset` | `Lab_Brass_MAT`、`Lab_Graphite_MAT` |
| `Lab_Cube_0p04700_0p02500_0p30000` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cube_0p04700_0p02500_0p30000.asset` | `Lab_Rubber_MAT` |
| `Lab_Cube_0p06000_0p02300_0p03000` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cube_0p06000_0p02300_0p03000.asset` | `Lab_Teal_MAT` |
| `Lab_Cube_0p06000_1p55000_2p25000` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cube_0p06000_1p55000_2p25000.asset` | `Lab_Graphite_MAT` |
| `Lab_Cube_0p06400_0p02600_0p06400` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cube_0p06400_0p02600_0p06400.asset` | `Lab_Graphite_MAT` |
| `Lab_Cube_0p06500_0p74000_0p06500` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cube_0p06500_0p74000_0p06500.asset` | `Lab_BrushedSteel_MAT` |
| `Lab_Cube_0p07100_0p03200_0p08500` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cube_0p07100_0p03200_0p08500.asset` | `Lab_Porcelain_MAT` |
| `Lab_Cube_0p07500_0p08500_0p01000` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cube_0p07500_0p08500_0p01000.asset` | `Lab_Rubber_MAT` |
| `Lab_Cube_0p08800_0p04200_0p08800` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cube_0p08800_0p04200_0p08800.asset` | `Lab_Rubber_MAT` |
| `Lab_Cube_0p12000_0p05000_0p07000` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cube_0p12000_0p05000_0p07000.asset` | `Lab_Brass_MAT` |
| `Lab_Cube_0p13000_0p02500_0p03200` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cube_0p13000_0p02500_0p03200.asset` | `Lab_BrushedSteel_MAT` |
| `Lab_Cube_0p14000_0p15000_0p01200` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cube_0p14000_0p15000_0p01200.asset` | `Lab_Porcelain_MAT` |
| `Lab_Cube_0p15000_0p16000_0p14000` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cube_0p15000_0p16000_0p14000.asset` | `Lab_Porcelain_MAT` |
| `Lab_Cube_0p15000_0p23000_0p00400` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cube_0p15000_0p23000_0p00400.asset` | `Lab_Porcelain_MAT` |
| `Lab_Cube_0p16000_0p01200_0p03700` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cube_0p16000_0p01200_0p03700.asset` | `Lab_BrushedSteel_MAT` |
| `Lab_Cube_0p16000_0p03400_0p04700` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cube_0p16000_0p03400_0p04700.asset` | `Lab_Rubber_MAT` |
| `Lab_Cube_0p16000_3p30000_4p60000` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cube_0p16000_3p30000_4p60000.asset` | `Lab_Wall_MAT` |
| `Lab_Cube_0p17000_0p03000_0p04000` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cube_0p17000_0p03000_0p04000.asset` | `Lab_Rubber_MAT` |
| `Lab_Cube_0p20000_0p30000_0p13000` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cube_0p20000_0p30000_0p13000.asset` | `Lab_CableRed_MAT` |
| `Lab_Cube_0p24000_0p02800_0p03200` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cube_0p24000_0p02800_0p03200.asset` | `Lab_BrushedSteel_MAT` |
| `Lab_Cube_0p24000_0p19000_0p00600` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cube_0p24000_0p19000_0p00600.asset` | `Lab_Screen_MAT` |
| `Lab_Cube_0p25000_0p04700_0p20000` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cube_0p25000_0p04700_0p20000.asset` | `Lab_BrushedSteel_MAT` |
| `Lab_Cube_0p25200_0p01100_0p08300` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cube_0p25200_0p01100_0p08300.asset` | `Lab_Screen_MAT` |
| `Lab_Cube_0p27600_0p09600_0p00700` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cube_0p27600_0p09600_0p00700.asset` | `Lab_Screen_MAT` |
| `Lab_Cube_0p29000_0p09800_0p01200` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cube_0p29000_0p09800_0p01200.asset` | `Lab_Porcelain_MAT` |
| `Lab_Cube_0p30000_0p06500_0p11000` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cube_0p30000_0p06500_0p11000.asset` | `Lab_Graphite_MAT` |
| `Lab_Cube_0p30000_0p11800_0p01400` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cube_0p30000_0p11800_0p01400.asset` | `Lab_Rubber_MAT` |
| `Lab_Cube_0p34000_0p03600_0p14000` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cube_0p34000_0p03600_0p14000.asset` | `Lab_Graphite_MAT` |
| `Lab_Cube_0p38000_0p27000_0p25000` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cube_0p38000_0p27000_0p25000.asset` | `Lab_Porcelain_MAT`、`Lab_Teal_MAT` |
| `Lab_Cube_0p40000_0p20000_0p32000` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cube_0p40000_0p20000_0p32000.asset` | `Lab_Teal_MAT` |
| `Lab_Cube_0p45200_0p34500_0p01900` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cube_0p45200_0p34500_0p01900.asset` | `Lab_Graphite_MAT` |
| `Lab_Cube_0p46000_0p30000_0p28000` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cube_0p46000_0p30000_0p28000.asset` | `Lab_Porcelain_MAT` |
| `Lab_Cube_0p48000_0p38000_0p40000` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cube_0p48000_0p38000_0p40000.asset` | `Lab_Porcelain_MAT` |
| `Lab_Cube_0p50000_0p00700_0p50000` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cube_0p50000_0p00700_0p50000.asset` | `Lab_Teal_MAT` |
| `Lab_Cube_0p50000_0p06500_0p42000` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cube_0p50000_0p06500_0p42000.asset` | `Lab_Graphite_MAT` |
| `Lab_Cube_0p58000_0p22000_0p03500` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cube_0p58000_0p22000_0p03500.asset` | `Lab_Teal_MAT` |
| `Lab_Cube_0p62000_0p30000_0p93000` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cube_0p62000_0p30000_0p93000.asset` | `Lab_Graphite_MAT` |
| `Lab_Cube_0p68000_0p72000_0p03500` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cube_0p68000_0p72000_0p03500.asset` | `Lab_Porcelain_MAT` |
| `Lab_Cube_0p70000_0p09000_0p48000` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cube_0p70000_0p09000_0p48000.asset` | `Lab_Rubber_MAT` |
| `Lab_Cube_0p72000_0p82000_0p68000` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cube_0p72000_0p82000_0p68000.asset` | `Lab_Graphite_MAT` |
| `Lab_Cube_1p12000_0p32000_0p00600` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cube_1p12000_0p32000_0p00600.asset` | `Lab_Acrylic_MAT` |
| `Lab_Cube_1p14000_0p01300_0p55000` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cube_1p14000_0p01300_0p55000.asset` | `Lab_Ceramic_MAT` |
| `Lab_Cube_1p22000_0p04500_0p64000` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cube_1p22000_0p04500_0p64000.asset` | `Lab_BrushedSteel_MAT` |
| `Lab_Cube_1p40000_1p18000_0p02400` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cube_1p40000_1p18000_0p02400.asset` | `Lab_Graphite_MAT` |
| `Lab_Cube_2p60000_0p04000_0p38000` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cube_2p60000_0p04000_0p38000.asset` | `Lab_CeilingLight_MAT` |
| `Lab_Cube_2p70000_0p05500_0p05000` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cube_2p70000_0p05500_0p05000.asset` | `Lab_BrushedSteel_MAT` |
| `Lab_Cube_2p80000_0p04000_0p18000` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cube_2p80000_0p04000_0p18000.asset` | `Lab_CeilingLight_MAT` |
| `Lab_Cube_3p00000_0p06000_0p05500` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cube_3p00000_0p06000_0p05500.asset` | `Lab_BrushedSteel_MAT` |
| `Lab_Cube_3p10000_0p06600_0p02500` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cube_3p10000_0p06600_0p02500.asset` | `Lab_Graphite_MAT` |
| `Lab_Cube_3p10000_0p08000_1p38000` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cube_3p10000_0p08000_1p38000.asset` | `Lab_Porcelain_MAT` |
| `Lab_Cube_3p66000_0p04500_0p43000` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cube_3p66000_0p04500_0p43000.asset` | `Lab_BrushedSteel_MAT` |
| `Lab_Cube_3p82000_0p06500_0p78000` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cube_3p82000_0p06500_0p78000.asset` | `Lab_BrushedSteel_MAT` |
| `Lab_Cube_8p00000_0p00300_0p00900` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cube_8p00000_0p00300_0p00900.asset` | `Lab_BrushedSteel_MAT` |
| `Lab_Cube_8p00000_0p02500_0p02000` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cube_8p00000_0p02500_0p02000.asset` | `Lab_Brass_MAT` |
| `Lab_Cube_8p00000_0p08500_0p04000` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cube_8p00000_0p08500_0p04000.asset` | `Lab_Graphite_MAT` |
| `Lab_Cube_8p00000_0p10000_8p00000` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cube_8p00000_0p10000_8p00000.asset` | `Lab_Floor_MAT` |
| `Lab_Cube_8p00000_0p85000_0p02000` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cube_8p00000_0p85000_0p02000.asset` | `Lab_Teal_MAT` |
| `Lab_Cube_8p00000_3p30000_0p16000` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cube_8p00000_3p30000_0p16000.asset` | `Lab_Wall_MAT` |
| `Lab_Cylinder_0p00500_0p04901_0p00500` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cylinder_0p00500_0p04901_0p00500.asset` | `Lab_Brass_MAT` |
| `Lab_Cylinder_0p00500_0p07520_0p00500` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cylinder_0p00500_0p07520_0p00500.asset` | `Lab_Brass_MAT` |
| `Lab_Cylinder_0p00500_0p07603_0p00500` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cylinder_0p00500_0p07603_0p00500.asset` | `Lab_Brass_MAT` |
| `Lab_Cylinder_0p01100_0p04950_0p01100` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cylinder_0p01100_0p04950_0p01100.asset` | `Lab_Brass_MAT` |
| `Lab_Cylinder_0p01100_0p05895_0p01100` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cylinder_0p01100_0p05895_0p01100.asset` | `Lab_Brass_MAT` |
| `Lab_Cylinder_0p01200_0p00250_0p01200` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cylinder_0p01200_0p00250_0p01200.asset` | `Lab_BrushedSteel_MAT` |
| `Lab_Cylinder_0p01400_0p06205_0p01400` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cylinder_0p01400_0p06205_0p01400.asset` | `Lab_CableRed_MAT` |
| `Lab_Cylinder_0p01400_0p06804_0p01400` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cylinder_0p01400_0p06804_0p01400.asset` | `Lab_CableRed_MAT` |
| `Lab_Cylinder_0p01400_0p07106_0p01400` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cylinder_0p01400_0p07106_0p01400.asset` | `Lab_Rubber_MAT` |
| `Lab_Cylinder_0p01400_0p08061_0p01400` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cylinder_0p01400_0p08061_0p01400.asset` | `Lab_CableRed_MAT` |
| `Lab_Cylinder_0p01400_0p08139_0p01400` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cylinder_0p01400_0p08139_0p01400.asset` | `Lab_CableRed_MAT` |
| `Lab_Cylinder_0p01400_0p08142_0p01400` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cylinder_0p01400_0p08142_0p01400.asset` | `Lab_CableRed_MAT` |
| `Lab_Cylinder_0p01400_0p09500_0p01400` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cylinder_0p01400_0p09500_0p01400.asset` | `Lab_CableRed_MAT` |
| `Lab_Cylinder_0p01400_0p10284_0p01400` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cylinder_0p01400_0p10284_0p01400.asset` | `Lab_Rubber_MAT` |
| `Lab_Cylinder_0p01400_0p13973_0p01400` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cylinder_0p01400_0p13973_0p01400.asset` | `Lab_CableRed_MAT` |
| `Lab_Cylinder_0p01400_0p20524_0p01400` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cylinder_0p01400_0p20524_0p01400.asset` | `Lab_Rubber_MAT` |
| `Lab_Cylinder_0p01400_0p21506_0p01400` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cylinder_0p01400_0p21506_0p01400.asset` | `Lab_Rubber_MAT` |
| `Lab_Cylinder_0p01400_0p23587_0p01400` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cylinder_0p01400_0p23587_0p01400.asset` | `Lab_Rubber_MAT` |
| `Lab_Cylinder_0p01400_0p25926_0p01400` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cylinder_0p01400_0p25926_0p01400.asset` | `Lab_Rubber_MAT` |
| `Lab_Cylinder_0p01700_0p06500_0p01700` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cylinder_0p01700_0p06500_0p01700.asset` | `Lab_Rubber_MAT` |
| `Lab_Cylinder_0p01700_0p07000_0p01700` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cylinder_0p01700_0p07000_0p01700.asset` | `Lab_Rubber_MAT` |
| `Lab_Cylinder_0p01700_0p08559_0p01700` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cylinder_0p01700_0p08559_0p01700.asset` | `Lab_Rubber_MAT` |
| `Lab_Cylinder_0p01800_0p04561_0p01800` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cylinder_0p01800_0p04561_0p01800.asset` | `Lab_HotWire_MAT` |
| `Lab_Cylinder_0p01800_0p05289_0p01800` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cylinder_0p01800_0p05289_0p01800.asset` | `Lab_HotWire_MAT` |
| `Lab_Cylinder_0p01800_0p06099_0p01800` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cylinder_0p01800_0p06099_0p01800.asset` | `Lab_HotWire_MAT` |
| `Lab_Cylinder_0p01800_0p07501_0p01800` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cylinder_0p01800_0p07501_0p01800.asset` | `Lab_HotWire_MAT` |
| `Lab_Cylinder_0p02200_0p01800_0p02200` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cylinder_0p02200_0p01800_0p02200.asset` | `Lab_Brass_MAT` |
| `Lab_Cylinder_0p03200_0p01250_0p03200` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cylinder_0p03200_0p01250_0p03200.asset` | `Lab_Brass_MAT` |
| `Lab_Cylinder_0p03500_0p01250_0p03500` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cylinder_0p03500_0p01250_0p03500.asset` | `Lab_CableRed_MAT`、`Lab_Rubber_MAT` |
| `Lab_Cylinder_0p05100_0p05000_0p05100` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cylinder_0p05100_0p05000_0p05100.asset` | `Lab_Brass_MAT` |
| `Lab_Cylinder_0p05200_0p01750_0p05200` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cylinder_0p05200_0p01750_0p05200.asset` | `Lab_BrushedSteel_MAT` |
| `Lab_Cylinder_0p06900_0p08000_0p06900` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cylinder_0p06900_0p08000_0p06900.asset` | `Lab_Rubber_MAT` |
| `Lab_Cylinder_0p08000_0p01300_0p08000` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cylinder_0p08000_0p01300_0p08000.asset` | `Lab_CableRed_MAT` |
| `Lab_Cylinder_0p22000_0p28500_0p22000` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cylinder_0p22000_0p28500_0p22000.asset` | `Lab_CableRed_MAT` |
| `Lab_Cylinder_0p23000_0p01250_0p23000` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cylinder_0p23000_0p01250_0p23000.asset` | `Lab_Rubber_MAT` |
| `Lab_Cylinder_0p26000_0p15000_0p26000` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Cylinder_0p26000_0p15000_0p26000.asset` | `Lab_Rubber_MAT` |
| `Lab_Sphere_0p00500_0p00500_0p00500` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Sphere_0p00500_0p00500_0p00500.asset` | `Lab_Brass_MAT` |
| `Lab_Sphere_0p01100_0p01100_0p01100` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Sphere_0p01100_0p01100_0p01100.asset` | `Lab_Brass_MAT` |
| `Lab_Sphere_0p01400_0p01400_0p01400` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Sphere_0p01400_0p01400_0p01400.asset` | `Lab_CableRed_MAT`、`Lab_Rubber_MAT` |
| `Lab_Sphere_0p01700_0p01700_0p01700` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Sphere_0p01700_0p01700_0p01700.asset` | `Lab_Rubber_MAT` |
| `Lab_Sphere_0p01800_0p01800_0p01800` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Sphere_0p01800_0p01800_0p01800.asset` | `Lab_HotWire_MAT` |
| `Lab_Sphere_0p01900_0p01900_0p00600` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Sphere_0p01900_0p01900_0p00600.asset` | `Lab_PowerLED_MAT` |
| `Lab_Sphere_0p02400_0p00900_0p00500` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Sphere_0p02400_0p00900_0p00500.asset` | `Lab_BreakerLED_MAT` |
| `Lab_Sphere_0p05000_0p03200_0p03800` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Sphere_0p05000_0p03200_0p03800.asset` | `Lab_HotWire_MAT` |
| `Lab_Sphere_0p22000_0p12000_0p22000` | `Assets/_Project/Art/Models/GeneratedLab/Lab_Sphere_0p22000_0p12000_0p22000.asset` | `Lab_CableRed_MAT` |

### 材质及贴图引用

| 材质资产基础名称 | 材质资源路径 | 贴图引用及用途 |
| --- | --- | --- |
| `Lab_Acrylic` | `Assets/_Project/Art/Materials/GeneratedLab/Lab_Acrylic_MAT.mat` | 无贴图；使用颜色、金属度、光滑度或自发光参数 |
| `Lab_Brass` | `Assets/_Project/Art/Materials/GeneratedLab/Lab_Brass_MAT.mat` | 无贴图；使用颜色、金属度、光滑度或自发光参数 |
| `Lab_BreakerLED` | `Assets/_Project/Art/Materials/GeneratedLab/Lab_BreakerLED_MAT.mat` | 无贴图；使用颜色、金属度、光滑度或自发光参数 |
| `Lab_BrushedSteel` | `Assets/_Project/Art/Materials/GeneratedLab/Lab_BrushedSteel_MAT.mat` | 无贴图；使用颜色、金属度、光滑度或自发光参数 |
| `Lab_CableRed` | `Assets/_Project/Art/Materials/GeneratedLab/Lab_CableRed_MAT.mat` | 无贴图；使用颜色、金属度、光滑度或自发光参数 |
| `Lab_CeilingLight` | `Assets/_Project/Art/Materials/GeneratedLab/Lab_CeilingLight_MAT.mat` | 无贴图；使用颜色、金属度、光滑度或自发光参数 |
| `Lab_Ceramic` | `Assets/_Project/Art/Materials/GeneratedLab/Lab_Ceramic_MAT.mat` | 无贴图；使用颜色、金属度、光滑度或自发光参数 |
| `Lab_DisplayGlass` | `Assets/_Project/Art/Materials/GeneratedLab/Lab_DisplayGlass_MAT.mat` | 无贴图；使用颜色、金属度、光滑度或自发光参数 |
| `Lab_AimMarker` | `Assets/_Project/Art/Materials/GeneratedLab/Lab_AimMarker_MAT.mat` | 无贴图；喷嘴瞄准位置标记 |
| `Lab_FaultMarker` | `Assets/_Project/Art/Materials/GeneratedLab/Lab_FaultMarker_MAT.mat` | 无贴图；连续故障选点标记 |
| `Lab_Flame` | `Assets/_Project/Art/Materials/GeneratedLab/Lab_Flame_MAT.mat` | `Lab_SoftParticle_Alpha` |
| `Lab_Floor` | `Assets/_Project/Art/Materials/GeneratedLab/Lab_Floor_MAT.mat` | 无贴图；使用颜色、金属度、光滑度或自发光参数 |
| `Lab_Graphite` | `Assets/_Project/Art/Materials/GeneratedLab/Lab_Graphite_MAT.mat` | 无贴图；使用颜色、金属度、光滑度或自发光参数 |
| `Lab_HotWire` | `Assets/_Project/Art/Materials/GeneratedLab/Lab_HotWire_MAT.mat` | 无贴图；使用颜色、金属度、光滑度或自发光参数 |
| `Lab_Porcelain` | `Assets/_Project/Art/Materials/GeneratedLab/Lab_Porcelain_MAT.mat` | 无贴图；使用颜色、金属度、光滑度或自发光参数 |
| `Lab_PowerLED` | `Assets/_Project/Art/Materials/GeneratedLab/Lab_PowerLED_MAT.mat` | 无贴图；使用颜色、金属度、光滑度或自发光参数 |
| `Lab_Rubber` | `Assets/_Project/Art/Materials/GeneratedLab/Lab_Rubber_MAT.mat` | 无贴图；使用颜色、金属度、光滑度或自发光参数 |
| `Lab_Screen` | `Assets/_Project/Art/Materials/GeneratedLab/Lab_Screen_MAT.mat` | 无贴图；使用颜色、金属度、光滑度或自发光参数 |
| `Lab_Smoke` | `Assets/_Project/Art/Materials/GeneratedLab/Lab_Smoke_MAT.mat` | `Lab_SoftParticle_Alpha` |
| `Lab_Teal` | `Assets/_Project/Art/Materials/GeneratedLab/Lab_Teal_MAT.mat` | 无贴图；使用颜色、金属度、光滑度或自发光参数 |
| `Lab_Wall` | `Assets/_Project/Art/Materials/GeneratedLab/Lab_Wall_MAT.mat` | 无贴图；使用颜色、金属度、光滑度或自发光参数 |
| `Lab_Window` | `Assets/_Project/Art/Materials/GeneratedLab/Lab_Window_MAT.mat` | 无贴图；使用颜色、金属度、光滑度或自发光参数 |

### 贴图

| 贴图资产基础名称 | 贴图资源路径 | 用途和引用 |
| --- | --- | --- |
| `Lab_SoftParticle` | `Assets/_Project/Art/Textures/GeneratedLab/Lab_SoftParticle_Alpha.asset` | Alpha 透明度；由 `Lab_Flame_MAT` 和 `Lab_Smoke_MAT` 共享 |

### Prefab

| 资产基础名称 | Prefab 资源路径 | 类别 |
| --- | --- | --- |
| `DCPowerSupply` | `Assets/_Project/Prefabs/Devices/DCPowerSupply.prefab` | 设备 |
| `LoadResistor` | `Assets/_Project/Prefabs/Devices/LoadResistor.prefab` | 设备 |
| `TemperatureSensor` | `Assets/_Project/Prefabs/Devices/TemperatureSensor.prefab` | 设备 |
| `TestWireHolder` | `Assets/_Project/Prefabs/Devices/TestWireHolder.prefab` | 设备 |
| `HotWireGlow` | `Assets/_Project/Prefabs/Effects/HotWireGlow.prefab` | 效果 |
| `ShortCircuitSparks` | `Assets/_Project/Prefabs/Effects/ShortCircuitSparks.prefab` | 效果 |
| `ThermalOverlayMarker` | `Assets/_Project/Prefabs/Effects/ThermalOverlayMarker.prefab` | 效果 |
| `WireFlames` | `Assets/_Project/Prefabs/Effects/WireFlames.prefab` | 效果 |
| `WireSmoke` | `Assets/_Project/Prefabs/Effects/WireSmoke.prefab` | 效果 |
| `LaboratoryBench` | `Assets/_Project/Prefabs/Environment/LaboratoryBench.prefab` | 环境 |
| `LaboratoryRoom` | `Assets/_Project/Prefabs/Environment/LaboratoryRoom.prefab` | 环境 |
| `AlarmButton` | `Assets/_Project/Prefabs/Interactables/AlarmButton.prefab` | 交互对象 |
| `CircuitBreaker` | `Assets/_Project/Prefabs/Interactables/CircuitBreaker.prefab` | 交互对象 |
| `CO2Extinguisher` | `Assets/_Project/Prefabs/Interactables/CO2Extinguisher.prefab` | 交互对象 |
| `ExtinguisherStand` | `Assets/_Project/Prefabs/Interactables/ExtinguisherStand.prefab` | 灭火器原位标记 |
| `FaultInjectionHandle` | `Assets/_Project/Prefabs/Interactables/FaultInjectionHandle.prefab` | 交互对象 |
| `LaboratoryInterface` | `Assets/_Project/Prefabs/UI/LaboratoryInterface.prefab` | 界面 |

### 场景和渲染配置

| 资产基础名称 | 资源路径 | 用途 |
| --- | --- | --- |
| `01_PC_Main` | `Assets/_Project/Scenes/PC/01_PC_Main.unity` | 正式 PC 实验主场景，唯一启用的 Build 场景 |
| `LabVolumeProfile` | `Assets/_Project/Settings/LabVolumeProfile.asset` | 实验室后期处理配置 |
| `PC_UniversalRenderer` | `Assets/_Project/Settings/PC_UniversalRenderer.asset` | PC URP Renderer |
| `PC_UniversalRenderPipeline` | `Assets/_Project/Settings/PC_UniversalRenderPipeline.asset` | PC URP 管线配置 |
| `UniversalRenderPipelineGlobalSettings` | `Assets/_Project/Settings/UniversalRenderPipelineGlobalSettings.asset` | URP 全局配置 |

字体源、静态 TMP 中文字体及许可已在本文件开头和 `ThirdPartyAssets.md` 单独登记。声音由 `Assets/_Project/Scripts/Experiment/JouleLabAudio.cs` 运行时生成，当前没有独立 AudioClip 文件；特效参数保存在上述效果 Prefab 中，当前没有独立 VFX Graph 或 AnimationClip 文件，因此对应预留目录只保留 `.gitkeep`。
