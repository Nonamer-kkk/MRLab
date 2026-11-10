# Hunyuan3D 图片建模集成

当前路线是本地 Hunyuan3D 推理服务，不再把原有 Unity 网格作为建模输入。

## 架构

`Unity 编辑器窗口 → HTTP 图片上传 → Hunyuan3D-2mini Turbo → OBJ → Unity AssetDatabase → 当前场景`

Hunyuan 只负责图片到无纹理网格的推理。Unity 负责选择图片、请求服务、保存 OBJ、导入模型和放置场景。生成后的对象是普通 Unity 模型，可以在场景中移动、旋转、缩放、添加材质和碰撞体。

## 启动服务

先启动本地服务：

```powershell
& 'E:/Unity Project/MRLab_E10_24371119/SourceAssets/Hunyuan3D/start_hunyuan_service.ps1'
```

首次启动会从 Hugging Face 下载 `tencent/Hunyuan3D-2mini` 的 `hunyuan3d-dit-v2-mini-turbo` 权重。当前配置只运行形状推理，不启用纹理模型；RTX 4060 8GB 以 128 八叉树分辨率、5 步推理为默认值。

## Unity 操作

1. 保持 Hunyuan 服务窗口运行。
2. 打开 Unity 工程，选择菜单 `MRLab → Hunyuan3D → 从图片生成模型`。
3. 选择一张物体图片，输入模型名称，点击“生成并导入到当前场景”。
4. 生成的文件保存在 `Assets/_Project/Art/Models/HunyuanGenerated/`，并自动实例化到当前场景原点。
5. 生成对象可以像普通 Unity 对象一样移动、旋转、缩放、挂载材质和 Collider，然后保存场景。

图片应尽量单物体、背景干净、主体完整。服务会返回无纹理 OBJ；材质和纹理可在 Unity 中手动添加，或后续单独接入 Hunyuan Paint。

## 边界

这条链路不读取工程中已有 Unity 模型作为输入。Unity Player 运行时不执行大模型推理；一键生成发生在 Unity Editor，生成后再进入场景和 PC 构建。
