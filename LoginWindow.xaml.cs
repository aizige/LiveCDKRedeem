using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI;
using System.Diagnostics;
using WinRT.Interop;
using Windows.System;
using System.Management;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace LiveCDKRedeem
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            this.InitializeComponent();

            ExtendsContentIntoTitleBar = true;
            GetAppWindowAndPresenter();
        }
        public void GetAppWindowAndPresenter()
        {
            int width = 590;
            int height = 890;
            System.IntPtr hWnd = WindowNative.GetWindowHandle(this);
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
            AppWindow appWindow = AppWindow.GetFromWindowId(windowId);

            // 应用系统标题及图标
            appWindow.Title = AppTitleTextBlock.Text;
            appWindow.SetIcon("Assets/logo.ico");
            //Windows.Graphics.SizeInt32 size = appWindow.Size; // 获取窗口大小
            //PointInt32 pointInt = appWindow.Position; // 获取窗口坐标
            // 获取显示器分辨率
            int width_X = DisplayArea.Primary.OuterBounds.Width;
            int height_Y = DisplayArea.Primary.OuterBounds.Height;
            Debug.WriteLine($"--- > 显示器分辨率 X：{width_X} Y: {height_Y}");
            // 设置窗口起来时坐在坐标
            int x = (width_X / 2) - (width / 2);
            int y = (height_Y / 2) - (height / 2);
            appWindow.MoveAndResize(new Windows.Graphics.RectInt32(x, y, width, height));

            Debug.WriteLine($"--- > 设置窗口大小为 X：{appWindow.Size.Width} Y: {appWindow.Size.Height}");
        }

        // 绑定LoginView视图的回车键触发事件(实现按下回车键进行登录)
        private void LoginView_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                Button_Click(sender, e);
            }

        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            string systemId = null;
            using (ManagementObjectSearcher mos = new ManagementObjectSearcher("select * from Win32_ComputerSystemProduct"))
            {
                ManagementObjectCollection managementObjectCollection = mos.Get();
                foreach (var item in managementObjectCollection)
                {
                    
                    Debug.WriteLine($"设备Caption --- > {item["Caption"].ToString()}");
                    Debug.WriteLine($"设备Description --- > {item["Description"].ToString()}");
                    Debug.WriteLine($"设备IdentifyingNumber --- > {item["IdentifyingNumber"].ToString()}");
                    Debug.WriteLine($"设备Name --- > {item["Name"].ToString()}");
                    //Debug.WriteLine($"设备SKUNumber --- > {item["SKUNumber"].ToString()}");
                    Debug.WriteLine($"设备Vendor --- > {item["Vendor"].ToString()}");
                    Debug.WriteLine($"设备Version --- > {item["Version"].ToString()}");
                    Debug.WriteLine($"设备UUID --- > {item["UUID"].ToString()}");
                }

            }

            
        }

    }
}