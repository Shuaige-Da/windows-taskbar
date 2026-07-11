using DynamicIslandBar;

namespace DynamicIslandBar.Tests;

public class MainWindowUiLogicTests
{
    [Fact]
    public void MainWindow_SideDockLyricsUseBottomToTopDanmakuMotion()
    {
        var code = ReadProjectFile("DynamicIslandBar", "MainWindow.xaml.cs");

        Assert.Contains("var usesVerticalLyricsFlow = _capsuleConfig.Mode is CapsuleMode.LeftDock or CapsuleMode.RightDock;", code);
        Assert.Contains("FormatVerticalLyricColumn(window.CurrentText)", code);
        Assert.Contains("TextWrapping = TextWrapping.NoWrap", code);
        Assert.Contains("CenterCardLyricsDanmakuCanvas.Children.Clear();", code);
        Assert.Contains("CenterCardLyricsDanmakuPolicy.BuildLineMotionPlan(", code);
        Assert.Contains("Canvas.SetTop(textBlock, primaryOffset);", code);
        Assert.Contains("usesVerticalLyricsFlow ? Canvas.TopProperty : Canvas.LeftProperty", code);
    }

    [Fact]
    public void MainWindow_HorizontalLyricsShowCurrentLineThenPreviewNextLine()
    {
        var code = ReadProjectFile("DynamicIslandBar", "MainWindow.xaml.cs");

        Assert.Contains("_lyricsService.GetPlaybackWindow(position)", code);
        Assert.Contains("BeginCenterCardLyricWindowAnimation(window, forceRestart);", code);
        Assert.Contains("window.CurrentText", code);
        Assert.Contains("window.NextText", code);
        Assert.Contains("BeginTime = plan.NextRevealDelay", code);
        Assert.DoesNotContain("GetCurrentLyricSequence(position, maxLines: 6)", code);
        Assert.DoesNotContain("BuildContinuousTrack", code);
    }

    [Fact]
    public void MainWindow_ShowsSongInformationBeforeFirstLyricWithoutClearingTheHandoff()
    {
        var code = ReadProjectFile("DynamicIslandBar", "MainWindow.xaml.cs");

        Assert.Contains("_lyricsService.GetSongIntroductionWindow(", code);
        Assert.Contains("previous.NextText, window.NextText", code);
        Assert.Contains("FindCenterCardLyricBlock(window.Index)", code);
        Assert.Contains("ApplyCenterCardLyricAtPosition(position, forceRestart: false);", code);
    }

    [Fact]
    public void MainWindow_SideDockNormalLyricsUseDanmakuCanvasInsteadOfPinnedText()
    {
        var code = ReadProjectFile("DynamicIslandBar", "MainWindow.xaml.cs");

        Assert.Contains("CenterCardLyricMarqueeText.Visibility = Visibility.Collapsed;", code);
        Assert.Contains("CenterCardLyricsDanmakuCanvas.Visibility = state.ShowLyricsMarquee ? Visibility.Visible : Visibility.Collapsed;", code);
        Assert.Contains("BeginCenterCardLyricWindowAnimation(", code);
        Assert.DoesNotContain("CenterCardLyricsDanmakuCanvas.Visibility = Visibility.Collapsed;", code);
    }

    [Fact]
    public void MainWindow_MediaRefreshReappliesLyricsAfterLyricsFetchCompletes()
    {
        var code = ReadProjectFile("DynamicIslandBar", "MainWindow.xaml.cs");

        Assert.Contains("var lyricsAvailable = await _lyricsService.EnsureLyricsAsync(", code);
        Assert.Contains("ApplyCenterCardLyricAtPosition(", code);
        Assert.Contains("forceRestart: playbackStateChanged", code);
        Assert.Contains("_centerCardLiveMediaSnapshot = snapshot with { Lyric = window.CurrentText };", code);
        Assert.Contains("UpdateActiveAppSummary(app, GetPrimarySummaryStatus(app));", code);
    }

