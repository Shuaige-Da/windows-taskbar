namespace DynamicIslandBar.Tests;

public class VisualLayerContractTests
{
    [Fact]
    public void MainWindow_DoesNotUseIndependentCapsuleGlowPath()
    {
        var xaml = ReadMainWindowXaml();

        Assert.DoesNotContain("CapsuleAmbientGlowPath", xaml);
        Assert.DoesNotContain("x:Name=\"GlowPath\"", xaml);
    }

    [Fact]
    public void MainWindow_UsesSingleOuterCapsuleGlowLayer()
    {
        var xaml = ReadMainWindowXaml();

        Assert.Contains("x:Name=\"CapsuleBorder\"", xaml);
        Assert.Contains("Padding=\"0\"", xaml);
        Assert.Contains("BorderThickness=\"1.4\"", xaml);
        Assert.Contains("<Grid Margin=\"14,0\">", xaml);
        Assert.Contains("<ColumnDefinition Width=\"*\"/>", xaml);
        Assert.Contains("Grid.Column=\"2\"", xaml);
        Assert.DoesNotContain("Background=\"{StaticResource CapsuleInnerDepthBrush}\"", xaml);
        Assert.DoesNotContain("x:Name=\"CapsuleAmbientGlowBorder\"", xaml);
        Assert.DoesNotContain("x:Name=\"CapsuleGlowBorder\"", xaml);
        Assert.DoesNotContain("CapsuleGlassRimBrush", xaml);
    }

    [Fact]
    public void MainWindow_DoesNotUseMainCapsuleDropShadowOrThickCardStroke()
    {
        var xaml = ReadMainWindowXaml();

        Assert.Contains("BorderThickness=\"1.2\"", xaml);
        Assert.DoesNotContain("DropShadowEffect BlurRadius=\"20\" ShadowDepth=\"4\" Opacity=\"0.34\"", xaml);
        Assert.DoesNotContain("DropShadowEffect BlurRadius=\"34\" ShadowDepth=\"9\" Opacity=\"0.5\"", xaml);
    }

    [Fact]
    public void MainWindow_CodeBehindDoesNotReplaceOuterGlowWithThemeBorder()
    {
        var code = ReadProjectFile("DynamicIslandBar", "MainWindow.xaml.cs");

        Assert.DoesNotContain("CapsuleBorder.BorderBrush = CreateBrush(_currentTheme.BorderBrush)", code);
        Assert.DoesNotContain("CapsuleBorder.Background = CreateBrush(_currentTheme.CapsuleBackground)", code);
        Assert.Contains("CapsuleBorder.Background = CapsuleAppearanceMapper.BuildBackgroundBrush(_capsuleConfig.GlassOpacityPercent)", code);
        Assert.Contains("CapsuleBorder.Effect = CapsuleAppearanceMapper.BuildShadowEffect(_capsuleConfig.Mode, _capsuleConfig.ShadowPercent)", code);
        Assert.Contains("UpdateCapsuleGlowBrush(null)", code);
        Assert.Contains("CapsuleBorder.BorderThickness = new Thickness(CapsuleAppearanceMapper.MapGlowThickness(_capsuleConfig.GlowThicknessPercent))", code);
    }

    [Fact]
    public void MainWindow_PopupPanelsUseCapsuleGlassAppearance()
    {
        var code = ReadProjectFile("DynamicIslandBar", "MainWindow.xaml.cs");
        var xaml = ReadMainWindowXaml();

        Assert.Contains("ApplyGlassPanelTheme(WifiPanel)", code);
        Assert.Contains("CapsuleAppearanceMapper.BuildPanelBackgroundBrush(_capsuleConfig.GlassOpacityPercent)", code);
        Assert.Contains("CapsuleAppearanceMapper.BuildPanelBorderBrush(_capsuleConfig.GlowIntensityPercent)", code);
        Assert.DoesNotContain("WifiPanel.Background = CreateBrush(_currentTheme.PanelBackground)", code);
        Assert.Contains("BorderBrush=\"#6046E0FF\"", xaml);
    }

    [Fact]
    public void MainWindow_AllFloatingSurfacesFollowCapsuleGlassAppearance()
    {
        var code = ReadProjectFile("DynamicIslandBar", "MainWindow.xaml.cs");

        var floatingSurfaces = new[]
        {
            "WifiPanel",
            "VolumePanel",
            "AppsPanel",
            "OverflowAppsPanel",
            "PermissionPromptPanel",
            "AppHoverOverlayBackground",
            "CenterCardSideDetailsChrome"
        };

        foreach (var surface in floatingSurfaces)
        {
            Assert.Contains($"ApplyGlassPanelTheme({surface})", code);
        }
    }

