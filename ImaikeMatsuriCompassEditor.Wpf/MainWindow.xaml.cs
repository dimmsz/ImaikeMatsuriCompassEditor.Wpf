using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ImaikeMatsuriCompassEditor.Wpf;

public partial class MainWindow : Window
{
    private const string OfficialTimetableUrl = "https://www.imaike55.com/%E4%BB%8A%E6%B1%A0%E3%81%BE%E3%81%A4%E3%82%8A2026%E3%82%BF%E3%82%A4%E3%83%A0%E3%83%86%E3%83%BC%E3%83%96%E3%83%AB";
    private readonly SupabaseService _supabase = new();
    private readonly List<EventSchedule> _allSchedules = [];
    private CancellationTokenSource? _autoSaveCts;
    private bool _dataLoaded;

    public ObservableCollection<Venue> Venues { get; } = [];
    public ObservableCollection<EventSchedule> CurrentSchedules { get; } = [];

    private EventSchedule? _selectedSchedule;
    public EventSchedule? SelectedSchedule
    {
        get => _selectedSchedule;
        set
        {
            if (ReferenceEquals(_selectedSchedule, value)) return;
            _selectedSchedule = value;
            RefreshEditControls();
            OnPropertyChanged();
        }
    }

    public ObservableCollection<string> Genres { get; } =
        ["音楽", "ダンス", "ステージ", "その他"];

    public ObservableCollection<string> AvailableTags { get; } =
        [
            "DJ", "ジャズ", "ロック", "ブルース", "パンク", "ソウル・R&B", "ラテン",
            "ワールド音楽", "合唱", "吹奏楽", "和楽器", "フラメンコ", "沖縄", "韓国", "打楽器",
            "三線", "ダンススクール", "バレエ", "紙芝居", "演劇", "一人芝居", "詩朗読",
            "トーク", "マジック", "大道芸", "クラウン", "アクロバット", "盆踊り",
            "プロレス", "スポーツ", "空手", "キック", "地域交流", "商店街", "学生・学校",
            "社会人", "青少年", "パフォーマンス", "落語", "三味線", "大正琴", "ご当地ソング",
            "ウクレレ", "幻燈", "ゴスペル", "ディスコ", "カポエイラ", "サンバ", "ブラジル",
            "能登", "結婚式", "名古屋グランパス", "ライブ", "伝統芸能"
        ];

    public ObservableCollection<string> SelectedTags { get; } = [];
    private bool _updatingEditControls;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        ScheduleDataGrid.ItemsSource = CurrentSchedules;
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= MainWindow_Loaded;
        await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        try
        {
            _dataLoaded = false;
            _autoSaveCts?.Cancel();
            ConnectionStatus.Content = "Supabase接続中…";
            var venues = await _supabase.GetVenuesAsync();
            var schedules = await _supabase.GetSchedulesAsync();

            Venues.Clear();
            foreach (var venue in venues) Venues.Add(venue);
            _allSchedules.Clear();
            _allSchedules.AddRange(schedules);

            SelectedSchedule = null;
            ConnectionStatus.Content = $"接続済み / 会場 {Venues.Count} / スケジュール {_allSchedules.Count}件 / 自動保存ON";
            VenueComboBox.SelectedIndex = Venues.Count > 0 ? 0 : -1;
            _dataLoaded = true;
        }
        catch (Exception ex)
        {
            ConnectionStatus.Content = "接続エラー";
            MessageBox.Show(this, ex.Message, "Supabase接続エラー", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void VenueComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (VenueComboBox.SelectedItem is not Venue venue)
        {
            CurrentSchedules.Clear();
            ScheduleCountText.Text = "-";
            ScheduleDataGrid.SelectedItem = null;
            SelectedSchedule = null;
            return;
        }

        CurrentSchedules.Clear();
        foreach (var schedule in _allSchedules
                     .Where(x => x.VenueId == venue.Id)
                     .OrderBy(x => x.EventDate)
                     .ThenBy(x => x.StartTime))
        {
            CurrentSchedules.Add(schedule);
        }

        ScheduleCountText.Text = CurrentSchedules.Count.ToString();
        ScheduleDataGrid.SelectedItem = null;
        SelectedSchedule = null;
    }

    private void ScheduleDataGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (FindVisualParent<DataGridRow>(e.OriginalSource as DependencyObject) is DataGridRow row &&
            row.Item is EventSchedule schedule)
        {
            ScheduleDataGrid.SelectedItem = schedule;
            SelectedSchedule = schedule;
        }
    }

