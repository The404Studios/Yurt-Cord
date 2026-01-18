using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using VeaMarketplace.Client.Services;

namespace VeaMarketplace.Client.Views;

public partial class VoiceCallDashboard : UserControl
{
    private readonly IVoiceService _voiceService = null!;
    private readonly IApiService _apiService = null!;
    private readonly IToastNotificationService _toastService = null!;
    private readonly ObservableCollection<VoiceParticipant> _participants = new();
    private readonly ObservableCollection<ActiveScreenShare> _activeShares = new();
    private bool _isMuted;
    private bool _isDeafened;
    private string? _currentScreenSharerConnectionId;
    private int _frameCount;
    private int _lastWidth;
    private int _lastHeight;
    private bool _isViewingSelfPreview;
    private bool _previewHidden;
    private int _selfPreviewFrameCount;
    private System.Windows.Threading.DispatcherTimer? _statsTimer;

    // Channel name mapping
    private static readonly Dictionary<string, string> ChannelDisplayNames = new()
    {
        { "general-voice", "General Voice" },
        { "music-voice", "Music" },
        { "marketplace-voice", "Marketplace Deals" }
    };

    public VoiceCallDashboard()
    {
        InitializeComponent();

        if (DesignerProperties.GetIsInDesignMode(this))
            return;

        _voiceService = (IVoiceService)App.ServiceProvider.GetService(typeof(IVoiceService))!;
        _apiService = (IApiService)App.ServiceProvider.GetService(typeof(IApiService))!;
        _toastService = (IToastNotificationService)App.ServiceProvider.GetService(typeof(IToastNotificationService))!;

        ParticipantsItemsControl.ItemsSource = _participants;
        ActiveSharesItemsControl.ItemsSource = _activeShares;

        SetupEventHandlers();
        UpdateSelfInfo();
        LoadOutputDevices();

        // Stats timer for FPS display (update once per second, not on every frame)
        _statsTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _statsTimer.Tick += StatsTimer_Tick;
        _statsTimer.Start();

        // Cleanup on unloaded
        Unloaded += OnUnloaded;
    }

    private void StatsTimer_Tick(object? sender, EventArgs e)
    {
        if (_lastWidth > 0 && _lastHeight > 0)
        {
            StreamFpsText.Text = $"{_frameCount} FPS | {_lastWidth}x{_lastHeight}";
        }
        _frameCount = 0;
        _selfPreviewFrameCount = 0;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // Stop and cleanup timer
        if (_statsTimer != null)
        {
            _statsTimer.Stop();
            _statsTimer.Tick -= StatsTimer_Tick;
            _statsTimer = null;
        }

        // Unsubscribe from all VoiceService events
        _voiceService.OnUserJoinedVoice -= OnUserJoinedVoice;
        _voiceService.OnUserLeftVoice -= OnUserLeftVoice;
        _voiceService.OnVoiceChannelUsers -= OnVoiceChannelUsers;
        _voiceService.OnUserSpeaking -= OnUserSpeaking;
        _voiceService.OnLocalAudioLevel -= OnLocalAudioLevel;
        _voiceService.OnScreenFrameReceived -= OnScreenFrameReceived;
        _voiceService.OnUserScreenShareChanged -= OnUserScreenShareChanged;
        _voiceService.OnUserDisconnectedByAdmin -= OnUserDisconnectedByAdmin;
        _voiceService.OnLocalScreenFrameReady -= OnLocalScreenFrameReady;
        _voiceService.ScreenSharingManager.OnScreenShareStarted -= OnScreenShareStarted;
        _voiceService.ScreenSharingManager.OnScreenShareStopped -= OnScreenShareStopped;

        // Close any open windows
        CloseViewingWindows();
    }