    [Fact]
    public void MainWindow_ContextMenuGroupsStyleAppearanceAndGlowUnderSettings()
    {
        var code = ReadProjectFile("DynamicIslandBar", "MainWindow.xaml.cs");

        Assert.Contains("new MenuItem { Header = \"设置\"", code);
        Assert.Contains("Header = \"风格\"", code);
        Assert.Contains("Header = \"外观\"", code);
        Assert.Contains("\"流光\"", code);
        Assert.Contains("\"亮度\"", code);
        Assert.Contains("\"胶囊粗细\"", code);
        Assert.Contains("\"胶囊长度\"", code);
        Assert.Contains("SetCapsuleLengthPercent", code);
        Assert.Contains("refreshLayout: true", code);
        Assert.Contains("SetGlowThicknessPercent", code);
        Assert.Contains("SetGlowSpeedPercent", code);
        Assert.DoesNotContain("menu.Items.Add(themeMenu);", code);
        Assert.DoesNotContain("menu.Items.Add(appearanceMenu);", code);
    }

    [Fact]
    public void MainWindow_ContextMenuUsesCapsuleThemeStyles()
    {
        var xaml = ReadMainWindowXaml();
        var code = ReadProjectFile("DynamicIslandBar", "MainWindow.xaml.cs");

        Assert.Contains("CapsuleContextMenuStyle", xaml);
        Assert.Contains("CapsuleMenuItemStyle", xaml);
        Assert.Contains("StyleCapsuleContextMenu(menu)", code);
        Assert.Contains("StyleCapsuleMenuItem", code);
    }

    [Fact]
    public void MainWindow_AppHoverAppliesIconAccentToMarqueeGlow()
    {
        var code = ReadProjectFile("DynamicIslandBar", "MainWindow.xaml.cs");

        Assert.Contains("private void ApplyGlowAccent(RunningAppEntry app)", code);
        Assert.Contains("UpdateCapsuleGlowBrush(accent)", code);
        Assert.DoesNotContain("BuildCapsuleAccentBrush", code);
    }

    [Fact]
    public void MainWindow_SummaryGlowUsesActualBorderInsteadOfInnerRectangle()
    {
        var xaml = ReadMainWindowXaml();

        Assert.DoesNotContain("x:Name=\"ActiveAppSummaryGlow\"", xaml);
        Assert.Contains("x:Name=\"ActiveAppSummaryPanel\"", xaml);
    }

    [Fact]
    public void MainWindow_DefersFloatingAppPanelRenderingUntilPanelsAreOpen()
    {
        var code = ReadProjectFile("DynamicIslandBar", "MainWindow.xaml.cs");

        Assert.Contains("if (AppsPopup.IsOpen)", code);
        Assert.Contains("if (OverflowAppsPopup.IsOpen)", code);
        Assert.DoesNotContain(
            """
            RenderMainBarApps();
            RenderAppsManagementPanel();
            RenderOverflowAppsPanel();
            """,
            code);
    }

    [Fact]
    public void MainWindow_ThrottlesRunningAppsRefreshWhenIdle()
    {
        var code = ReadProjectFile("DynamicIslandBar", "MainWindow.xaml.cs");

        Assert.Contains("RunningAppsRefreshPolicy.GetInterval(false)", code);
        Assert.Contains("UpdateRunningAppsRefreshInterval()", code);
        Assert.Contains("IsRunningAppsRefreshInteractive()", code);
    }

    [Fact]
    public void MainWindow_ApplyLayoutPreservesConfiguredCapsuleThickness()
    {
        var code = ReadProjectFile("DynamicIslandBar", "MainWindow.xaml.cs");

        Assert.Contains("var capsuleHeight = CapsuleAppearanceMapper.MapCapsuleHeight(", code);
        Assert.Contains("ApplyCapsuleSize(_currentLayoutMetrics.CapsuleWidth, capsuleHeight);", code);
        Assert.Contains("CapsuleBorder.Width = IsSideDockMode ? capsuleThickness : logicalCapsuleLength;", code);
        Assert.Contains("CapsuleBorder.Height = IsSideDockMode ? logicalCapsuleLength : capsuleThickness;", code);
        Assert.DoesNotContain("CapsuleBorder.Height = _currentLayoutMetrics.CapsuleHeight;", code);
    }

