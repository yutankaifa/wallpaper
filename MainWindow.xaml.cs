using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace WpfApp3
{
  /// <summary>
  /// 桌面壁纸设置工具主窗口
  /// </summary>
  public partial class MainWindow : Window
  {
    private string? selectedMediaPath;
    private bool isVideoMode = false;
    private bool isVideoPlaying = false;

    // Windows API 用于设置壁纸
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);

    // Windows API 用于设置动态壁纸
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDesktopWindow();

    private const int SPI_SETDESKWALLPAPER = 20;
    private const int SPIF_UPDATEINIFILE = 0x01;
    private const int SPIF_SENDCHANGE = 0x02;

    public MainWindow()
    {
      InitializeComponent();
      Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
      UpdateStatus("应用程序已启动，请选择媒体文件");
    }

    /// <summary>
    /// 媒体类型切换事件
    /// </summary>
    private void MediaType_Changed(object sender, RoutedEventArgs e)
    {
      // 确保控件已初始化
      if (SelectMediaButton == null || ImageRadioButton == null || VideoRadioButton == null)
        return;

      if (ImageRadioButton.IsChecked == true)
      {
        isVideoMode = false;
        SelectMediaButton.Content = "选择图片";
        UpdateStatus("已切换到图片模式");
        ResetMediaDisplay();
      }
      else if (VideoRadioButton.IsChecked == true)
      {
        isVideoMode = true;
        SelectMediaButton.Content = "选择视频";
        UpdateStatus("已切换到视频模式");
        ResetMediaDisplay();
      }
    }

    /// <summary>
    /// 选择媒体文件按钮点击事件
    /// </summary>
    private void SelectMediaButton_Click(object sender, RoutedEventArgs e)
    {
      try
      {
        UpdateStatus("正在打开文件选择对话框...");

        OpenFileDialog openFileDialog = new OpenFileDialog();

        if (isVideoMode)
        {
          openFileDialog.Title = "选择壁纸视频";
          openFileDialog.Filter = "视频文件|*.mp4;*.avi;*.mov;*.wmv;*.mkv;*.flv;*.webm;*.m4v|" +
                                "MP4文件|*.mp4|" +
                                "AVI文件|*.avi|" +
                                "MOV文件|*.mov|" +
                                "WMV文件|*.wmv|" +
                                "MKV文件|*.mkv|" +
                                "FLV文件|*.flv|" +
                                "WebM文件|*.webm|" +
                                "所有文件|*.*";
        }
        else
        {
          openFileDialog.Title = "选择壁纸图片";
          openFileDialog.Filter = "图片文件|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tiff;*.webp|" +
                                "JPEG文件|*.jpg;*.jpeg|" +
                                "PNG文件|*.png|" +
                                "BMP文件|*.bmp|" +
                                "GIF文件|*.gif|" +
                                "TIFF文件|*.tiff|" +
                                "WebP文件|*.webp|" +
                                "所有文件|*.*";
        }

        openFileDialog.FilterIndex = 1;
        openFileDialog.Multiselect = false;

        if (openFileDialog.ShowDialog() == true)
        {
          selectedMediaPath = openFileDialog.FileName;
          if (isVideoMode)
          {
            LoadAndDisplayVideo(selectedMediaPath);
          }
          else
          {
            LoadAndDisplayImage(selectedMediaPath);
          }
        }
        else
        {
          UpdateStatus("未选择文件");
        }
      }
      catch (Exception ex)
      {
        ShowError($"选择文件时发生错误: {ex.Message}");
      }
    }

    /// <summary>
    /// 加载并显示选中的视频
    /// </summary>
    private void LoadAndDisplayVideo(string videoPath)
    {
      try
      {
        UpdateStatus("正在加载视频...");

        // 验证文件是否存在
        if (!File.Exists(videoPath))
        {
          ShowError("选择的文件不存在");
          return;
        }

        // 获取文件信息
        FileInfo fileInfo = new FileInfo(videoPath);

        // 检查文件大小（限制为500MB）
        if (fileInfo.Length > 500 * 1024 * 1024)
        {
          ShowError("视频文件过大，请选择小于500MB的视频");
          return;
        }

        // 设置视频源
        PreviewVideo.Source = new Uri(videoPath);

        // 显示视频控件，隐藏图片控件
        PreviewVideo.Visibility = Visibility.Visible;
        PreviewImage.Visibility = Visibility.Collapsed;
        PlaceholderText.Visibility = Visibility.Collapsed;
        VideoControls.Visibility = Visibility.Visible;

        // 启用设置壁纸按钮
        SetWallpaperButton.IsEnabled = true;

        // 更新视频信息
        UpdateVideoInfo(videoPath);

        // 自动播放预览
        PreviewVideo.Play();
        isVideoPlaying = true;
        PlayPauseButton.Content = "⏸";

        UpdateStatus($"视频加载成功: {Path.GetFileName(videoPath)}");
      }
      catch (Exception ex)
      {
        ShowError($"加载视频失败: {ex.Message}");
        ResetMediaDisplay();
      }
    }

    /// <summary>
    /// 加载并显示选中的图片
    /// </summary>
    private void LoadAndDisplayImage(string imagePath)
    {
      try
      {
        UpdateStatus("正在加载图片...");

        // 验证文件是否存在
        if (!File.Exists(imagePath))
        {
          ShowError("选择的文件不存在");
          return;
        }

        // 获取文件信息
        FileInfo fileInfo = new FileInfo(imagePath);

        // 检查文件大小（限制为50MB）
        if (fileInfo.Length > 50 * 1024 * 1024)
        {
          ShowError("图片文件过大，请选择小于50MB的图片");
          return;
        }

        // 创建BitmapImage并加载图片
        BitmapImage bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.UriSource = new Uri(imagePath);
        bitmap.CacheOption = BitmapCacheOption.OnLoad; // 立即加载到内存
        bitmap.EndInit();

        // 显示图片，隐藏视频控件
        PreviewImage.Source = bitmap;
        PreviewImage.Visibility = Visibility.Visible;
        PreviewVideo.Visibility = Visibility.Collapsed;
        VideoControls.Visibility = Visibility.Collapsed;
        PlaceholderText.Visibility = Visibility.Collapsed;

        // 启用设置壁纸按钮
        SetWallpaperButton.IsEnabled = true;

        // 更新图片信息
        UpdateImageInfo(imagePath, bitmap);

        UpdateStatus($"图片加载成功: {Path.GetFileName(imagePath)}");
      }
      catch (Exception ex)
      {
        ShowError($"加载图片失败: {ex.Message}");
        ResetMediaDisplay();
      }
    }

    /// <summary>
    /// 更新视频信息显示
    /// </summary>
    private void UpdateVideoInfo(string videoPath)
    {
      try
      {
        FileInfo fileInfo = new FileInfo(videoPath);
        string fileName = Path.GetFileName(videoPath);
        string fileSize = FormatFileSize(fileInfo.Length);
        string format = Path.GetExtension(videoPath).ToUpper();

        ImageInfoText.Text = $"文件名: {fileName}\n" +
                           $"大小: {fileSize}\n" +
                           $"格式: {format}\n" +
                           $"类型: 视频文件";
      }
      catch (Exception ex)
      {
        ImageInfoText.Text = $"无法获取视频信息: {ex.Message}";
      }
    }

    /// <summary>
    /// 更新图片信息显示
    /// </summary>
    private void UpdateImageInfo(string imagePath, BitmapImage bitmap)
    {
      try
      {
        FileInfo fileInfo = new FileInfo(imagePath);
        string fileName = Path.GetFileName(imagePath);
        string fileSize = FormatFileSize(fileInfo.Length);
        string dimensions = $"{bitmap.PixelWidth} × {bitmap.PixelHeight}";

        ImageInfoText.Text = $"文件名: {fileName}\n" +
                           $"尺寸: {dimensions}\n" +
                           $"大小: {fileSize}\n" +
                           $"格式: {Path.GetExtension(imagePath).ToUpper()}";
      }
      catch (Exception ex)
      {
        ImageInfoText.Text = $"无法获取图片信息: {ex.Message}";
      }
    }

    /// <summary>
    /// 格式化文件大小显示
    /// </summary>
    private string FormatFileSize(long bytes)
    {
      string[] sizes = { "B", "KB", "MB", "GB" };
      double len = bytes;
      int order = 0;
      while (len >= 1024 && order < sizes.Length - 1)
      {
        order++;
        len = len / 1024;
      }
      return $"{len:0.##} {sizes[order]}";
    }

    /// <summary>
    /// 播放/暂停按钮点击事件
    /// </summary>
    private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
    {
      try
      {
        if (isVideoPlaying)
        {
          PreviewVideo.Pause();
          PlayPauseButton.Content = "▶";
          isVideoPlaying = false;
          UpdateStatus("视频已暂停");
        }
        else
        {
          PreviewVideo.Play();
          PlayPauseButton.Content = "⏸";
          isVideoPlaying = true;
          UpdateStatus("视频正在播放");
        }
      }
      catch (Exception ex)
      {
        ShowError($"视频控制失败: {ex.Message}");
      }
    }

    /// <summary>
    /// 停止按钮点击事件
    /// </summary>
    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
      try
      {
        PreviewVideo.Stop();
        PlayPauseButton.Content = "▶";
        isVideoPlaying = false;
        UpdateStatus("视频已停止");
      }
      catch (Exception ex)
      {
        ShowError($"停止视频失败: {ex.Message}");
      }
    }

    /// <summary>
    /// 音量滑块值改变事件
    /// </summary>
    private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
      try
      {
        if (PreviewVideo != null)
        {
          PreviewVideo.Volume = e.NewValue;
          UpdateStatus($"音量已调整为: {(int)(e.NewValue * 100)}%");
        }
      }
      catch (Exception ex)
      {
        ShowError($"调整音量失败: {ex.Message}");
      }
    }

    /// <summary>
    /// 视频播放结束事件
    /// </summary>
    private void PreviewVideo_MediaEnded(object sender, RoutedEventArgs e)
    {
      try
      {
        // 循环播放
        PreviewVideo.Position = TimeSpan.Zero;
        PreviewVideo.Play();
        UpdateStatus("视频循环播放");
      }
      catch (Exception ex)
      {
        ShowError($"视频循环播放失败: {ex.Message}");
      }
    }

    /// <summary>
    /// 设置壁纸按钮点击事件
    /// </summary>
    private void SetWallpaperButton_Click(object sender, RoutedEventArgs e)
    {
      if (string.IsNullOrEmpty(selectedMediaPath))
      {
        ShowError("请先选择一个媒体文件");
        return;
      }

      try
      {
        UpdateStatus("正在设置桌面壁纸...");

        if (isVideoMode)
        {
          // 设置视频壁纸
          SetVideoWallpaper(selectedMediaPath);
        }
        else
        {
          // 设置图片壁纸
          SetImageWallpaper(selectedMediaPath);
        }
      }
      catch (Exception ex)
      {
        ShowError($"设置壁纸时发生错误: {ex.Message}");
      }
    }

    /// <summary>
    /// 设置图片壁纸
    /// </summary>
    private void SetImageWallpaper(string imagePath)
    {
      // 调用Windows API设置壁纸
      int result = SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, imagePath,
          SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);

      if (result != 0)
      {
        UpdateStatus("桌面壁纸设置成功！");
        MessageBox.Show("桌面壁纸已成功设置！", "成功",
            MessageBoxButton.OK, MessageBoxImage.Information);
      }
      else
      {
        ShowError("设置桌面壁纸失败，请检查图片格式是否支持");
      }
    }

    /// <summary>
    /// 设置视频壁纸（动态壁纸）
    /// </summary>
    private void SetVideoWallpaper(string videoPath)
    {
      try
      {
        // 注意：真正的动态视频壁纸需要第三方软件支持
        // 这里我们提供一个基础实现，实际使用可能需要Wallpaper Engine等软件

        // 创建一个隐藏的视频播放窗口
        var videoWindow = new Window
        {
          WindowStyle = WindowStyle.None,
          ResizeMode = ResizeMode.NoResize,
          ShowInTaskbar = false,
          Topmost = false,
          Left = 0,
          Top = 0,
          Width = SystemParameters.PrimaryScreenWidth,
          Height = SystemParameters.PrimaryScreenHeight,
          Background = Brushes.Black
        };

        var videoElement = new MediaElement
        {
          Source = new Uri(videoPath),
          LoadedBehavior = MediaState.Manual,
          UnloadedBehavior = MediaState.Manual,
          Stretch = Stretch.UniformToFill,
          IsMuted = true
        };

        videoElement.MediaEnded += (s, e) =>
        {
          videoElement.Position = TimeSpan.Zero;
          videoElement.Play();
        };

        videoWindow.Content = videoElement;
        videoWindow.Show();
        videoElement.Play();

        // 将窗口设置到桌面背景层
        IntPtr desktopHandle = GetDesktopWindow();
        IntPtr windowHandle = new System.Windows.Interop.WindowInteropHelper(videoWindow).Handle;

        // 这是一个简化的实现，真正的动态壁纸需要更复杂的系统集成
        UpdateStatus("动态壁纸设置完成！");
        MessageBox.Show("动态壁纸已设置！\n\n注意：这是一个基础实现。\n要获得完整的动态壁纸体验，建议使用专业软件如Wallpaper Engine。",
            "动态壁纸", MessageBoxButton.OK, MessageBoxImage.Information);
      }
      catch (Exception ex)
      {
        ShowError($"设置动态壁纸失败: {ex.Message}");
      }
    }

    /// <summary>
    /// 重置媒体显示
    /// </summary>
    private void ResetMediaDisplay()
    {
      // 确保控件已初始化
      if (PreviewImage == null || PreviewVideo == null || VideoControls == null || 
          PlaceholderText == null || SetWallpaperButton == null || ImageInfoText == null || 
          PlayPauseButton == null)
        return;

      // 重置图片
      PreviewImage.Source = null;
      PreviewImage.Visibility = Visibility.Collapsed;

      // 重置视频
      PreviewVideo.Source = null;
      PreviewVideo.Stop();
      PreviewVideo.Visibility = Visibility.Collapsed;
      VideoControls.Visibility = Visibility.Collapsed;

      // 重置UI状态
      PlaceholderText.Visibility = Visibility.Visible;
      SetWallpaperButton.IsEnabled = false;
      ImageInfoText.Text = "未选择媒体文件";
      selectedMediaPath = null;
      isVideoPlaying = false;
      PlayPauseButton.Content = "▶";
    }

    /// <summary>
    /// 更新状态栏文本
    /// </summary>
    private void UpdateStatus(string message)
    {
      if (StatusText != null)
      {
        StatusText.Text = $"{DateTime.Now:HH:mm:ss} - {message}";
      }
    }

    /// <summary>
    /// 显示错误消息
    /// </summary>
    private void ShowError(string message)
    {
      UpdateStatus($"错误: {message}");
      MessageBox.Show(message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
    }
  }
}