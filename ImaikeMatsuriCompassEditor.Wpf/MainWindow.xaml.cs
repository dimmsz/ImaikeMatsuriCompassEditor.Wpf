using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace ImaikeMatsuriCompassEditor.Wpf;

public partial class MainWindow : Window
{
    public ObservableCollection<Venue> Venues { get; } = new();
    public ObservableCollection<EventSchedule> CurrentSchedules { get; } = new();

    private readonly List<EventSchedule> _allSchedules = new()
    {
        new(1, new DateOnly(2026, 9, 20), new TimeOnly(10, 0), null, "サンプルイベント1", 1),
        new(2, new DateOnly(2026, 9, 20), new TimeOnly(11, 0), null, "サンプルイベント2", 1),
        new(3, new DateOnly(2026, 9, 20), new TimeOnly(12, 0), null, "サンプルイベント3", 2),
        new(4, new DateOnly(2026, 9, 21), new TimeOnly(10, 30), null, "サンプルイベント4", 2)
    };

    public MainWindow()
    {
        InitializeComponent();

        Venues.Add(new Venue(1, "今池ガスホール"));
        Venues.Add(new Venue(2, "ストリートコーナーパラダイス"));
        Venues.Add(new Venue(3, "東南会場"));
        Venues.Add(new Venue(4, "一本裏会場"));
        Venues.Add(new Venue(5, "十六広場"));
        Venues.Add(new Venue(6, "西南会場"));
        Venues.Add(new Venue(7, "Imaike Park会場"));
        Venues.Add(new Venue(8, "ノースアイランド"));
        Venues.Add(new Venue(9, "4丁目Pit"));
        Venues.Add(new Venue(10, "下町ネバーランド"));

        VenueListBox.ItemsSource = Venues;
        ScheduleDataGrid.ItemsSource = CurrentSchedules;
        ConnectionStatus.Content = "ローカルデータ表示中";
    }

    private void VenueListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (VenueListBox.SelectedItem is not Venue venue)
        {
            CurrentSchedules.Clear();
            ScheduleHeaderText.Text = "会場を選択してください。";
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

        ScheduleHeaderText.Text = $"{venue.Name} — {CurrentSchedules.Count}件";
    }
}

public sealed record Venue(long Id, string Name);

public sealed class EventSchedule : INotifyPropertyChanged
{
    public long Id { get; }
    public DateOnly EventDate { get; }
    public TimeOnly StartTime { get; }
    public TimeOnly? EndTime { get; }
    public string Title { get; }
    public long VenueId { get; }

    private string _category = string.Empty;
    private bool _verified;

    public string Category
    {
        get => _category;
        set
        {
            if (_category == value) return;
            _category = value;
            OnPropertyChanged();
        }
    }

    public bool Verified
    {
        get => _verified;
        set
        {
            if (_verified == value) return;
            _verified = value;
            OnPropertyChanged();
        }
    }

    public EventSchedule(long id, DateOnly eventDate, TimeOnly startTime, TimeOnly? endTime, string title, long venueId)
    {
        Id = id;
        EventDate = eventDate;
        StartTime = startTime;
        EndTime = endTime;
        Title = title;
        VenueId = venueId;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