    [Fact]
    public void MainWindow_DoesNotDoubleScaleSystemParametersByDpi()
    {
        var code = ReadProjectFile("DynamicIslandBar", "MainWindow.xaml.cs");

        Assert.Contains("return DisplayBoundsProvider.GetPrimaryScreenSize();", code);
        Assert.DoesNotContain("width / dpi.DpiScaleX", code);
        Assert.DoesNotContain("height / dpi.DpiScaleY", code);
    }

    [Fact]
    public void MainWindow_CenterCardHasLyricsAndDetailsLayers()
    {
        var xaml = ReadMainWindowXaml();

        Assert.Contains("x:Name=\"CenterCardLyricsLayer\"", xaml);
        Assert.Contains("x:Name=\"CenterCardLyricsViewport\"", xaml);
        Assert.Contains("x:Name=\"CenterCardDetailsLayer\"", xaml);
        Assert.Contains("x:Name=\"CenterCardLyricMarqueeText\"", xaml);
        Assert.Contains("x:Name=\"CenterCardLeftWave\"", xaml);
        Assert.Contains("x:Name=\"CenterCardRightWave\"", xaml);
        Assert.Contains("x:Name=\"CenterCardTransportControls\"", xaml);
        Assert.Contains("x:Name=\"CenterCardPlayPauseButton\"", xaml);
        Assert.Contains("Click=\"CenterCardPlayPause_Click\"", xaml);
        Assert.Contains("Click=\"CenterCardPrevious_Click\"", xaml);
        Assert.Contains("Click=\"CenterCardNext_Click\"", xaml);
        Assert.Contains("Click=\"CenterCardVolume_Click\"", xaml);
        Assert.Contains("x:Name=\"CenterCardAppSelectorButton\"", xaml);
        Assert.Contains("x:Name=\"CenterCardSideAppSelectorButton\"", xaml);
        Assert.Contains("x:Name=\"CenterCardAppsPopup\"", xaml);
        Assert.Contains("x:Name=\"CenterCardAppsListPanel\"", xaml);
        Assert.Contains("x:Name=\"CenterCardLeftResizeHandle\"", xaml);
        Assert.Contains("x:Name=\"CenterCardRightResizeHandle\"", xaml);
        Assert.Contains("Grid.Column=\"0\"", xaml);
        Assert.Contains("Stroke=\"#D8FFFFFF\"", xaml);
        Assert.Contains("Background=\"Transparent\"", xaml);
        Assert.Contains("BorderThickness=\"0\"", xaml);
        Assert.Contains("Data=\"M0,0 L5,5 L10,0\"", xaml);
        Assert.DoesNotContain("CenterCardAppSelectorButton\"\r\n                                        Grid.Column=\"1\"", xaml);
        Assert.DoesNotContain("x:Name=\"CenterCardAppActions\"", xaml);
    }

    [Fact]
    public void MainWindow_CenterCardSupportsHoverAndWidthDrag()
    {
        var code = ReadProjectFile("DynamicIslandBar", "MainWindow.xaml.cs");

        Assert.Contains("UpdateCenterCardPresentation(", code);
        Assert.Contains("CenterCard_MouseEnter", code);
        Assert.Contains("CenterCardResizeHandle_DragDelta", code);
        Assert.Contains("CenterCardAppSelector_Click", code);
        Assert.Contains("CenterCardAppSelector_MouseEnter", code);
        Assert.Contains("CenterCardAppsPanel_MouseLeave", code);
        Assert.Contains("OpenCenterCardAppsPopup", code);
        Assert.Contains("CenterCardSideAppSelectorButton", code);
        Assert.Contains("RenderCenterCardAppsPanel", code);
        Assert.Contains("ClearCenterCardAppSelectorHighlight", code);
        Assert.DoesNotContain("OpenCenterCardAppSelector", code);
        Assert.DoesNotContain("SetSystemIconHighlight(CenterCardAppSelectorButton", code);
        Assert.DoesNotContain("CenterCardActions_Click", code);
        Assert.Contains("SetCenterCardWidthPercent", code);
        Assert.Contains("SetCenterCardApp", code);
        Assert.Contains("CenterCardPresentationPolicy.Build", code);
        Assert.Contains("WindowsMediaSessionSnapshotSource", code);
        Assert.Contains("_centerCardMediaRefreshTimer", code);
        Assert.Contains("CenterCardMediaSnapshotProvider.Resolve", code);
        Assert.Contains("CenterCardPlayPause_Click", code);
        Assert.Contains("FindVisualChildren<Rectangle>(CenterCardRightWave)", code);
        Assert.Contains("ApplyCenterCardLyricsLayout", code);
        Assert.Contains("CenterCardLayoutPolicy.GetLyricsLayout", code);
    }

