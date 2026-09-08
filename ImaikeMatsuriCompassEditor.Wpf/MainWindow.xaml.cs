using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace ImaikeMatsuriCompassEditor.Wpf;

public partial class MainWindow : Window
{
    private const string OfficialTimetableUrl = "https://www.imaike55.com/%E4%BB%8A%E6%B1%A0%E3%81%BE%E3%81%A4%E3%82%8A2026%E3%82%BF%E3%82%A4%E3%83%A0%E3%83%86%E3%83%BC%E3%83%96%E3%83%AB";
    private readonly SupabaseService _supabase = new();
    private readonly List<EventSchedule> _allSchedules = [];

    public ObservableCollection<Venue> Venues { get; } = [];
    public ObservableCollection<EventSchedule> CurrentSchedules { get; } = [];
    public EventSchedule? SelectedSchedule { get; private set; }
    public ObservableCollection<string> Categories { get; } =
        ["音楽", "ダンス", "大道芸", "演劇", "トーク", "伝統芸能", "紙芝居", "マジック", "その他"];

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
            ConnectionStatus.Content = "Supabase接続中…";
            var venues = await _supabase.GetVenuesAsync();
            var schedules = await _supabase.GetSchedulesAsync();

            Venues.Clear();
            foreach (var venue in venues) Venues.Add(venue);

            _allSchedules.Clear();
            _allSchedules.AddRange(schedules);

            SelectedSchedule = null;
            OnPropertyChanged(nameof(SelectedSchedule));
            ConnectionStatus.Content = $"接続済み / 会場 {Venues.Count} / スケジュール {_allSchedules.Count}件";
            VenueComboBox.SelectedIndex = Venues.Count > 0 ? 0 : -1;
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
            ScheduleHeaderText.Text = "会場を選択してください。";
            ScheduleCountText.Text = "-";
            SetSelectedSchedule(null);
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

        ScheduleHeaderText.Text = $"{venue.Name} — タイムスケジュール";
        ScheduleCountText.Text = CurrentSchedules.Count.ToString();
        SetSelectedSchedule(null);
    }

    private void ScheduleDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SetSelectedSchedule(ScheduleDataGrid.SelectedItem as EventSchedule);
    }

    private void SetSelectedSchedule(EventSchedule? schedule)
    {
        SelectedSchedule = schedule;
        OnPropertyChanged(nameof(SelectedSchedule));
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ScheduleDataGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            ScheduleDataGrid.CommitEdit(DataGridEditingUnit.Row, true);
            SaveButton.IsEnabled = false;
            ConnectionStatus.Content = "保存中…";

            foreach (var schedule in _allSchedules)
                await _supabase.UpdateScheduleAsync(schedule);

            ConnectionStatus.Content = $"保存完了 / {_allSchedules.Count}件";
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
    private string _category;
    private bool _verified;
    public string Category { get => _category; set { if (_category == value) return; _category = value; OnPropertyChanged(); } }
    public bool Verified { get => _verified; set { if (_verified == value) return; _verified = value; OnPropertyChanged(); } }

    public EventSchedule(long id, DateOnly eventDate, TimeOnly startTime, TimeOnly? endTime, string title, long venueId, string description, string category, bool verified)
    {
        Id = id;
        EventDate = eventDate;
        StartTime = startTime;
        EndTime = endTime;
        Title = title;
        VenueId = venueId;
        Description = description;
        _category = category;
        _verified = verified;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