    private void ScheduleDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SelectedSchedule = ScheduleDataGrid.SelectedItem as EventSchedule;
    }

    private void RefreshEditControls()
    {
        if (!IsInitialized)
            return;

        _updatingEditControls = true;
        try
        {
            var schedule = _selectedSchedule;
            EditEventDateText.Text = schedule?.EventDateText ?? "—";
            EditStartTimeText.Text = schedule?.StartTimeText ?? "—";
            EditEndTimeText.Text = schedule?.EndTimeText ?? "—";
            EditTitleText.Text = schedule?.Title ?? "イベントを選択してください";
            EditGenreComboBox.SelectedItem = schedule?.Genre;
            EditVerifiedCheckBox.IsChecked = schedule?.Verified ?? false;

            SelectedTags.Clear();
            if (schedule is not null)
            {
                foreach (var tag in schedule.Tags)
                    SelectedTags.Add(tag);
            }

            TagComboBox.SelectedIndex = -1;
        }
        finally
        {
            _updatingEditControls = false;
        }
    }

    private void EditGenreComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingEditControls || _selectedSchedule is null)
            return;
        if (EditGenreComboBox.SelectedItem is string genre)
        {
            _selectedSchedule.Genre = genre;
            QueueAutoSave(_selectedSchedule);
        }
    }

    private void EditVerifiedCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_updatingEditControls || _selectedSchedule is null)
            return;
        _selectedSchedule.Verified = EditVerifiedCheckBox.IsChecked == true;
        QueueAutoSave(_selectedSchedule);
    }

    private void AddTagButton_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedSchedule is null || TagComboBox.SelectedItem is not string tag)
            return;

        if (!SelectedSchedule.Tags.Contains(tag))
            SelectedSchedule.Tags.Add(tag);
        if (!SelectedTags.Contains(tag))
            SelectedTags.Add(tag);
        TagComboBox.SelectedIndex = -1;
        QueueAutoSave(SelectedSchedule);
    }

    private void RemoveTagButton_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedSchedule is null || sender is not Button button || button.Tag is not string tag)
            return;
        SelectedSchedule.Tags.Remove(tag);
        SelectedTags.Remove(tag);
        QueueAutoSave(SelectedSchedule);
    }

    private void QueueAutoSave(EventSchedule schedule)
    {
        if (!_dataLoaded)
            return;

        _autoSaveCts?.Cancel();
        var cts = new CancellationTokenSource();
        _autoSaveCts = cts;
        _ = AutoSaveAsync(schedule, cts.Token);
    }

    private async Task AutoSaveAsync(EventSchedule schedule, CancellationToken cancellationToken)
    {
        try
        {
            ConnectionStatus.Content = "変更を自動保存中…";
            await Task.Delay(350, cancellationToken);
            await _supabase.UpdateScheduleAsync(schedule, cancellationToken);
            ConnectionStatus.Content = $"自動保存済み / {_allSchedules.Count}件中 編集中の1件を保存";
        }
        catch (OperationCanceledException)
        {
            // 次の変更による自動保存に置き換えられたため何もしない。
        }
        catch (Exception ex)
        {
            ConnectionStatus.Content = "自動保存エラー";
            Debug.WriteLine($"Auto save failed for schedule {schedule.Id}: {ex}");
        }
        finally
        {
            ctsDisposeIfCurrent(cancellationToken);
        }
    }

    private void ctsDisposeIfCurrent(CancellationToken cancellationToken)
    {
        if (_autoSaveCts?.Token == cancellationToken)
        {
            _autoSaveCts.Dispose();
            _autoSaveCts = null;
        }
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ScheduleDataGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            ScheduleDataGrid.CommitEdit(DataGridEditingUnit.Row, true);
            _autoSaveCts?.Cancel();
            SaveButton.IsEnabled = false;
            ConnectionStatus.Content = "全件保存中…";

            foreach (var schedule in _allSchedules)
                await _supabase.UpdateScheduleAsync(schedule);

            ConnectionStatus.Content = $"全件保存完了 / {_allSchedules.Count}件";
        }
        catch (Exception ex)
        {
            ConnectionStatus.Content = "保存エラー";
            MessageBox.Show(this, ex.Message, "保存エラー", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SaveButton.IsEnabled = true;
        }
    }

    private async void ReloadButton_Click(object sender, RoutedEventArgs e) => await ReloadAsync();

    private void OfficialButton_Click(object sender, RoutedEventArgs e)
        => Process.Start(new ProcessStartInfo(OfficialTimetableUrl) { UseShellExecute = true });

    private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child is not null)
        {
            if (child is T parent)
                return parent;
            child = VisualTreeHelper.GetParent(child);
        }
        return null;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed record Venue(long Id, short VenueNo, string Name, string? Location = null, double Latitude = 0, double Longitude = 0);

public sealed class EventSchedule : INotifyPropertyChanged
{
    public long Id { get; }
    public DateOnly EventDate { get; }
    public TimeOnly StartTime { get; }
    public TimeOnly? EndTime { get; }
    public string EventDateText => EventDate.ToString("yyyy-MM-dd");
    public string StartTimeText => StartTime.ToString("HH:mm");
    public string EndTimeText => EndTime?.ToString("HH:mm") ?? "";
    public string Title { get; }
    public long VenueId { get; }
    public string Description { get; }
    private string _genre;
    private bool _verified;
    public string Genre { get => _genre; set { if (_genre == value) return; _genre = value; OnPropertyChanged(); } }
    public bool Verified { get => _verified; set { if (_verified == value) return; _verified = value; OnPropertyChanged(); } }
    public ObservableCollection<string> Tags { get; } = [];

    public EventSchedule(long id, DateOnly eventDate, TimeOnly startTime, TimeOnly? endTime, string title, long venueId, string description, string genre, bool verified, IEnumerable<string>? tags = null)
    {
        Id = id;
        EventDate = eventDate;
        StartTime = startTime;
        EndTime = endTime;
        Title = title;
        VenueId = venueId;
        Description = description;
        _genre = genre;
        _verified = verified;
        if (tags is not null)
        {
            foreach (var tag in tags.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct())
                Tags.Add(tag);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