    [Fact]
    public void MainWindow_DeclaresSnapPreviewLayer()
    {
        var xaml = ReadMainWindowXaml();

        Assert.Contains("x:Name=\"CapsuleSnapPreviewLayer\"", xaml);
        Assert.Contains("x:Name=\"CapsuleSnapPreviewOutline\"", xaml);
    }

    [Fact]
    public void MainWindow_CodeBehind_TracksFloatingAndPreviewState()
    {
        var code = ReadProjectFile("DynamicIslandBar", "MainWindow.xaml.cs");

        Assert.Contains("_activeSnapPreview", code);
        Assert.Contains("UpdateSnapPreview(", code);
        Assert.Contains("ApplySnapPreview(", code);
        Assert.Contains("ClearSnapPreview()", code);
        Assert.Contains("CaptureFloatingPosition()", code);
        Assert.Contains("CapsuleLayoutManager.BuildSnapPreview(", code);
        Assert.Contains("CapsuleSnapPreviewGeometry.ComputeOutlineOrigin(", code);
        Assert.Contains("CapsuleGrid.PointFromScreen(", code);
        Assert.Contains("if (_activeSnapPreview != null)", code);
        Assert.DoesNotContain("preview.Frame.Left - Left - 10", code);
        Assert.DoesNotContain("preview.Frame.Top - Top - 10", code);
    }

    [Fact]
    public void MainWindow_SideDockReflowsRealContentInsteadOfRotatingWholeCapsule()
    {
        var code = ReadProjectFile("DynamicIslandBar", "MainWindow.xaml.cs");

        Assert.Contains("var isSideDock = _capsuleConfig.Mode is CapsuleMode.LeftDock or CapsuleMode.RightDock;", code);
        Assert.Contains("DockItems.Orientation = isSideDock ? Orientation.Vertical : Orientation.Horizontal;", code);
        Assert.Contains("AppIconsHost.Orientation = isSideDock ? Orientation.Vertical : Orientation.Horizontal;", code);
        Assert.Contains("SystemIconsHost.Orientation = isSideDock ? Orientation.Vertical : Orientation.Horizontal;", code);
        Assert.Contains("CenterCardTransportControls.Orientation = isSideDock ? Orientation.Vertical : Orientation.Horizontal;", code);
        Assert.DoesNotContain("CapsuleBorder.LayoutTransform = _capsuleConfig.Mode is CapsuleMode.LeftDock or CapsuleMode.RightDock", code);
    }

    [Fact]
    public void MainWindow_SideDockLyricsUseVerticalMotionContract()
    {
        var code = ReadProjectFile("DynamicIslandBar", "MainWindow.xaml.cs");

        Assert.Contains("var usesVerticalLyricsFlow = _capsuleConfig.Mode is CapsuleMode.LeftDock or CapsuleMode.RightDock;", code);
        Assert.Contains("Text = usesVerticalLyricsFlow ? FormatVerticalLyricColumn(lyric) : lyric", code);
        Assert.Contains("textBlock.TextWrapping = TextWrapping.NoWrap;", code);
        Assert.Contains("CenterCardLyricsDanmakuCanvas.Children.Clear();", code);
        Assert.Contains("textBlock.BeginAnimation(Canvas.TopProperty, animation);", code);
        Assert.Contains("textBlock.BeginAnimation(Canvas.LeftProperty, animation);", code);
    }

    [Fact]
    public void MainWindow_SideDockCenterCardUsesVerticalContentFlow()
    {
        var code = ReadProjectFile("DynamicIslandBar", "MainWindow.xaml.cs");

        Assert.Contains("DockPanel.SetDock(CenterCardLyricsIcon, Dock.Top);", code);
        Assert.Contains("CenterCardLyricsDock.VerticalAlignment = VerticalAlignment.Stretch;", code);
        Assert.Contains("CenterCardLyricsIcon.VerticalAlignment = VerticalAlignment.Top;", code);
        Assert.Contains("CenterCardLyricsViewport.VerticalAlignment = VerticalAlignment.Stretch;", code);
        Assert.Contains("Grid.SetRow(ActiveAppSummaryIcon, 0);", code);
        Assert.Contains("Grid.SetRow(CenterCardDetailsTextStack, 1);", code);
        Assert.Contains("Grid.SetRow(CenterCardTransportControls, 2);", code);
    }

