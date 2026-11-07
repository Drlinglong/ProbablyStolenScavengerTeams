# 从源码构建

源码根目录就是本仓库。游戏运行时代码在src，Mod清单、说明和最终图标在content，AI美术源图及提示词在assets/source，验证代码在verify。package、outputs、bin、obj均为忽略的生成目录。

## 已实际运行的构建方式

需要Windows、PowerShell、合法安装的Probably Stolen，以及可在Windows运行的Roslyn csc.exe（支持C# 9）。编译器可从Microsoft.Net.Compilers.Toolset获得；它不是游戏DLL，不随Mod分发。本机编译器安装在J:/codex-tools/probably-stolen-mod/compiler/tasks/net472/csc.exe，迁移环境时通过参数提供自己的路径。

```powershell
./build.ps1 -GameRoot 'I:\SteamLibrary\steamapps\common\Probably Stolen' -Compiler 'J:\codex-tools\probably-stolen-mod\compiler\tasks\net472\csc.exe'
```

build.ps1引用游戏Probably Stolen_Data/Managed中的程序集，启用确定性构建和warnings-as-errors，输出package/ScavengerTeams。只生成本地包，不自动安装；游戏DLL不会复制进包。

原项目还提供ScavengerTeams.csproj，目标netstandard2.1。装有.NET SDK的机器可使用：

```powershell
dotnet build ./ScavengerTeams.csproj -c Release -p:GameRoot='I:\SteamLibrary\steamapps\common\Probably Stolen'
```

此SDK入口在当前只有运行时、没有SDK的机器上没有运行验证。它输出bin下的DLL，不自动组装发行目录；需另附content里的manifest.xml、README.zh-CN.md和Textures文件夹。可移植构建不能依赖作者机器上的硬编码默认路径，应显式设置GameRoot和Compiler。

## 代码与资源验证

```powershell
./verify.ps1 -Compiler 'J:\codex-tools\probably-stolen-mod\compiler\tasks\net472\csc.exe'
python ./verify/validate_artifact.py
```

verify.ps1将生产逻辑与小型宿主替身一起编译，覆盖供货、独立状态、价格、开箱及异常路径；0.2.0为56项通过。这不替代Unity加载、真实库存装箱和游戏存读档。后者已由用户针对0.2.0四条路线完成实测。

图像检查脚本需要Pillow；本机脚本目前兼容本地既有工具依赖目录，其他环境可自行安装Pillow到Python环境。脚本检查包结构、UTF-8、48×48透明PNG及不同素材哈希，写入outputs/verification/candidate-receipt.json。

已有最终贴图在content/Textures/Items，无需重新生成美术即可构建。需要从已保存源图重新缩制时，使用tools/prepare-art.ps1；加工参数和图像生成记录见各版本美术文档与assets/source/PROMPTS-0.2.0.md。

## 安装与验收基线

游戏关闭后备份现有独立Mod目录，将package/ScavengerTeams整个文件夹放入游戏Mods。由玩家在游戏中启用并按提示重启。不要修改原版DLL、Workshop缓存、启用/信任记录或玩家存档文件。

0.2.0实测基线：Steam Build26496118，游戏bundleVersion0.46D；对应Assembly-CSharp.dll SHA-256为d9abc029f6b390619c31175df5735901aec6dc071c35c50ce5b3fdf92e3ede46。其他游戏构建及其他Mod组合需要另行确认。

测试资金Mod是另一个私用仓库，不属于本仓库或此发行包。
