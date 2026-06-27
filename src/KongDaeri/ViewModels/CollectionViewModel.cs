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

    /// <summary>설정 창 열기 요청(App 이 실제 창을 띄움).</summary>
    public Action? OpenSettingsRequested;

    [RelayCommand]
    private void OpenSettings() => OpenSettingsRequested?.Invoke();

    public CollectionViewModel(CaptureService service)
    {
        _service = service;
        _service.ItemAdded += OnItemAdded;
        _service.ItemUpdated += OnItemUpdated;
        _service.ItemDeleted += OnItemDeleted;
        _service.Cleared += (_, _) => OnUi(() => { Items.Clear(); SelectedItem = null; });
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

    /// <summary>번역 버튼 라벨(설정 기본 언어 반영). 예: "번역(English)".</summary>
    public string TranslateButtonText => $"번역({CurrentLanguage})";

    private static string CurrentLanguage
        => string.IsNullOrWhiteSpace(AppSettings.Load().TranslateLanguage) ? "English" : AppSettings.Load().TranslateLanguage!;

    /// <summary>설정 저장 후 호출 — 번역 버튼 라벨 등 설정 기반 표시를 갱신.</summary>
    public void RefreshSettings() => OnPropertyChanged(nameof(TranslateButtonText));

    [RelayCommand]
    private Task OrganizeAiAsync() => RunAiAsync(AiTask.Organize, null, "정리");

    [RelayCommand]
    private Task TranslateAiAsync()
    {
        var lang = CurrentLanguage;
        return RunAiAsync(AiTask.Translate, lang, $"{lang} 번역");
    }

    private async Task RunAiAsync(AiTask task, string? language, string label)
    {
        var sel = SelectedItem;
        if (sel is null) return;

        if (!_service.AiEnabled)
        {
            MessageBox.Show("AI가 비활성화됨(Gemini 키 확인). 설정에서 키를 확인하세요.",
                "AI 처리", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // 성공/실패 모두 서비스가 상태·배지·말풍선을 갱신(예외는 내부 처리).
        await _service.ProcessWithAiAsync(sel.Model, task, language);
        sel.ProcessedAs = label;   // 처리 방식 표시
    }

    [RelayCommand]
    private async Task ShareAsync()
    {
        var sel = SelectedItem;
        if (sel is null) return;

        // 실패는 서비스가 분류된 alert·말풍선으로 처리(원본 예외 메시지 노출 안 함).
        await _service.ExportAsync(sel.Model);
        sel.RaiseAllChanged();
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