    [Fact]
    public void MainWindow_LyricTimerRestartsAnimationWhenTimedLineChanges()
    {
        var code = ReadProjectFile("DynamicIslandBar", "MainWindow.xaml.cs");

        Assert.Contains("private bool ApplyCenterCardLyricWindow(", code);
        Assert.Contains("previous.Index == window.Index", code);
        Assert.Contains("forceRestart", code);
        Assert.Contains("|| !isSameWindow", code);
        Assert.Contains("BeginCenterCardLyricWindowAnimation(window, forceRestart);", code);
        Assert.Contains("FindCenterCardLyricBlock(window.Index)", code);
        Assert.Contains("GetLyricBlockPrimaryOffset(currentBlock, usesVerticalLyricsFlow)", code);
        Assert.Contains("CenterCardLyricsDanmakuCanvas.Children.Remove(currentBlock);", code);
    }

    [Fact]
    public void MainWindow_AppIconHoverDoesNotReplaceActiveMusicLyricsSummary()
    {
        var code = ReadProjectFile("DynamicIslandBar", "MainWindow.xaml.cs");

        Assert.Contains("ResolveCenterCardSummaryAppDuringIconHover()", code);
        Assert.Contains("ShouldKeepCenterCardMusicLyricsWhileAppIconHovered()", code);
        Assert.DoesNotContain("var summaryApp = _hoveredApp ?? GetPrimarySummaryApp();", code);
    }

    [Fact]
    public void MainWindow_LyricsFastTimerUpdatesCurrentSongWithoutSongChangeEventGate()
    {
        var code = ReadProjectFile("DynamicIslandBar", "MainWindow.xaml.cs");

        Assert.Contains("if (_mediaService == null || !_isMusicPlaying)", code);
        Assert.DoesNotContain("|| !_mediaService.HasSeenSongChange", code);
    }

    [Fact]
    public void MainWindow_SeekForcesLyricWindowResyncAtTargetPosition()
    {
        var code = ReadProjectFile("DynamicIslandBar", "MainWindow.xaml.cs");

        Assert.Contains("ApplyCenterCardLyricAtPosition(TimeSpan.FromMilliseconds(targetMs), forceRestart: true);", code);
    }

