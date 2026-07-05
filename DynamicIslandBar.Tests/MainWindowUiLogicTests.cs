using DynamicIslandBar;

namespace DynamicIslandBar.Tests;

public class MainWindowUiLogicTests
{
    [Fact]
    public void MainWindow_SideDockLyricsUseBottomToTopDanmakuMotion()
    {
        var code = ReadProjectFile("DynamicIslandBar", "MainWindow.xaml.cs");

        Assert.Contains("var usesVerticalLyricsFlow = _capsuleConfig.Mode is CapsuleMode.LeftDock or CapsuleMode.RightDock;", code);
        Assert.Contains("laneCount: usesVerticalLyricsFlow ? 1 : 3", code);
        Assert.Contains("Text = usesVerticalLyricsFlow ? FormatVerticalLyricColumn(lyric) : lyric", code);
        Assert.Contains("textBlock.TextWrapping = TextWrapping.NoWrap;", code);
        Assert.Contains("CenterCardLyricsDanmakuCanvas.Children.Clear();", code);
        Assert.Contains("Canvas.SetTop(textBlock, verticalStartTop);", code);
        Assert.Contains("textBlock.BeginAnimation(Canvas.TopProperty, animation);", code);
        Assert.Contains("? viewportHeight + 2", code);
        Assert.Contains("To = usesVerticalLyricsFlow ? -(textHeight + 28) : -(textWidth + 36)", code);
        Assert.Contains("ScheduleSideDockLyricsContinuation();", code);
    }

    [Fact]
    public void MainWindow_SideDockNormalLyricsUseDanmakuCanvasInsteadOfPinnedText()
    {
        var code = ReadProjectFile("DynamicIslandBar", "MainWindow.xaml.cs");

        Assert.Contains("CenterCardLyricMarqueeText.Visibility = Visibility.Collapsed;", code);
        Assert.Contains("CenterCardLyricsDanmakuCanvas.Visibility = state.ShowLyricsMarquee ? Visibility.Visible : Visibility.Collapsed;", code);
        Assert.Contains("UpdateCenterCardLyricsDanmaku(CenterCardLyricMarqueeText.Text);", code);
        Assert.DoesNotContain("CenterCardLyricsDanmakuCanvas.Visibility = Visibility.Collapsed;", code);
    }

    [Fact]
    public void MainWindow_MediaRefreshReappliesLyricsAfterLyricsFetchCompletes()
    {
        var code = ReadProjectFile("DynamicIslandBar", "MainWindow.xaml.cs");

        Assert.Contains("var refreshedLyric = _lyricsService.GetCurrentLyric(info.Position);", code);
        Assert.Contains("_centerCardLiveMediaSnapshot = snapshot with { Lyric = refreshedLyric };", code);
        Assert.Contains("UpdateActiveAppSummary(app, GetPrimarySummaryStatus(app));", code);
    }

    [Fact]
    public void MainWindow_MediaRefreshCreatesLiveSnapshotFromMediaServiceWhenStrictSnapshotIsMissing()
    {
        var code = ReadProjectFile("DynamicIslandBar", "MainWindow.xaml.cs");

        Assert.Contains("if (snapshot == null && CenterCardMediaSnapshotProvider.IsLikelyMusicApp(app)", code);
        Assert.Contains("snapshot = new CenterCardMediaSnapshot(", code);
        Assert.Contains("Title: info.Title ?? app.DisplayName,", code);
        Assert.Contains("Artist: info.Artist ?? string.Empty,", code);
        Assert.Contains("IsPlaying: info.IsPlaying,", code);
        Assert.Contains("_centerCardLiveMediaSnapshot = snapshot;", code);
        Assert.Contains("UpdateActiveAppSummary(app, GetPrimarySummaryStatus(app));", code);
    }

    [Fact]
    public void MainWindow_SideDockResizeUsesVerticalDeltaForCenterCardExtent()
    {
        var code = ReadProjectFile("DynamicIslandBar", "MainWindow.xaml.cs");

        Assert.Contains("var delta = IsSideDockMode", code);
        Assert.Contains("string.Equals(side, \"Top\", StringComparison.OrdinalIgnoreCase)", code);
        Assert.Contains("e.VerticalChange * 2", code);
        Assert.Contains("SetCenterCardWidthPercent", code);
    }

    [Fact]
    public void MainWindow_LyricsMarqueeLoopAvoidsPerRefreshResetAndRequeuesSameLine()
    {
        var code = ReadProjectFile("DynamicIslandBar", "MainWindow.xaml.cs");

        Assert.Contains("private readonly DispatcherTimer _centerCardLyricsLoopTimer;", code);
        Assert.Contains("_centerCardLyricsLoopTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2200) };", code);
        Assert.Contains("_centerCardLyricsLoopTimer.Tick += CenterCardLyricsLoopTimer_Tick;", code);
        Assert.Contains("if (!_isCenterCardLyricsMarqueeActive || !string.Equals(_activeCenterCardLyricText, state.PrimaryText, StringComparison.Ordinal))", code);
        Assert.Contains("if (IsSideDockMode && CenterCardLyricsDanmakuCanvas.Children.Count > 0)", code);
        Assert.Contains("await Task.Delay(160);", code);
        Assert.Contains("_lastDanmakuLyric = null;", code);
        Assert.Contains("UpdateCenterCardLyricsDanmaku(CenterCardLyricMarqueeText.Text);", code);
    }

