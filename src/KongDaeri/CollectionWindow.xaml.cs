using System.Windows;
using KongDaeri.ViewModels;

namespace KongDaeri;

/// <summary>수집함 창. DataContext 는 App 에서 CollectionViewModel 로 주입한다.</summary>
public partial class CollectionWindow : Window
{
    public CollectionWindow(CollectionViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
