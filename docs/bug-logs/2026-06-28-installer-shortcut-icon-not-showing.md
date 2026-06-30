# 2026-06-28 安装包桌面快捷方式图标不显示问题记录

## 问题现象

使用 Visual Studio Installer Projects 打包并安装 `DynamicIslandBar` 后，桌面快捷方式没有显示项目图标，而是显示 Windows 默认的白蓝色程序图标。

当时已经把 `DynamicIslandBar-AppIcon.ico` 添加到了 Setup Project，并且快捷方式看起来也设置了 `Icon`，但安装后的桌面图标仍然不生效。

## 排查过程

1. 检查图标文件本身是否有效。

   使用 Pillow 读取 `DynamicIslandBar-AppIcon.ico`，确认它是合法 ICO 文件，并包含 `16/24/32/48/64/128/256` 多尺寸图标。

2. 检查发布后的 exe 是否内嵌应用图标。

   用 `System.Drawing.Icon.ExtractAssociatedIcon()` 从发布目录的 `DynamicIslandBar.exe` 提取关联图标。

   结果：修复前 exe 仍然可能显示默认图标；添加 `ApplicationIcon` 后，exe 已经可以提取出正确图标。

3. 检查安装后桌面快捷方式的真实目标。

   使用 `WScript.Shell` 读取桌面 `DynamicIslandBar.lnk`：

   ```powershell
   $desktop = [Environment]::GetFolderPath('Desktop')
   $shell = New-Object -ComObject WScript.Shell
   $shortcut = $shell.CreateShortcut((Join-Path $desktop 'DynamicIslandBar.lnk'))
   $shortcut.TargetPath
   $shortcut.IconLocation
   ```

   结果发现桌面快捷方式不是直接指向安装目录下的 `DynamicIslandBar.exe`，而是指向：

   ```text
   C:\Users\Tyl\AppData\Roaming\Microsoft\Installer\{...}\_xxxx.exe
   ```

   这说明 Visual Studio Installer Projects 创建的是 Windows Installer 广告快捷方式。该快捷方式会使用 MSI 缓存里的图标资源，而不是直接读取安装目录中的 exe 图标。

4. 提取 Installer 缓存 exe 的图标。

   从 `C:\Users\Tyl\AppData\Roaming\Microsoft\Installer\{...}\_xxxx.exe` 提取图标后，确认它仍然是默认白蓝图标。

   这说明问题根因不是普通 Windows 图标缓存，而是 MSI 广告快捷方式缓存了默认图标。

## 根因

主要根因有三个：

1. WPF 项目没有在 `.csproj` 中配置 `ApplicationIcon`，导致 `DynamicIslandBar.exe` 自身没有稳定内嵌应用图标。
2. Visual Studio Installer Projects 生成的是 Windows Installer 广告快捷方式，桌面快捷方式实际指向 Installer 缓存文件，不直接指向安装目录中的 exe。
3. 原始 `DynamicIslandBar-AppIcon.ico` 虽然 Windows 能读取，但对 MSI 广告快捷方式不够稳。Installer 对 ICO 兼容性更挑剔，使用传统未压缩图标帧更可靠。

## 修复内容

1. 生成 MSI 兼容的 Legacy 图标：

   `D:\UI-win\DynamicIslandBar\Assets\DynamicIslandBar-AppIcon-Legacy.ico`

2. 在 WPF 项目中内嵌应用图标：

   文件：[DynamicIslandBar.csproj](D:/UI-win/DynamicIslandBar/DynamicIslandBar.csproj)

   ```xml
   <ApplicationIcon>Assets\DynamicIslandBar-AppIcon-Legacy.ico</ApplicationIcon>
   ```

3. 修改 Setup Project 配置：

   文件：`D:\UI-win\Setup1\DynamicIslandBar.Setup\DynamicIslandBar.Setup\DynamicIslandBar.Setup.vdproj`

   修改点：

   - 图标文件改为 `DynamicIslandBar-AppIcon-Legacy.ico`
   - `ARPPRODUCTICON` 指向该图标文件
   - `ProductVersion` 从 `1.0.0` 升到 `1.0.1`
   - 更换 `ProductCode`
   - 更换 `PackageCode`

4. 重新发布程序：

   ```powershell
   dotnet publish D:\UI-win\DynamicIslandBar\DynamicIslandBar.csproj `
     -c Release `
     -r win-x64 `
     --self-contained true `
     -p:PublishSingleFile=true `
     -p:DebugType=none `
     -p:DebugSymbols=false `
     -o D:\UI-win\release\DynamicIslandBar-v1.0-beta1
   ```

5. 临时修复本机桌面快捷方式：

   删除 Installer 广告快捷方式后，重建一个普通快捷方式，直接指向：

   `D:\win-ui1.0\DynamicIslandBar.exe`

   并设置图标：

   `D:\UI-win\DynamicIslandBar\Assets\DynamicIslandBar-AppIcon-Legacy.ico`

## 下次遇到类似问题时的检查清单

1. 先确认 `.ico` 文件是否有效，并且包含多尺寸图标。
2. 确认 `.csproj` 是否配置了 `<ApplicationIcon>Assets\\xxx.ico</ApplicationIcon>`。
3. 重新 `dotnet publish` 后，从发布目录的 exe 提取关联图标，确认 exe 本身已经带图标。
4. 检查桌面 `.lnk`：
   - `TargetPath` 是否直接指向真实 exe
   - `IconLocation` 是否指向正确图标
   - 如果指向 `AppData\\Roaming\\Microsoft\\Installer\\{...}`，说明它是 MSI 广告快捷方式
5. 如果是 MSI 广告快捷方式：
   - 优先使用 Legacy ICO
   - 设置 `ARPPRODUCTICON`
   - 升级 `ProductVersion`
   - 更换 `ProductCode`
   - 更换 `PackageCode`
   - 卸载旧版本后重新安装
6. 如果配置都正确但桌面仍旧显示旧图标：
   - 删除旧快捷方式
   - 重启资源管理器或执行 `ie4uinit.exe -show`
   - 必要时重启电脑

## 经验总结

安装包图标问题不能只看 Visual Studio UI 里有没有选择图标。需要分三层验证：

1. `ico` 文件本身是否可靠。
2. `exe` 是否真正内嵌图标。
3. 安装后的快捷方式是否直接使用 exe 图标，还是使用 MSI 广告快捷方式缓存图标。

以后打包前，建议先验证发布目录里的 `DynamicIslandBar.exe` 图标，再生成 MSI。这样能提前发现大多数图标问题。
