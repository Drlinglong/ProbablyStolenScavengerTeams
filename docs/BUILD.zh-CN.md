# 源码与构建

完整可移植步骤见[英文构建说明](BUILD.md)。需要Windows PowerShell、合法安装的Probably Stolen、支持C#9的Roslyn csc.exe和Python/Pillow。

```powershell
$env:PROBABLY_STOLEN_GAME_ROOT = 'D:\SteamLibrary\steamapps\common\Probably Stolen'
$env:CSC_PATH = 'D:\Tools\Microsoft.Net.Compilers.Toolset\tasks\net472\csc.exe'
./verify.ps1
./build.ps1
python -m pip install Pillow
python ./verify/validate_artifact.py --game-root $env:PROBABLY_STOLEN_GAME_ROOT
```

也可显式传入-GameRoot和-Compiler参数。编译器可从Microsoft.Net.Compilers.Toolset 4.12.0获得，游戏程序集仅作为本机编译引用，不收入包。输出为package/ScavengerTeams，不自动安装。原SDK项目也可传入GameRoot构建DLL，但该入口未在当前无SDK的机器上验证，且不自动打完整发行包。

68项宿主回归覆盖玩法、四路独立存档、异常路径及八语分句链。结构检查包含八语46键、格式参数、标签、分句、透明贴图及发行文件一致性；不等于Unity显示或人工语言质量验收。

用户已实测0.2.0全部路线、箱图和开箱；之后已确认0.2.1中文文案、各级问候及分句正常。非中文语言仍待实机显示确认。实测游戏基线为Steam Build26496118、bundleVersion0.46D。

游戏关闭后备份旧ScavengerTeams目录再安装完整包，由游戏界面启用，不修改玩家存档、原版DLL、Workshop缓存或信任记录。测试资金Mod始终留在另一个私用仓库，不属于本源码或发行包。