    [Fact]
    public void MainWindow_MediaIdentityChangeClearsOldTrackWithoutDiscardingServiceCache()
    {
        var code = ReadProjectFile("DynamicIslandBar", "MainWindow.xaml.cs");

        Assert.Contains("if (HasCenterCardMediaIdentityChanged(previousSnapshot, snapshot))", code);
        Assert.Contains("ResetCenterCardLyricScrollState(clearActiveTrack: true);", code);
        Assert.DoesNotContain("_lyricsService.Clear();", code);
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
    public void MainWindow_LyricsPipelineUsesTimedWindowsInsteadOfLoopTimer()
    {
        var code = ReadProjectFile("DynamicIslandBar", "MainWindow.xaml.cs");

        Assert.DoesNotContain("private readonly DispatcherTimer _centerCardLyricsLoopTimer;", code);
        Assert.DoesNotContain("CenterCardLyricsLoopTimer_Tick", code);
        Assert.Contains("if (position < TimeSpan.Zero)", code);
        Assert.Contains("_lyricsService.GetPlaybackWindow(position)", code);
        Assert.Contains("BeginCenterCardLyricWindowAnimation(", code);
        Assert.DoesNotContain("ScheduleCenterCardLyricsContinuation", code);
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
        Assert.Contains("&& _mediaDuration.TotalSeconds > 0", code);
        Assert.Contains("CenterCardSideProgressPanel.Visibility = _mediaDuration.TotalSeconds > 0", code);
        Assert.DoesNotContain("&& _isMusicPlaying\r\n                && _mediaDuration.TotalSeconds > 0", code);
        Assert.Contains("CenterCardSideProgressBar.Value = safePercent;", code);
        Assert.Contains("ReferenceEquals(sender, CenterCardSideProgressBar)", code);
        Assert.Contains("? Math.Max(progressBar.ActualHeight, 1)", code);
        Assert.Contains("? trackLength - e.GetPosition(progressBar).Y", code);
    }

    [Fact]
    public void MainWindow_PlaybackModeButtonUsesMediaServiceResult()
    {
        var code = ReadProjectFile("DynamicIslandBar", "MainWindow.xaml.cs");
        var mediaService = ReadProjectFile("DynamicIslandBar", "MediaService.cs");

        Assert.Contains("var requestedMode = (_playbackModeIndex + 1) % 4;", code);
        Assert.Contains("var changed = await _mediaService.SetPlaybackModeAsync(requestedMode);", code);
        Assert.Contains("? requestedMode", code);
        Assert.Contains("case IAsyncOperation<bool> asyncOperation:", mediaService);
        Assert.Contains("return await asyncOperation.AsTask();", mediaService);
    }

    [Fact]
    public void MediaService_PlaybackModeMapsListAndTrackRepeatCorrectly()
    {
        var mediaService = ReadProjectFile("DynamicIslandBar", "MediaService.cs");

        Assert.Contains("return repeatInt switch { 2 => 1, 1 => 2, _ => 0 };", mediaService);
        Assert.Contains("case 1: // Loop All", mediaService);
        Assert.Contains("repeatMethod.Invoke(session, CreateRepeatArgs(2))", mediaService);
        Assert.Contains("case 2: // Loop One", mediaService);
        Assert.Contains("repeatMethod.Invoke(session, CreateRepeatArgs(1))", mediaService);
    }

    [Fact]
    public void MediaService_UsesDebouncedIdentityAndStablePositionAnchors()
    {
        var mediaService = ReadProjectFile("DynamicIslandBar", "MediaService.cs");

        Assert.Contains("IdentityDebounceMilliseconds = 500", mediaService);
        Assert.Contains("ObserveMediaIdentity(", mediaService);
        Assert.Contains("SetSmtcAnchorLocked(position, isPlaying);", mediaService);
        Assert.Contains("_lastSmtcPosition + _smtcTimer.Elapsed", mediaService);
        Assert.DoesNotContain("// SMTC is reporting position - sync timer", mediaService);
    }

    [Fact]
    public void MainWindow_HighFrequencyRefreshesAreThrottledAndStoppedOnClose()
    {
        var code = ReadProjectFile("DynamicIslandBar", "MainWindow.xaml.cs");

        Assert.Contains("if (_isCenterCardMediaRefreshInProgress)", code);
        Assert.Contains("_isCenterCardMediaRefreshInProgress = false;", code);
        Assert.Contains("Interlocked.Exchange(ref _isRunningAppsRefreshInProgress, 1)", code);
        Assert.Contains("private void StopAllTimers()", code);
        Assert.Contains("_lyricsRefreshCancellation?.Cancel();", code);
        Assert.Contains("_lyricsFastTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };", code);
        Assert.DoesNotContain("_lyricsFastTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };", code);
    }

    [Fact]
    public void MainWindow_LyricsNetworkFetchDoesNotBlockMediaRefreshLoop()
    {
        var code = ReadProjectFile("DynamicIslandBar", "MainWindow.xaml.cs");
        var refreshStart = code.IndexOf("private async Task RefreshCenterCardMediaSnapshotAsync()", StringComparison.Ordinal);
        var queueStart = code.IndexOf("private void QueueLyricsRefresh(", refreshStart, StringComparison.Ordinal);
        Assert.True(refreshStart >= 0 && queueStart > refreshStart);

        var refreshMethod = code[refreshStart..queueStart];
        Assert.Contains("QueueLyricsRefresh(", refreshMethod);
        Assert.DoesNotContain("await _lyricsService.EnsureLyricsAsync(", refreshMethod);
        Assert.Contains("_lyricsRefreshRetryAfterUtc = DateTime.UtcNow.AddMinutes(1);", code);
    }

    [Fact]
    public void MediaService_DisposeUnsubscribesWinRtSessionEvents()
    {
        var mediaService = ReadProjectFile("DynamicIslandBar", "MediaService.cs");

        Assert.Contains("public class MediaService : IDisposable", mediaService);
        Assert.Contains("_subscribedSession.TimelinePropertiesChanged -= OnTimelineChanged;", mediaService);
        Assert.Contains("_subscribedSession.PlaybackInfoChanged -= OnPlaybackInfoChanged;", mediaService);
        Assert.Contains("_subscribedSession.MediaPropertiesChanged -= OnMediaPropertiesChanged;", mediaService);
        Assert.Contains("GC.SuppressFinalize(this);", mediaService);
    }

    [Fact]
    public void MainWindow_CenterCardVolumeControlsMusicAppOnly()
    {
        var code = ReadProjectFile("DynamicIslandBar", "MainWindow.xaml.cs");

        Assert.Contains("ResolveCenterCardVolumeProcessId()", code);
        Assert.Contains("OpenCenterCardVolumePopup(sender as UIElement ?? CenterCardVolumeButton);", code);
        Assert.Contains("CenterCardVolumeButton_MouseEnter", code);
        Assert.Contains("CenterCardVolumeSlider.IsEnabled = vol >= 0;", code);
        Assert.Contains("AudioService.SetAppVolume(_volumeControlAppPid, pct);", code);
        Assert.DoesNotContain("AudioService.SetVolume(pct); // Fallback", code);
        Assert.DoesNotContain("vol = AudioService.GetVolume(); // Fallback to system volume", code);
    }

    [Fact]
    public void MainWindow_SideDockVolumePopupKeepsDetailsOpenAndUsesVerticalSlider()
    {
        var code = ReadProjectFile("DynamicIslandBar", "MainWindow.xaml.cs");
        var xaml = ReadProjectFile("DynamicIslandBar", "MainWindow.xaml");

        Assert.Contains("x:Name=\"CenterCardSideVolumePanel\"", xaml);
        Assert.Contains("x:Name=\"CenterCardSideVolumeSlider\"", xaml);
        Assert.Contains("Style=\"{StaticResource SideDockVolumeSliderStyle}\"", xaml);
        Assert.Contains("Fill=\"#46E0FF\"", xaml);
        Assert.Contains("ValueChanged=\"CenterCardSideVolumeSlider_ValueChanged\"", xaml);
        Assert.Contains("Orientation=\"Vertical\"", xaml);
        Assert.Contains("ToggleCenterCardSideVolumeSlider();", code);
        Assert.Contains("_isCenterCardSideVolumeSliderPinned", code);
        Assert.Contains("CenterCardSideVolumePanel.Visibility = _isCenterCardSideVolumeSliderPinned", code);
        Assert.Contains("CenterCardSideVolumeSlider.Value = vol >= 0 ? vol : 0;", code);
        Assert.Contains("CenterCardSideVolumeSlider_ValueChanged", code);
        Assert.Contains("_isCenterCardSideVolumeSliderPinned = false;", code);
        Assert.Contains("ScheduleCenterCardHoverExit();", code);
    }

    [Fact]
    public void MainWindow_SideDockHoverKeepsCapsuleInNormalLyricsMode()
    {
        var code = ReadProjectFile("DynamicIslandBar", "MainWindow.xaml.cs");

        Assert.Contains("var capsuleIsHovered = IsSideDockMode ? false : _isCenterCardHovered;", code);
        Assert.Contains("var sideDetailsState = CenterCardPresentationPolicy.Build(", code);
        Assert.Contains("_presentationController.SetRuntimeVisibility(CapsuleVisualPart.Details, !state.ShowLyricsMarquee);", code);
        Assert.Contains("_presentationController.SetRuntimeVisibility(CapsuleVisualPart.Lyrics, state.ShowLyricsMarquee);", code);
        Assert.Contains("CenterCardLyricsDock.VerticalAlignment = VerticalAlignment.Stretch;", code);
        Assert.Contains("CenterCardLyricsViewport.VerticalAlignment = VerticalAlignment.Stretch;", code);
        Assert.Contains("CenterCardLeftWave.Visibility = Visibility.Collapsed;", code);
        Assert.Contains("CenterCardRightWave.Visibility = Visibility.Collapsed;", code);
    }

    [Fact]
    public void MainWindow_CenterCardResizeHandlesKeepHitTargetsWithoutVisibleChrome()
    {
        var code = ReadProjectFile("DynamicIslandBar", "MainWindow.xaml.cs");

        Assert.Contains("private void HideCenterCardResizeHandleChrome()", code);
        Assert.Contains("CenterCardLeftResizeHandle.Opacity = 0;", code);
        Assert.Contains("CenterCardRightResizeHandle.Opacity = 0;", code);
        Assert.DoesNotContain("var targetHandleOpacity = _isCenterCardHovered ? 0.42 : 0;", code);
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
