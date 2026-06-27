using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KongDaeri.Core;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;
using MessageBoxResult = System.Windows.MessageBoxResult;

namespace KongDaeri.ViewModels;

/// <summary>수집함 창 뷰모델. 리스트 + 선택 항목 + 노션공유/삭제 액션, 실시간 갱신.</summary>
public partial class CollectionViewModel : ObservableObject
{
    private readonly CaptureService _service;

    public ObservableCollection<CaptureItemViewModel> Items { get; } = new();

    [ObservableProperty]
    private CaptureItemViewModel? selectedItem;

    public CollectionViewModel(CaptureService service)
    {
        _service = service;
        _service.ItemAdded += OnItemAdded;
        _service.ItemUpdated += OnItemUpdated;
        _service.ItemDeleted += OnItemDeleted;
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        var list = await _service.ListAsync();   // storage 가 captured_at DESC (최신 위)
        OnUi(() =>
        {
            Items.Clear();
            foreach (var it in list)
            {
                Items.Add(new CaptureItemViewModel(it));
            }
            SelectedItem ??= Items.Count > 0 ? Items[0] : null;
        });
    }

    private void OnItemAdded(object? sender, CaptureItem item) => OnUi(() =>
    {
        if (Items.Any(v => v.Id == item.Id)) return;
        Items.Insert(0, new CaptureItemViewModel(item));   // 최신 항목이 위로
    });

    private void OnItemUpdated(object? sender, CaptureItem item) => OnUi(() =>
    {
        Items.FirstOrDefault(v => v.Id == item.Id)?.RaiseAllChanged();
    });

    private void OnItemDeleted(object? sender, Guid id) => OnUi(() =>
    {
        var vm = Items.FirstOrDefault(v => v.Id == id);
        if (vm is not null) Items.Remove(vm);
    });

    [RelayCommand]
    private async Task ProcessAiAsync()
    {
        var sel = SelectedItem;
        if (sel is null) return;

        if (!_service.AiEnabled)
        {
            MessageBox.Show("AI가 비활성화됨(Gemini 키 확인). settings.json 을 점검하세요.",
                "AI 정리", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // 성공/실패 모두 서비스가 상태·배지·말풍선을 갱신한다(예외는 내부 처리).
        await _service.ProcessWithAiAsync(sel.Model);
    }

    [RelayCommand]
    private async Task ShareAsync()
    {
        var sel = SelectedItem;
        if (sel is null) return;

        try
        {
            await _service.ExportAsync(sel.Model);
            sel.RaiseAllChanged();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "노션 전송", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        var sel = SelectedItem;
        if (sel is null) return;

        var answer = MessageBox.Show(
            $"이 항목을 삭제할까요?\n\n{sel.Title}",
            "삭제 확인", MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (answer != MessageBoxResult.OK) return;

        await _service.DeleteAsync(sel.Model);
    }

    // 이벤트가 백그라운드에서 와도 UI 스레드에서 컬렉션을 만지도록 보장.
    private static void OnUi(Action action)
    {
        var disp = Application.Current?.Dispatcher;
        if (disp is not null && !disp.CheckAccess()) disp.Invoke(action);
        else action();
    }
}