    [Fact]
    public void MainWindow_SideDockDetailsPopupUsesGlassCardAndVerticalProgress()
    {
        var xaml = ReadMainWindowXaml();

        Assert.Contains("x:Name=\"CenterCardSideDetailsPanel\"", xaml);
        Assert.Contains("Width=\"166\"", xaml);
        Assert.Contains("Background=\"{StaticResource HoverOverlayGlassBrush}\"", xaml);
        Assert.Contains("ApplyGlassPanelTheme(CenterCardSideDetailsChrome)", ReadProjectFile("DynamicIslandBar", "MainWindow.xaml.cs"));
        Assert.Contains("x:Name=\"CenterCardSideProgressBar\"", xaml);
        Assert.Contains("Grid.Row=\"1\"", xaml);
        Assert.Contains("Width=\"6\"", xaml);
        Assert.Contains("ScaleY=\"{Binding RelativeSource={RelativeSource TemplatedParent}, Path=Value, Converter={StaticResource PercentToScaleConverter}}\"", xaml);
    }

    [Fact]
    public void MainWindow_ResizeHandleChromeUsesSubtleEdgeMountedStyle()
    {
        var xaml = ReadMainWindowXaml();

        Assert.Contains("Opacity=\"0\"", xaml);
        Assert.Contains("CornerRadius=\"2\"", xaml);
        Assert.Contains("Background=\"#26FFFFFF\"", xaml);
    }

    [Fact]
    public void MainWindow_PresentationKeepsSideDockDetailsOutsideCapsule()
    {
        var code = ReadProjectFile("DynamicIslandBar", "MainWindow.xaml.cs");
        var stateBuildIndex = code.IndexOf("var state = CenterCardPresentationPolicy.Build(", StringComparison.Ordinal);
        var lyricsVisibilityIndex = code.IndexOf(
            "CenterCardLyricsLayer.Visibility = state.ShowLyricsMarquee ? Visibility.Visible : Visibility.Collapsed;",
            StringComparison.Ordinal);
        var detailsVisibilityLine = "CenterCardDetailsLayer.Visibility = IsSideDockMode";
        var detailsVisibilityIndex = code.IndexOf(detailsVisibilityLine, StringComparison.Ordinal);

        Assert.Contains("var state = CenterCardPresentationPolicy.Build(", code);
        Assert.Contains("var sideDetailsState = CenterCardPresentationPolicy.Build(", code);
        Assert.Contains("CenterCardLyricsLayer.Visibility = state.ShowLyricsMarquee ? Visibility.Visible : Visibility.Collapsed;", code);
        Assert.Contains(detailsVisibilityLine, code);
        Assert.Contains("UpdateCenterCardSideDetailsOverlay(app, sideDetailsState);", code);
        Assert.True(stateBuildIndex >= 0);
        Assert.True(lyricsVisibilityIndex > stateBuildIndex);
        Assert.True(detailsVisibilityIndex > lyricsVisibilityIndex);

        var sharedVisibilityBlock = code[stateBuildIndex..(detailsVisibilityIndex + detailsVisibilityLine.Length)];
        Assert.DoesNotContain("preferLyricsInDetails", sharedVisibilityBlock);
    }

    [Fact]
    public void MainWindow_SideDockRoutesPopupsAndHoverDetailsToOuterEdge()
    {
        var code = ReadProjectFile("DynamicIslandBar", "MainWindow.xaml.cs");

        Assert.Contains("PopupFlowDirection.Right", code);
        Assert.Contains("PopupFlowDirection.Left", code);
        Assert.Contains("PlacementMode.Right", code);
        Assert.Contains("PlacementMode.Left", code);
        Assert.Contains("AppHoverOverlayPopup.PlacementTarget = iconHost;", code);
        Assert.Contains("ApplyAppHoverOverlayPopupPlacement(iconHost);", code);
        Assert.Contains("ResolveSideDockPopupDirection(_capsuleConfig.Mode)", code);
    }

    [Fact]
    public void MainWindow_DraggingClampsCapsuleWindowInsideVisibleScreenBounds()
    {
        var code = ReadProjectFile("DynamicIslandBar", "MainWindow.xaml.cs");

        Assert.Contains("CapsuleLayoutManager.ClampWindowOriginToVisibleBounds(", code);
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

    private static string ReadMainWindowXaml()
    {
        return ReadProjectFile("DynamicIslandBar", "MainWindow.xaml");
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
