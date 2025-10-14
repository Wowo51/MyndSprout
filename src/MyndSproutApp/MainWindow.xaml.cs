//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
//Copyright Warren Harding 2025.
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.IO;

namespace MyndSproutApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
            OpenAILLM.LLM.OpenAiKeyPath = @"";
            OpenRouter.LLM.KeyPath = @"";
            OpenRouter.LLM.Initialize();
            if (OpenAILLM.LLM.OpenAiKeyPath == "" && OpenRouter.LLM.KeyPath == "")
            {
                throw new InvalidOperationException("No key path set. It goes just above this. Set the defaults in LLMSwitch too.");
            }
        }

        private void FileButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.ContextMenu != null)
            {
                b.ContextMenu.PlacementTarget = b;
                b.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                b.ContextMenu.IsOpen = true;
            }
        }

        private void Output_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                tb.ScrollToEnd();
            }
        }
    }
}