    private void LoadOutputDevices()
    {
        try
        {
            var devices = _voiceService.GetOutputDevices();
            OutputDeviceCombo.Items.Clear();

            foreach (var device in devices)
            {
                OutputDeviceCombo.Items.Add(new ComboBoxItem
                {
                    Content = device.Name,
                    Tag = device.DeviceNumber
                });
            }

            // Select first device by default
            if (OutputDeviceCombo.Items.Count > 0)
            {
                OutputDeviceCombo.SelectedIndex = 0;
            }
        }
        catch
        {
            // Audio device enumeration failed
        }
    }

    private void OutputDevice_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (OutputDeviceCombo.SelectedItem is ComboBoxItem item && item.Tag is int deviceNumber)
        {
            _voiceService.SetOutputDevice(deviceNumber);
        }
    }

    private void SetupEventHandlers()
    {
        _voiceService.OnUserJoinedVoice += OnUserJoinedVoice;
        _voiceService.OnUserLeftVoice += OnUserLeftVoice;
        _voiceService.OnVoiceChannelUsers += OnVoiceChannelUsers;
        _voiceService.OnUserSpeaking += OnUserSpeaking;
        _voiceService.OnLocalAudioLevel += OnLocalAudioLevel;
        _voiceService.OnScreenFrameReceived += OnScreenFrameReceived;
        _voiceService.OnUserScreenShareChanged += OnUserScreenShareChanged;
        _voiceService.OnUserDisconnectedByAdmin += OnUserDisconnectedByAdmin;
        _voiceService.OnLocalScreenFrameReady += OnLocalScreenFrameReady;
        _voiceService.ScreenSharingManager.OnScreenShareStarted += OnScreenShareStarted;
        _voiceService.ScreenSharingManager.OnScreenShareStopped += OnScreenShareStopped;
    }

    private void OnUserJoinedVoice(VoiceUserState user)
    {
        Dispatcher.Invoke(() =>
        {
            if (!_participants.Any(p => p.ConnectionId == user.ConnectionId))
            {
                _participants.Add(new VoiceParticipant(user));
            }
            UpdateParticipantCount();
        });
    }

    private void OnUserLeftVoice(VoiceUserState user)
    {
        Dispatcher.Invoke(() =>
        {
            var participant = _participants.FirstOrDefault(p => p.ConnectionId == user.ConnectionId);
            if (participant != null)
            {
                _participants.Remove(participant);
            }
            UpdateParticipantCount();

            // If this user was sharing, clear the stream
            if (_currentScreenSharerConnectionId == user.ConnectionId)
            {
                ClearScreenShare();
            }
        });
    }

    private void OnVoiceChannelUsers(List<VoiceUserState> users)
    {
        Dispatcher.Invoke(() =>
        {
            _participants.Clear();
            foreach (var user in users)
            {
                _participants.Add(new VoiceParticipant(user));

                // Auto-watch if someone is already screen sharing
                if (user.IsScreenSharing && _currentScreenSharerConnectionId == null)
                {
                    _currentScreenSharerConnectionId = user.ConnectionId;
                    StreamerNameText.Text = user.Username;
                    StreamInfoPanel.Visibility = Visibility.Visible;
                    NoStreamPanel.Visibility = Visibility.Collapsed;
                    ScreenShareImage.Visibility = Visibility.Visible;
                }
            }
            UpdateParticipantCount();
            UpdateChannelName();
        });
    }

    private void OnUserSpeaking(string connectionId, string username, bool isSpeaking, double audioLevel)
    {
        Dispatcher.Invoke(() =>
        {
            var participant = _participants.FirstOrDefault(p => p.ConnectionId == connectionId);
            if (participant != null)
            {
                participant.IsSpeaking = isSpeaking;
                participant.AudioLevel = audioLevel;
            }
        });
    }

    private void OnLocalAudioLevel(double level)
    {
        Dispatcher.Invoke(() =>
        {
            var maxWidth = MicLevelBar.Parent is Border parent ? parent.ActualWidth : 200;
            MicLevelBar.Width = level * maxWidth;

            MicLevelIcon.Text = _isMuted ? "🔇" : "🎤";
            MicLevelIcon.Foreground = _isMuted
                ? (System.Windows.Media.Brush)FindResource("TextMutedBrush")
                : (level > 0.1
                    ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(67, 181, 129))
                    : (System.Windows.Media.Brush)FindResource("TextMutedBrush"));
        });
    }

    private void OnScreenFrameReceived(string senderConnectionId, byte[] frameData, int width, int height)
    {
        // Track stats without UI update
        _frameCount++;
        _lastWidth = width;
        _lastHeight = height;

        // Use BeginInvoke for non-blocking async UI updates
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Render, () =>
        {
            try
            {
                // Only update stream info UI when switching streams (not every frame)
                if (_currentScreenSharerConnectionId != senderConnectionId)
                {
                    _currentScreenSharerConnectionId = senderConnectionId;
                    var sharer = _participants.FirstOrDefault(p => p.ConnectionId == senderConnectionId);
                    StreamerNameText.Text = sharer?.Username ?? "Unknown";
                    StreamInfoPanel.Visibility = Visibility.Visible;
                    NoStreamPanel.Visibility = Visibility.Collapsed;
                    ScreenShareImage.Visibility = Visibility.Visible;
                    // Show stream controls when viewing a stream
                    StreamControlsPanel.Visibility = Visibility.Visible;
                }

                // Display frame directly
                using var ms = new MemoryStream(frameData);
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                bitmap.StreamSource = ms;
                bitmap.EndInit();
                bitmap.Freeze();

                ScreenShareImage.Source = bitmap;

                // Update PiP window if open
                if (_pipWindow?.Content is System.Windows.Controls.Image pipImage)
                {
                    pipImage.Source = bitmap;
                }
                // Update fullscreen window if open
                if (_fullscreenWindow?.Content is System.Windows.Controls.Image fsImage)
                {
                    fsImage.Source = bitmap;
                }
            }
            catch
            {
                // Ignore frame errors
            }
        });
    }

    private void OnUserScreenShareChanged(string connectionId, bool isSharing)
    {
        Dispatcher.Invoke(() =>
        {
            var participant = _participants.FirstOrDefault(p => p.ConnectionId == connectionId);
            if (participant != null)
            {
                participant.IsScreenSharing = isSharing;
            }

            if (isSharing)
            {
                // Add to active shares list
                if (!_activeShares.Any(s => s.ConnectionId == connectionId))
                {
                    _activeShares.Add(new ActiveScreenShare
                    {
                        ConnectionId = connectionId,
                        Username = participant?.Username ?? "Unknown"
                    });
                }
                UpdateActiveSharesVisibility();

                // Auto-watch new screen share if not viewing self preview
                if (!_isViewingSelfPreview || _previewHidden)
                {
                    _currentScreenSharerConnectionId = connectionId;
                    StreamerNameText.Text = participant?.Username ?? "Unknown";
                    StreamInfoPanel.Visibility = Visibility.Visible;
                    NoStreamPanel.Visibility = Visibility.Collapsed;
                    ScreenShareImage.Visibility = Visibility.Visible;
                    _frameCount = 0;
                }
            }
            else
            {
                // Remove from active shares list
                var share = _activeShares.FirstOrDefault(s => s.ConnectionId == connectionId);
                if (share != null)
                {
                    _activeShares.Remove(share);
                }
                UpdateActiveSharesVisibility();

                if (_currentScreenSharerConnectionId == connectionId)
                {
                    // If we were viewing this share, switch to another or self preview
                    if (_isViewingSelfPreview && !_previewHidden && _voiceService.IsScreenSharing)
                    {
                        // Stay on self preview
                        _currentScreenSharerConnectionId = null;
                    }
                    else
                    {
                        // Find another active sharer if available
                        var otherSharer = _participants.FirstOrDefault(p => p.IsScreenSharing && p.ConnectionId != connectionId);
                        if (otherSharer != null)
                        {
                            _currentScreenSharerConnectionId = otherSharer.ConnectionId;
                            StreamerNameText.Text = otherSharer.Username;
                        }
                        else if (_voiceService.IsScreenSharing && !_previewHidden)
                        {
                            // Switch to self preview
                            _isViewingSelfPreview = true;
                            _currentScreenSharerConnectionId = null;
                        }
                        else
                        {
                            ClearScreenShare();
                        }
                    }
                }
            }
        });
    }

    private void OnUserDisconnectedByAdmin(string reason)
    {
        Dispatcher.Invoke(() =>
        {
            _toastService.ShowWarning("Disconnected", reason);
        });
    }

    private void OnLocalScreenFrameReady(byte[] frameData, int width, int height)
    {
        // Track stats
        _selfPreviewFrameCount++;

        // Only process if we're viewing self preview and not hidden
        if (!_voiceService.IsScreenSharing || !_isViewingSelfPreview || _previewHidden)
            return;

        // Use BeginInvoke for non-blocking async UI updates
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Render, () =>
        {
            try
            {
                // Update visibility only once when starting preview
                if (SelfSharePreviewToggle.Visibility != Visibility.Visible)
                {
                    SelfSharePreviewToggle.Visibility = Visibility.Visible;
                    UpdateActiveSharesVisibility();
                    ScreenShareImage.Visibility = Visibility.Visible;
                    NoStreamPanel.Visibility = Visibility.Collapsed;
                    StreamInfoPanel.Visibility = Visibility.Visible;
                    StreamerNameText.Text = "You (Preview)";
                }

                // Display frame
                using var ms = new MemoryStream(frameData);
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                bitmap.StreamSource = ms;
                bitmap.EndInit();
                bitmap.Freeze();

                ScreenShareImage.Source = bitmap;
            }
            catch
            {
                // Ignore frame errors
            }
        });
    }

    private void OnScreenShareStarted(RemoteScreenShare share)
    {
        Dispatcher.Invoke(() =>
        {
            if (!_activeShares.Any(s => s.ConnectionId == share.ConnectionId))
            {
                _activeShares.Add(new ActiveScreenShare
                {
                    ConnectionId = share.ConnectionId,
                    Username = share.Username
                });
            }
            UpdateActiveSharesVisibility();
        });
    }

    private void OnScreenShareStopped(string connectionId)
    {
        Dispatcher.Invoke(() =>
        {
            var share = _activeShares.FirstOrDefault(s => s.ConnectionId == connectionId);
            if (share != null)
            {
                _activeShares.Remove(share);
            }
            UpdateActiveSharesVisibility();
        });
    }

    private void UpdateActiveSharesVisibility()
    {
        var hasActiveShares = _activeShares.Count > 0 || _voiceService.IsScreenSharing;
        ActiveSharesSection.Visibility = hasActiveShares ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateSelfInfo()
    {
        var user = _apiService.CurrentUser;
        if (user != null)
        {
            SelfUsernameText.Text = user.Username;
            SetSelfAvatar(user.AvatarUrl);
        }
    }

    /// <summary>
    /// Sets the self avatar with proper fallback handling for special formats.
    /// </summary>
    private void SetSelfAvatar(string? avatarUrl)
    {
        // Check if it's a special format (emoji gradient) or empty - use default
        if (string.IsNullOrWhiteSpace(avatarUrl) ||
            avatarUrl.StartsWith("emoji:") ||
            avatarUrl.StartsWith("gradient:"))
        {
            try
            {
                SelfAvatarBrush.ImageSource = new BitmapImage(new Uri(AppConstants.DefaultAvatarPath));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load default avatar: {ex.Message}");
            }
            return;
        }

        // Try to load the URL as an image
        try
        {
            SelfAvatarBrush.ImageSource = new BitmapImage(new Uri(avatarUrl));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load avatar: {ex.Message}");
            // Use default avatar as fallback
            try
            {
                SelfAvatarBrush.ImageSource = new BitmapImage(new Uri(AppConstants.DefaultAvatarPath));
            }
            catch
            {
                // Fallback also failed
            }
        }
    }

    private void UpdateChannelName()
    {
        var channelId = _voiceService.CurrentChannelId;
        if (channelId != null)
        {
            ChannelNameText.Text = ChannelDisplayNames.TryGetValue(channelId, out var name)
                ? name : channelId;
        }
    }

    private void UpdateParticipantCount()
    {
        ParticipantCountText.Text = $"{_participants.Count} participant{(_participants.Count != 1 ? "s" : "")}";
    }

    private void ClearScreenShare()
    {
        _currentScreenSharerConnectionId = null;
        ScreenShareImage.Source = null;
        ScreenShareImage.Visibility = Visibility.Collapsed;
        StreamInfoPanel.Visibility = Visibility.Collapsed;
        StreamControlsPanel.Visibility = Visibility.Collapsed;
        NoStreamPanel.Visibility = Visibility.Visible;

        // Close any open viewing windows
        CloseViewingWindows();
    }

    private void CloseViewingWindows()
    {
        if (_pipWindow != null)
        {
            try { _pipWindow.Close(); } catch (Exception ex) { Debug.WriteLine($"Failed to close PiP window: {ex.Message}"); }
            _pipWindow = null;
        }
        if (_fullscreenWindow != null)
        {
            try { _fullscreenWindow.Close(); } catch (Exception ex) { Debug.WriteLine($"Failed to close fullscreen window: {ex.Message}"); }
            _fullscreenWindow = null;
            _isFullscreen = false;
        }
    }

    private VoiceParticipant? GetParticipantFromSender(object sender)
    {
        if (sender is MenuItem menuItem)
        {
            var parent = menuItem.Parent;
            while (parent != null && !(parent is ContextMenu))
            {
                if (parent is MenuItem parentMenuItem)
                    parent = parentMenuItem.Parent;
                else
                    break;
            }

            if (parent is ContextMenu contextMenu && contextMenu.PlacementTarget is FrameworkElement element)
            {
                return element.Tag as VoiceParticipant ?? element.DataContext as VoiceParticipant;
            }
        }
        return null;
    }

    #region Button Click Handlers

    private async void ScreenShare_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_voiceService.IsScreenSharing)
            {
                await _voiceService.StopScreenShareAsync();
                ScreenShareIcon.Text = "🖥";
                ScreenShareBtn.Background = null;
                ScreenShareBtn.ToolTip = "Share Screen";

                // Reset self preview state
                _isViewingSelfPreview = false;
                _previewHidden = false;
                SelfSharePreviewToggle.Visibility = Visibility.Collapsed;
                UpdateActiveSharesVisibility();

                // If we were viewing self preview, clear the screen
                if (StreamerNameText.Text.Contains("You"))
                {
                    ClearScreenShare();
                }
            }
            else
            {
                // Show screen share picker dialog
                var picker = new ScreenSharePicker(_voiceService)
                {
                    Owner = Window.GetWindow(this)
                };

                if (picker.ShowDialog() == true && picker.SelectedDisplay != null)
                {
                    // Start screen sharing with the selected display and settings
                    var settings = picker.GetSettings();
                    await _voiceService.StartScreenShareAsync(picker.SelectedDisplay, settings);
                    ScreenShareIcon.Text = "🛑";
                    ScreenShareBtn.Background = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(67, 181, 129));
                    ScreenShareBtn.ToolTip = $"Stop Sharing ({picker.SelectedDisplay.FriendlyName}) - {settings.TargetWidth}x{settings.TargetHeight} @ {settings.TargetFps}fps";

                    // Enable self preview by default when starting to share
                    _isViewingSelfPreview = true;
                    _previewHidden = false;
                    _currentScreenSharerConnectionId = null; // Clear any other viewer to show self
                    SelfSharePreviewToggle.Visibility = Visibility.Visible;
                    UpdateActiveSharesVisibility();
                }
            }
        }
        catch (Exception ex)
        {
            _toastService.ShowError("Screen Share", $"Failed: {ex.Message}");
        }
    }

    private void Mute_Click(object sender, RoutedEventArgs e)
    {
        _isMuted = !_isMuted;
        _voiceService.IsMuted = _isMuted;
        MuteIcon.Text = _isMuted ? "🔇" : "🎤";
        MuteIcon.Opacity = _isMuted ? 0.5 : 1;
        SelfStatusText.Text = _isMuted ? "Muted" : "Connected";
        SelfStatusText.Foreground = _isMuted
            ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(237, 66, 69))
            : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(67, 181, 129));
    }

    private void Deafen_Click(object sender, RoutedEventArgs e)
    {
        _isDeafened = !_isDeafened;
        _voiceService.IsDeafened = _isDeafened;
        DeafenIcon.Text = _isDeafened ? "🔈" : "🔊";
        DeafenIcon.Opacity = _isDeafened ? 0.5 : 1;

        if (_isDeafened)
        {
            _isMuted = true;
            _voiceService.IsMuted = true;
            MuteIcon.Text = "🔇";
            MuteIcon.Opacity = 0.5;
            SelfStatusText.Text = "Deafened";
            SelfStatusText.Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(237, 66, 69));
        }
        else
        {
            SelfStatusText.Text = _isMuted ? "Muted" : "Connected";
        }
    }

    private async void Leave_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await _voiceService.LeaveVoiceChannelAsync();
        }
        catch (Exception ex)
        {
            _toastService.ShowError("Leave Channel", $"Failed: {ex.Message}");
        }
    }

    private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (VolumeText != null)
        {
            VolumeText.Text = $"{(int)e.NewValue}%";
        }
        // Set master volume (slider 0-100 maps to 0.0-1.0)
        if (_voiceService != null)
        {
            _voiceService.MasterVolume = (float)(e.NewValue / 100.0);
        }
    }

    #endregion

    #region Context Menu Handlers

    private void MuteUser_Click(object sender, RoutedEventArgs e)
    {
        var participant = GetParticipantFromSender(sender);
        if (participant == null) return;

        var isMuted = _voiceService.IsUserMuted(participant.ConnectionId);
        _voiceService.SetUserMuted(participant.ConnectionId, !isMuted);

        var action = isMuted ? "Unmuted" : "Muted";
        _toastService.ShowInfo($"User {action}", $"{action} {participant.Username} for yourself");
    }

    private void AdjustVolume_Click(object sender, RoutedEventArgs e)
    {
        var participant = GetParticipantFromSender(sender);
        if (participant == null) return;

        var currentVolume = _voiceService.GetUserVolume(participant.ConnectionId) * 100;
        var input = InputDialog.Show(
            "Adjust User Volume",
            $"Enter volume percentage for {participant.Username} (0-200):",
            currentVolume.ToString("F0"));

        if (!string.IsNullOrEmpty(input) && float.TryParse(input, out var volume))
        {
            volume = Math.Clamp(volume, 0, 200);
            _voiceService.SetUserVolume(participant.ConnectionId, volume / 100f);
            _toastService.ShowInfo("Volume Adjusted", $"{participant.Username}'s volume set to {volume}%");
        }
    }

    private void WatchScreen_Click(object sender, RoutedEventArgs e)
    {
        var participant = GetParticipantFromSender(sender);
        if (participant == null) return;

        // If they're sharing, focus on their stream
        if (participant.IsScreenSharing || _currentScreenSharerConnectionId == participant.ConnectionId)
        {
            // Already showing their stream in main view
            _toastService.ShowInfo("Screen Share", $"Now watching {participant.Username}'s screen");
        }
        else
        {
            // Open in separate window
            var viewer = new ScreenShareViewer(_voiceService, participant.ConnectionId, participant.Username);
            viewer.Show();
        }
    }

    private void ViewProfile_Click(object sender, RoutedEventArgs e)
    {
        var participant = GetParticipantFromSender(sender);
        if (participant == null) return;

        var navService = (INavigationService?)App.ServiceProvider.GetService(typeof(INavigationService));
        navService?.NavigateToProfile(participant.UserId);
    }

    #endregion

    #region Active Screen Share Handlers

    private void ActiveShare_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border && border.Tag is ActiveScreenShare share)
        {
            // Switch to viewing this user's screen share
            _isViewingSelfPreview = false;
            _currentScreenSharerConnectionId = share.ConnectionId;

            // Update UI to show we're viewing this share
            StreamerNameText.Text = share.Username;
            StreamInfoPanel.Visibility = Visibility.Visible;
            NoStreamPanel.Visibility = Visibility.Collapsed;
            ScreenShareImage.Visibility = Visibility.Visible;
            _frameCount = 0;
        }
    }

    private void SelfSharePreview_Click(object sender, MouseButtonEventArgs e)
    {
        if (!_voiceService.IsScreenSharing) return;

        // Toggle between showing self preview and hiding it
        if (_isViewingSelfPreview)
        {
            // Toggle hidden state
            _previewHidden = !_previewHidden;
            PreviewToggleText.Text = _previewHidden ? "👁‍🗨" : "👁";

            if (_previewHidden)
            {
                // Hide the preview but keep sharing
                if (StreamerNameText.Text.Contains("You"))
                {
                    // Check if there's another share to view
                    var otherShare = _activeShares.FirstOrDefault();
                    if (otherShare != null)
                    {
                        _currentScreenSharerConnectionId = otherShare.ConnectionId;
                        StreamerNameText.Text = otherShare.Username;
                    }
                    else
                    {
                        // No other shares, show "hidden preview" message
                        NoStreamPanel.Visibility = Visibility.Visible;
                        ScreenShareImage.Visibility = Visibility.Collapsed;
                        StreamInfoPanel.Visibility = Visibility.Collapsed;
                    }
                }
            }
        }
        else
        {
            // Start viewing self preview
            _isViewingSelfPreview = true;
            _previewHidden = false;
            _currentScreenSharerConnectionId = null;
            PreviewToggleText.Text = "👁";
        }
    }

    #endregion

    #region Screen Share Controls

    private bool _isFullscreen;
    private Window? _fullscreenWindow;
    private Window? _pipWindow;

    private void Fullscreen_Click(object sender, RoutedEventArgs e)
    {
        if (_isFullscreen)
        {
            ExitFullscreen();
        }
        else
        {
            EnterFullscreen();
        }
    }

    private void EnterFullscreen()
    {
        _isFullscreen = true;
        FullscreenIcon.Text = "⛏";

        // Create fullscreen window
        _fullscreenWindow = new Window
        {
            Title = "Screen Share - Fullscreen",
            WindowStyle = WindowStyle.None,
            WindowState = WindowState.Maximized,
            Background = System.Windows.Media.Brushes.Black,
            Topmost = true
        };

        var image = new System.Windows.Controls.Image
        {
            Stretch = System.Windows.Media.Stretch.Uniform,
            Source = ScreenShareImage.Source
        };

        _fullscreenWindow.Content = image;
        _fullscreenWindow.KeyDown += (s, e) =>
        {
            if (e.Key == Key.Escape)
            {
                ExitFullscreen();
            }
        };
        _fullscreenWindow.Closed += (s, e) => ExitFullscreen();
        _fullscreenWindow.Show();
    }

    private void ExitFullscreen()
    {
        _isFullscreen = false;
        FullscreenIcon.Text = "⛶";

        if (_fullscreenWindow != null)
        {
            _fullscreenWindow.Close();
            _fullscreenWindow = null;
        }
    }

    private void PictureInPicture_Click(object sender, RoutedEventArgs e)
    {
        if (_pipWindow != null)
        {
            _pipWindow.Close();
            _pipWindow = null;
            return;
        }

        // Create PiP window
        _pipWindow = new Window
        {
            Title = "Picture in Picture",
            Width = 400,
            Height = 225,
            WindowStyle = WindowStyle.ToolWindow,
            Topmost = true,
            Background = System.Windows.Media.Brushes.Black,
            ResizeMode = ResizeMode.CanResizeWithGrip
        };

        var image = new System.Windows.Controls.Image
        {
            Stretch = System.Windows.Media.Stretch.Uniform,
            Source = ScreenShareImage.Source
        };

        _pipWindow.Content = image;
        _pipWindow.Closed += (s, e) => _pipWindow = null;
        _pipWindow.Show();

        // Position in bottom-right corner
        var workArea = SystemParameters.WorkArea;
        _pipWindow.Left = workArea.Right - _pipWindow.Width - 20;
        _pipWindow.Top = workArea.Bottom - _pipWindow.Height - 20;
    }

    private void Quality_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (QualitySelector.SelectedItem is ComboBoxItem item && _currentScreenSharerConnectionId != null)
        {
            var quality = item.Tag?.ToString() ?? "Auto";
            // Request quality change from the sharer (future implementation)
            // _voiceService.RequestScreenQuality(_currentScreenSharerConnectionId, quality);
        }
    }

    private void ShowStreamControls()
    {
        StreamControlsPanel.Visibility = Visibility.Visible;
    }

    private void HideStreamControls()
    {
        StreamControlsPanel.Visibility = Visibility.Collapsed;
    }

    private void UpdateViewerCount(int count)
    {
        Dispatcher.Invoke(() =>
        {
            ViewerCountText.Text = count.ToString();
        });
    }

    #endregion
}

