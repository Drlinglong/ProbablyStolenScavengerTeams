# 0.1.6 放大箱图与原版上货规则

2026-11-07。

用户要求箱图轮廓更清楚、图像放大20%，并取消供货时腾柜台重试，采用原版放不下就销毁的规则。

## 美术

使用同一v4源图。首遍高质量缩图确定透明度64以上的视觉中心，再从源图裁取1/1.2尺寸视窗重采样为48×48，不放大旧48×48图。超过可容纳的可见尺寸会拒绝处理，避免裁掉箱体。仅在强亮度边界轻度锐化0.25，按透明度加权邻域颜色，降低黑色边缘污染；透明度使用连续平滑曲线增强轮廓，不二值化、不限色、不抖色。

透明度128以上的旧边界为38×38，新边界46×45，像素取整后约放大20%。占格仍为实心3×3。

```powershell
./tools/prepare-art.ps1 -GeneratedImage ./assets/source/printer-crate-original-style-two-spools-v4.png -SourceFileName printer-crate-original-style-two-spools-v4.png -ReuseSource -ResizeFilter HighQualityBicubic -VisualScale 1.2 -EdgeSharpness 0.25
```

预览outputs/size-edge-v6-comparison.png，左旧右新。

## 上货

删除整批回滚及“先腾点柜台空间”“重新摆货”分支。按索引先摆全部材料或箱子，再摆垃圾。原版TryAddDirectSellingItemToTable返回false时已销毁无法放下的物品，不再次手工销毁。

兼容保留soldMask字段，记录已购买或已被原版销毁的库存位。读档不补回这些物品。整批都被销毁时该批结束，仍按正常来访计算日期和首次优惠。材料优先只优先分配可用空间；如果连材料都放不下，也会按原版销毁。

开箱的空间不足保护与失败回滚不变。价格、等级、存档ID、周期及折扣不变。

## 验证

27项宿主回归通过，包括材料优先、垃圾溢出不回滚、2级箱子优先、丢弃后读档不补货和柜台全满时结束当批。对当前游戏程序集编译及图片/包结构检查通过。安装前备份旧版，收据为outputs/installation-0.1.6.json。边缘美术和新上货行为仍待用户游戏内复测。
