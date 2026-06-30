# Bug 日志索引

最后更新：`2026-06-30`

## 用法

以后查 bug 不要直接把全部日志都读一遍。

推荐顺序：

1. 先看这份索引
2. 按关键词找到最像的历史问题
3. 再只打开对应那一篇小日志
4. 最后结合 [胶囊任务栏功能参数与实现档案](D:/UI-win/docs/capsule-feature-archive.md) 判断当前实现有没有变

这样最省 token，也最容易快速定位。

## 当前日志列表

| 日期 | 问题标题 | 关键词 | 主要关联模块 | 日志文件 |
| --- | --- | --- | --- | --- |
| 2026-06-29 | 胶囊粗细与中心卡片回归 | 粗细滑块、顶部模式、中心卡片裁切、暂停音乐播放器、实时刷新、槽位裁切、歌词不显示 | `CapsuleAppearanceMapper` `CenterCardLayoutPolicy` `CenterCardPresentationPolicy` `MainWindow` | [2026-06-29-center-card-thickness-regression.md](D:/UI-win/docs/bug-logs/2026-06-29-center-card-thickness-regression.md) |
| 2026-06-28 | 安装包桌面快捷方式图标不显示 | 安装包图标、快捷方式图标、MSI 广告快捷方式、ApplicationIcon、Legacy ICO | `DynamicIslandBar.csproj` `Setup.vdproj` 打包流程 | [2026-06-28-installer-shortcut-icon-not-showing.md](D:/UI-win/docs/bug-logs/2026-06-28-installer-shortcut-icon-not-showing.md) |

## 新增日志规则

以后每次修复 bug：

1. 在 `docs/bug-logs/` 下新增一个按日期命名的小日志文件
2. 在这份索引里补一行
3. 标出关键词、主要关联模块和文件路径

这样以后我可以先看索引，再精准读取单篇日志，不需要把全部历史都读进上下文。