/// <summary>
/// Represents an active screen share from another user
/// </summary>
public class ActiveScreenShare
{
    public string ConnectionId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
}

/// <summary>
/// Wrapper class for voice participants with INotifyPropertyChanged support
/// </summary>
public class VoiceParticipant : INotifyPropertyChanged
{
    private bool _isSpeaking;
    private bool _isMuted;
    private bool _isDeafened;
    private bool _isScreenSharing;
    private double _audioLevel;

    public string ConnectionId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;

    public bool IsSpeaking
    {
        get => _isSpeaking;
        set { _isSpeaking = value; OnPropertyChanged(nameof(IsSpeaking)); }
    }

    public bool IsMuted
    {
        get => _isMuted;
        set { _isMuted = value; OnPropertyChanged(nameof(IsMuted)); }
    }

    public bool IsDeafened
    {
        get => _isDeafened;
        set { _isDeafened = value; OnPropertyChanged(nameof(IsDeafened)); }
    }

    public bool IsScreenSharing
    {
        get => _isScreenSharing;
        set { _isScreenSharing = value; OnPropertyChanged(nameof(IsScreenSharing)); }
    }

    public double AudioLevel
    {
        get => _audioLevel;
        set { _audioLevel = value; OnPropertyChanged(nameof(AudioLevel)); }
    }

    public VoiceParticipant() { }

    public VoiceParticipant(VoiceUserState state)
    {
        ConnectionId = state.ConnectionId;
        UserId = state.UserId;
        Username = state.Username;
        AvatarUrl = state.AvatarUrl;
        IsSpeaking = state.IsSpeaking;
        IsMuted = state.IsMuted;
        IsDeafened = state.IsDeafened;
        AudioLevel = state.AudioLevel;
        IsScreenSharing = state.IsScreenSharing;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

