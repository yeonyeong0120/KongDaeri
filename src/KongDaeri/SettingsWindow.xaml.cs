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
}
