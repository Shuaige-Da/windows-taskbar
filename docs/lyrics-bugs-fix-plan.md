# 歌词系统 Bug 修复计划

## 修复状态：全部完成

| # | Bug | 根因文件 | 状态 |
|---|-----|----------|------|
| 1 | 网易云部分歌曲不显示歌词 | `LyricsService.cs:SearchNetEaseAsync` | ✅ 已修复 |
| 2 | 歌词匹配错误（歌词不属于当前歌曲） | `LyricsService.cs:SearchNetEaseAsync` | ✅ 已修复 |
| 3 | 歌词切换闪屏 + 轨道重置 | `LyricsService.cs:EnsureLyricsAsync` | ✅ 已修复 |
| 4 | 歌词重复出现 | `MainWindow.xaml.cs:EnqueueCenterCardLyricsDanmaku` | ✅ 已修复 |
| 5 | 歌词和音频不同步 | `MediaService.cs:OnMediaPropertiesChanged` | ✅ 已修复 |
| 6 | 滚动速度不一致 | `CenterCardLyricsDanmakuPolicy.cs` | ✅ 已修复 |

---

## 修复 #1 + #2：网易云搜索关键词回退 + 结果评分验证

**文件**: `LyricsService.cs`

**改动**:
- `SearchNetEaseAsync` 增加 4 种关键词回退策略（与 LRCLIB 一致）
- `SearchNetEaseSingleAsync` 新增，单次搜索 + 结果评分
- `ScoreNetEaseResult` 新增，基于标题相似度 + 歌手相似度 + 时长差的评分算法（满分 240 分，阈值 80 分）
- `StringSimilarity` 新增，简单的字符串相似度计算
- 搜索结果数从 5 提升到 20
- 添加 User-Agent 和 Referer HTTP 头

---

## 修复 #3：歌词切换时序优化

**文件**: `LyricsService.cs`

**改动**:
- `EnsureLyricsAsync` 不再在请求前清空歌词，改用临时变量
- 网络请求成功后才替换旧歌词，失败则保留旧歌词
- `SearchNetEaseAsync` 返回类型改为 `(bool, TimeSpan, List<LyricLine>?)` 直接返回解析结果
- `SearchLrclibAsync` 返回类型改为 `(List<LyricLine>?, string[]?)` 直接返回结果
- 避免请求期间歌词为空导致 UI 闪烁

---

## 修复 #4：弹幕画布防重复

**文件**: `MainWindow.xaml.cs`

**改动**:
- `EnqueueCenterCardLyricsDanmaku` 在添加新弹幕前无条件 `Children.Clear()`
- 移除 `CenterCardLyricsLoopTimer_Tick` 中重置 `_lastDanmakuLyric = null` 的逻辑，让去重机制正常工作

---

## 修复 #5：SMTC 位置同步修复

**文件**: `MediaService.cs`

**改动**:
- `OnMediaPropertiesChanged` 增加 500ms 去抖，元数据更新（非真正切歌）不重启计时器
- `GetMediaInfoAsync` 中 SMTC 位置报告时不再尝试重启计时器（旧逻辑无效）
- 添加 `_pendingTitle`、`_pendingArtist`、`_pendingChangeTimestamp` 字段用于去抖

---

## 修复 #6：弹幕滚动速度平滑

**文件**: `CenterCardLyricsDanmakuPolicy.cs`

**改动**:
- `CalculateSynchronizedTrackDuration` 计算结果 clamp 到 [4, 10] 秒范围
- 避免短间隔行滚动过快、长间隔行滚动过慢
