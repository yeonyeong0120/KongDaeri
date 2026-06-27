using System.Windows;
using System.Windows.Controls;
using KongDaeri.ViewModels;
using MessageBox = System.Windows.MessageBox;
using TextBox = System.Windows.Controls.TextBox;
using PasswordBox = System.Windows.Controls.PasswordBox;

namespace KongDaeri;

/// <summary>
/// 설정 창. 시크릿은 PasswordBox 로 입력하고 보기 토글 시 TextBox 와 동기화한다.
/// 저장 성공 시 DialogResult=true (App 이 받아 설정을 다시 로드).
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _vm = new();
    private bool _syncing;

    /// <summary>모든 데이터 삭제 요청(App 이 실제 삭제 수행). 설정은 건드리지 않음.</summary>
    public Func<Task>? ClearAllDataRequested;

    public SettingsWindow()
    {
        InitializeComponent();
        DataContext = _vm;

        // PasswordBox ↔ TextBox 양방향 동기화(보기/숨기기 전환해도 값 유지, 저장은 PasswordBox 에서 읽음).
        GeminiPw.PasswordChanged += (_, _) => Sync(GeminiPw, GeminiTb, fromPw: true);
        GeminiTb.TextChanged += (_, _) => Sync(GeminiPw, GeminiTb, fromPw: false);
        NotionPw.PasswordChanged += (_, _) => Sync(NotionPw, NotionTb, fromPw: true);
        NotionTb.TextChanged += (_, _) => Sync(NotionPw, NotionTb, fromPw: false);
    }

    private void Sync(PasswordBox pw, TextBox tb, bool fromPw)
    {
        if (_syncing) return;
        _syncing = true;
        if (fromPw) tb.Text = pw.Password;
        else pw.Password = tb.Text;
        _syncing = false;
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        var (ok, error) = _vm.Apply(GeminiPw.Password, NotionPw.Password);
        if (!ok)
        {
            MessageBox.Show(error, "설정 저장", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;   // 창 유지
        }
        DialogResult = true;
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private async void OnClearAll(object sender, RoutedEventArgs e)
    {
        var answer = MessageBox.Show(
            "수집함의 모든 항목과 스니핑 이미지가 영구 삭제됩니다. 계속할까요?\n(설정/키는 유지됩니다)",
            "모든 데이터 삭제", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
        if (answer != MessageBoxResult.OK) return;

        if (ClearAllDataRequested is not null)
        {
            await ClearAllDataRequested.Invoke();
        }
        MessageBox.Show("모든 수집 데이터를 삭제했습니다.", "완료",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
