# 0.1.5 高质量缩图

2026-11-07。用户确认人脉网络的拾荒者服务显示和购买正常；三级服务、箱子占格、开箱及无卡顿此前已实机确认。

用户要求改进大图缩小方式，并在并排预览后明确确认“右侧的效果明显好于左侧”。本版采用该右侧素材：原v4源图经HighQualityBicubic缩至48×48，保留原色阶和透明度，不限20色、不做抖色、不做额外锐化。最近邻仅用于把对比预览整数倍放大，便于展示真实运行时像素，并非用于生成本版48×48素材。

可复现命令：

```powershell
./tools/prepare-art.ps1 -GeneratedImage ./assets/source/printer-crate-original-style-two-spools-v4.png -SourceFileName printer-crate-original-style-two-spools-v4.png -ReuseSource -ResizeFilter HighQualityBicubic
```

源图及编辑提示词仍见assets/source/PROMPT-v4.md。旧版最近邻48×48图保存在assets/source/printer-crate-v4-nearest-48.png；对比图为outputs/resampling-v5-comparison.png，左旧右新。

生产C#代码不变，运行时DLL哈希与0.1.4相同。检查透明PNG尺寸、包结构和安装文件哈希，不为美术缩放重复完整功能测试。安装前备份旧Mod目录，收据为outputs/installation-0.1.5.json。该预览已获用户认可，游戏中的最终观感仍待复测。