    [Fact]
    public void MainWindow_SideDockProgressUsesExternalDetailsPanelInsteadOfCrampedCapsuleProgress()
    {
        var code = ReadProjectFile("DynamicIslandBar", "MainWindow.xaml.cs");
        var xaml = ReadProjectFile("DynamicIslandBar", "MainWindow.xaml");

        Assert.Contains("x:Name=\"CenterCardSideDetailsPanel\"", xaml);
        Assert.Contains("x:Name=\"CenterCardSideProgressBar\"", xaml);
        Assert.Contains("UpdateCenterCardSideDetailsOverlay(app, sideDetailsState);", code);
        Assert.Contains("var showProgress = !IsSideDockMode", code);
        Assert.Contains("CenterCardSideProgressBar.Value = Math.Clamp(percent, 0, 100);", code);
        Assert.Contains("ReferenceEquals(sender, CenterCardSideProgressBar)", code);
        Assert.Contains("? Math.Max(progressBar.ActualHeight, 1)", code);
        Assert.Contains("? trackLength - e.GetPosition(progressBar).Y", code);
    }

    [Fact]
    public void MainWindow_SideDockHoverKeepsCapsuleInNormalLyricsMode()
    {
        var code = ReadProjectFile("DynamicIslandBar", "MainWindow.xaml.cs");

        Assert.Contains("var capsuleIsHovered = IsSideDockMode ? false : _isCenterCardHovered;", code);
        Assert.Contains("var sideDetailsState = CenterCardPresentationPolicy.Build(", code);
        Assert.Contains("CenterCardDetailsLayer.Visibility = IsSideDockMode", code);
        Assert.Contains("CenterCardLyricsLayer.Visibility = state.ShowLyricsMarquee ? Visibility.Visible : Visibility.Collapsed;", code);
        Assert.Contains("CenterCardLyricsDock.VerticalAlignment = VerticalAlignment.Stretch;", code);
        Assert.Contains("CenterCardLyricsViewport.VerticalAlignment = VerticalAlignment.Stretch;", code);
        Assert.Contains("CenterCardLeftWave.Visibility = Visibility.Collapsed;", code);
        Assert.Contains("CenterCardRightWave.Visibility = Visibility.Collapsed;", code);
    }

    [Fact]
    public void MainWindow_ResizeHandlesOnlyFadeInWhileCenterCardHovered()
    {
        var code = ReadProjectFile("DynamicIslandBar", "MainWindow.xaml.cs");

        Assert.Contains("var targetHandleOpacity = _isCenterCardHovered ? 0.42 : 0;", code);
        Assert.Contains("CenterCardLeftResizeHandle.Opacity = 0;", code);
        Assert.Contains("CenterCardRightResizeHandle.Opacity = 0;", code);
    }

    [Fact]
    public void BuildAppsContextMenuState_ShowsOpenForStoppedFavoriteWithKnownPath()
    {
        var entry = new RunningAppEntry(
            "wechat",
            "WeChat",
            @"C:\Apps\WeChat.exe",
            false,
            true,
            false,
            0);

        var menuState = AppsMenuStateBuilder.Build(entry);

        Assert.True(menuState.CanOpenApp);
        Assert.False(menuState.CanCloseApp);
        Assert.True(menuState.CanToggleFavorite);
    }

    [Fact]
    public void GetOverlayFrame_PlacesOverlayBesideIconWithoutChangingIconSlot()
    {
        var frame = AppHoverOverlayLayoutPolicy.GetOverlayFrame(
            PopupFlowDirection.Right,
            iconLeft: 120,
            iconTop: 180,
            iconWidth: 40,
            iconHeight: 40,
            overlayWidth: 172,
            overlayHeight: 40,
            layerWidth: 540,
            layerHeight: 400);

        Assert.Equal(166, frame.Left);
        Assert.Equal(180, frame.Top);
    }

    [Fact]
    public void GetOverlayFrame_ClampsOverlayInsideLayerNearRightEdge()
    {
        var frame = AppHoverOverlayLayoutPolicy.GetOverlayFrame(
            PopupFlowDirection.Right,
            iconLeft: 470,
            iconTop: 180,
            iconWidth: 40,
            iconHeight: 40,
            overlayWidth: 172,
            overlayHeight: 40,
            layerWidth: 540,
            layerHeight: 400);

        Assert.Equal(360, frame.Left);
        Assert.Equal(180, frame.Top);
    }

    [Fact]
    public void GetOverlayFrame_PlacesRightDockOverlayOnCapsuleOuterLeftSide()
    {
        var frame = AppHoverOverlayLayoutPolicy.GetOverlayFrame(
            PopupFlowDirection.Left,
            iconLeft: 470,
            iconTop: 180,
            iconWidth: 40,
            iconHeight: 40,
            overlayWidth: 84,
            overlayHeight: 116,
            layerWidth: 540,
            layerHeight: 400);

        Assert.Equal(380, frame.Left);
        Assert.Equal(142, frame.Top);
    }

    private static string ReadProjectFile(params string[] pathParts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "DynamicIslandBar", "MainWindow.xaml")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return File.ReadAllText(Path.Combine(new[] { directory!.FullName }.Concat(pathParts).ToArray()));
    }
}
