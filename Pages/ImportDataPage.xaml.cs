using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace LiveCDKRedeem.Pages
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ImportDataPage : Page
    {
        public String myTextBox;
        public ImportDataPage()
        {
            this.InitializeComponent();
        }

        private void textBox_LostFocus(object sender, RoutedEventArgs e)
        {
            myTextBox = textBox.Text;
        }
        public async void OutputErrorMessage(String message, bool autoClose = false)
        {

            errorMessageTextBlock.Text = message;
            errorMessageTextBlock.Visibility = Visibility.Visible;
            if (autoClose)
            {
                await Task.Delay(5000);
                errorMessageTextBlock.Visibility = Visibility.Collapsed;
            }

        }
    }
}
