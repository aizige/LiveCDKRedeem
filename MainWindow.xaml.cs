using LiveCDKRedeem.Bean;
using LiveCDKRedeem.Pages;
using LiveCDKRedeem.ViewModel;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using Windows.ApplicationModel;
using Windows.Storage;
using Windows.Web.Http;
using WinRT.Interop;
using static OpenCvSharp.Stitcher;
using static System.Net.Mime.MediaTypeNames;
using Path = System.IO.Path;


// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace LiveCDKRedeem
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Microsoft.UI.Xaml.Window
    {
        // 请求地址
        private Uri url = new Uri("https://api-foc.krafton.com/redeem/v2/register");
        // 请求Cookie
        private String cookie;
        // 表示正在运行屏幕OCR识别
        private String status;
        // 正在进行网络请求兑换CDK的数量
        public int requestingSum;
        // 已经兑换过的CDK (防止CDK未消失前重复兑换)
        public String OLD_CDK = "x";
        // 
        public bool IsLog = true;

        DispatcherQueue dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        public AccountViewModel accountViewModel { get; set; }
 
        private Process process;
        public MainWindow()
        {
            this.InitializeComponent();
            ExtendsContentIntoTitleBar = true;
            GetAppWindowAndPresenter();
            accountViewModel = new AccountViewModel();

            initInfo();
            /*int width = DisplayArea.Primary.OuterBounds.Width;
            int height = DisplayArea.Primary.OuterBounds.Height;
            int width1 = (int)(30f / 100f * width);
            int height1 = (int)(30f / 100f * height);
            Debug.WriteLine($" --- > {width1} --- {height1}");*/
        }

        private void initInfo() 
        {
            // 初始化版本信息
            PackageVersion version = Package.Current.Id.Version;

            version_textBlock.Text = $"Version: {version.Major}.{version.Minor}.{version.Build} Beta";
            Log($"主屏幕分辨率：{DisplayArea.Primary.OuterBounds.Width} × {DisplayArea.Primary.OuterBounds.Height}");

            Task.Run(() => 
            {
                // 设置Cookie...不设置也可以
                ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
                cookie = localSettings.Values["Cookie"] as string;
                if (string.IsNullOrWhiteSpace(cookie))
                {
                    // 没有Cookie那就生成一个， 原始cookie长度是个10位数的时间戳加9位数的随机数
                    long time = DateTimeOffset.UtcNow.ToUnixTimeSeconds();


                    // 生成一个 9 位数的数字
                    Random random = new Random();
                    cookie = $@"_ym_uid={time}{random.Next(100000000, 999999999)};_ym_d={time}";

                    // 存储起来
                    localSettings.Values["Cookie"] = cookie;
                    Debug.WriteLine($"生成随机Cookie --- > {cookie}");
                }
                InitOCR();
            });
        }
        // 设置窗口大小 应用系统标题及图标
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

        private async void myButton_ClickAsync(object sender, RoutedEventArgs e)
        {
            ContentDialog dialog = new();

            // XamlRoot must be set in the case of a ContentDialog running in a Desktop app
            dialog.XamlRoot = this.Content.XamlRoot;
            //dialog.Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style;
            dialog.Title = "添加用户";
            dialog.Background = new SolidColorBrush((Windows.UI.Color)Microsoft.UI.Xaml.Application.Current.Resources["SystemChromeAltHighColor"]);
            dialog.PrimaryButtonText = "应用";
            dialog.CloseButtonText = "取消";
            dialog.DefaultButton = ContentDialogButton.Close;
            dialog.Content = new ImportDataPage();
            dialog.PrimaryButtonClick += ContentDialog_Primary_Button_Click;

            ContentDialogResult result = await dialog.ShowAsync();
        }
        private void ContentDialog_Primary_Button_Click(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // 当点击Primary_Button的时候确保ContentDialog窗口处于打开状态
            args.Cancel = true;

            ImportDataPage importDataPage = sender.Content as ImportDataPage;
            if (String.IsNullOrWhiteSpace(importDataPage.myTextBox))
            {
                importDataPage.OutputErrorMessage("用户代码不能为空");
            }
            else
            {
                //Debug.WriteLine($"输入的新用户名 ---- > {importDataPage.myTextBox}");
                string myTextBox = importDataPage.myTextBox;
                // 解析数据
                try 
                {
                    

                    Data data = JsonSerializer.Deserialize<Data>(myTextBox);
                    DateTime dateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
                    dateTime = dateTime.AddSeconds(data.expiresAt).ToLocalTime();
                    string formattedDateTime = dateTime.ToString("yyyy-MM-dd HH:mm:ss");

                    AccountData accountData = new AccountData();
                    accountData.accessToken = data.accessToken;
                    accountData.displayName = data.profile.gamelinks.display_name;
                    accountData.expiresAt = formattedDateTime + " 到期";
                    accountViewModel.Add(accountData);
                    
                    // 关闭ContentDialog窗口
                    sender.Hide();
                }
                catch (Exception ex) 
                {
                    Debug.WriteLine($"解析用户Token发生错误 --- >  {ex.Message}");
                    importDataPage.OutputErrorMessage("用户代码格式错误");
                }
                
               
            }
        }

        private void delete_Button_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            AccountData accountData = button.DataContext as AccountData;
            Debug.WriteLine($"删除 --- >  {JsonSerializer.Serialize(accountData)}");
            accountViewModel.Remove(accountData);
        }
        public void Log(String log)
        {
            string result;
            tbLog.Document.GetText((Microsoft.UI.Text.TextGetOptions)Microsoft.UI.Text.TextSetOptions.None, out result);
            if (String.IsNullOrWhiteSpace(result)) { result = ""; }
            tbLog.Document.SetText(Microsoft.UI.Text.TextSetOptions.None, $"{result + log}");
        }

        private void startButton_Click(object sender, RoutedEventArgs e)
        {
            //string[] files = Directory.GetFiles("C:\\Users\\hefan\\Desktop\\PGC", "*.*", SearchOption.TopDirectoryOnly);
            //int s = 1;
            //foreach (string file in files) 
            //{
            //    Mat mat = test(file);
            //    mat.SaveImage($"C:/Users/hefan/Desktop/aa/{s}.png");
            //    s++;
            //}
            //test("");
            if (startButton.Tag.Equals("Stop"))
            {
                if (accountViewModel.Accounts == null || accountViewModel.Accounts.Count < 1) 
                {
                    Log("===先添加账号啊===");
                    return;
                }
                status = "Start";
                startButton.Tag = "Start";
                startButton.Content = "停止运行";
                Run();
            }
            else
            {
                status = "Stop";
                startButton.Tag = "Stop";
                startButton.Content = "启动";
            }

        }

        /*private Mat test(String Path)
        {
            //Bitmap bitmap = new Bitmap("C:\\Users\\hefan\\Desktop\\PGC\\13.png");
            //Path =  "C:\\Users\\hefan\\Desktop\\PGC\\14.png";
            Bitmap bitmap = new Bitmap(Path);
            
            //Mat result1 = mat.Threshold(128, 255, ThresholdTypes.Binary);
            //Cv2.ImShow("endresult1ddd", result1);

            int width = 1920;
            int height = 1080;
            width = (int)(2f * width);
            height = (int)(2f  * height);
            var destRect = new System.Drawing.Rectangle(0, 0, width, height);
            var destImage = new Bitmap(width, height);

            destImage.SetResolution(bitmap.HorizontalResolution, bitmap.VerticalResolution);

            using (var graphics = Graphics.FromImage(destImage))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                using (var wrapMode = new ImageAttributes())
                {
                    wrapMode.SetWrapMode(WrapMode.TileFlipXY);
                    graphics.DrawImage(bitmap, destRect, 0, 0, bitmap.Width, bitmap.Height, GraphicsUnit.Pixel, wrapMode);
                }
            }


            
            Mat mat1 = BitmapConverter.ToMat(bitmap);

            Mat dilation2 = new Mat();

            // 3、进行二值化处理，让图片只有2种颜色
            Mat mat = BitmapConverter.ToMat(destImage);
            Cv2.CvtColor(mat, mat, ColorConversionCodes.BGR2GRAY);

            //3. 膨胀和腐蚀操作的核函数
            Mat element1 = new Mat();
            Mat element2 = new Mat();
            OpenCvSharp.Size size1 = new OpenCvSharp.Size(30, 5);
            OpenCvSharp.Size size2 = new OpenCvSharp.Size(20, 5);

            element1 = Cv2.GetStructuringElement(MorphShapes.Rect, size1);
            element2 = Cv2.GetStructuringElement(MorphShapes.Rect, size2);
            //
            ////4. 膨胀一次，让轮廓突出
            Mat dilation = new Mat();
            Cv2.Dilate(mat, dilation, element1);
            Cv2.Dilate(dilation, dilation, element2);

            //5. 腐蚀一次，去掉细节，如表格线等。注意这里去掉的是竖直的线
            //Mat erosion = new Mat();
            //Cv2.Erode(dilation, erosion, element1);

          
            //6. 再次膨胀，让轮廓明显一些
            Cv2.Dilate(dilation, dilation2, element2, null, 3);

            //腐蚀一次
            //Cv2.Erode(dilation2, dilation2, element1);

            //Cv2.ImShow("endddd", dilation2);

            Mat src = dilation2;
            Mat mask = new Mat();
            Cv2.InRange(src, new Scalar(255, 255, 255), new Scalar(255, 255, 255), mask);
            Mat result = new Mat();
            Cv2.BitwiseAnd(src, src, result, mask);
            //Cv2.ImShow("dhg", result);
            // 6、 查找轮廓
            OpenCvSharp.Point[][] contours;
            Rect biggestContourRect;

            Cv2.FindContours(result, out contours, out HierarchyIndex[] hierarchly, RetrievalModes.External, ContourApproximationModes.ApproxSimple);



            // 7. 轮询所有轮廓点，筛选那些面积小的
            int s = 1;
            StringBuilder sb = new StringBuilder();
            foreach (OpenCvSharp.Point[] contour in contours)
            {

                double area = Cv2.ContourArea(contour);
                //  面积小的都筛选掉
                *//*if (area < 1000)
                {
                    continue;
                }*//*
                //  轮廓近似，作用很小
                double epsilon = 0.001 * Cv2.ArcLength(contour, true);
                //  找到最小的矩形
                biggestContourRect = Cv2.BoundingRect(contour);
                if (biggestContourRect.Width < 200*2 || biggestContourRect.Height < 40*2)
                {
                    continue;
                }
                // 在原图上画出矩形

                biggestContourRect.Height = (int)(biggestContourRect.Height / 2f);
                biggestContourRect.Width = (int)(biggestContourRect.Width / 2f);
                biggestContourRect.X = (int)(biggestContourRect.X / 2f);
                biggestContourRect.Y = (int)(biggestContourRect.Y / 2f);
                mat1.Rectangle(biggestContourRect, Scalar.Green, 2);
                //Debug.WriteLine($"X --- > {biggestContourRect.X}\tY --- > {biggestContourRect.Y}\tSize --- > {biggestContourRect.Width} * {biggestContourRect.Height}");

                // 裁剪出矩形
                *//*Mat mat4 = OpenCVToCut(mat1, biggestContourRect);
                mat4.SaveImage($"C:/Users/hefan/Desktop/aa/{s}.png");
                s++;*//*
                
                // 将每个矩形进行文字识别，然后添加到StringBuilder
                ///string iamegText = await tesseractOCR.OCRAsync(mat4.ToBytes());
                //Debug.WriteLine($"找到文本 --->  {iamegText}");
                ///sb.Append(iamegText);
            }
            //Cv2.ImShow("end", mat1);
            return mat1;
        }*/


        private Mat test(String Path)
        {
            //Bitmap bitmap = new Bitmap("C:\\Users\\hefan\\Desktop\\PGC\\13.png");
            Path =  "C:\\Users\\hefan\\Desktop\\PGC\\12.png";
            Bitmap bitmap = new Bitmap(Path);

            //Mat result1 = mat.Threshold(128, 255, ThresholdTypes.Binary);
            //Cv2.ImShow("endresult1ddd", result1);
            int beishu = 1;

            int width = 1920;
            int height = 1080;
            width = (int)(beishu * width);
            height = (int)(beishu * height);
            var destRect = new System.Drawing.Rectangle(0, 0, width, height);
            var destImage = new Bitmap(width, height);

            destImage.SetResolution(bitmap.HorizontalResolution, bitmap.VerticalResolution);

            using (var graphics = Graphics.FromImage(destImage))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                using (var wrapMode = new ImageAttributes())
                {
                    wrapMode.SetWrapMode(WrapMode.TileFlipXY);
                    graphics.DrawImage(bitmap, destRect, 0, 0, bitmap.Width, bitmap.Height, GraphicsUnit.Pixel, wrapMode);
                }
            }



            Mat mat1 = BitmapConverter.ToMat(bitmap);

            Mat dilation2 = new Mat();

            // 3、进行二值化处理，让图片只有2种颜色
            Mat mat = BitmapConverter.ToMat(destImage);
            Cv2.CvtColor(mat, mat, ColorConversionCodes.BGR2GRAY);
            //Mat result1 = mat.Threshold(131, 255, ThresholdTypes.Binary);

            //3. 膨胀和腐蚀操作的核函数
            Mat element1 = new Mat();
            Mat element2 = new Mat();
            OpenCvSharp.Size size1 = new OpenCvSharp.Size(15, 8);
            OpenCvSharp.Size size2 = new OpenCvSharp.Size(20, 5);

            element1 = Cv2.GetStructuringElement(MorphShapes.Rect, size1);
            element2 = Cv2.GetStructuringElement(MorphShapes.Rect, size2);
            //
            ////4. 膨胀一次，让轮廓突出
            Mat dilation = new Mat();
            Cv2.Dilate(mat, dilation, element1);
            Cv2.GaussianBlur(dilation,dilation2, new OpenCvSharp.Size(3, 3),0,0);

            //Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(20,20));
            //Cv2.Dilate(dilation,dilation, element1);
            //Cv2.Canny(dilation,dilation2,30,10,3);
            //5. 腐蚀一次，去掉细节，如表格线等。注意这里去掉的是竖直的线
            //Mat erosion = new Mat();
            //Cv2.Erode(dilation, erosion, element1);


            //6. 再次膨胀，让轮廓明显一些
            //Cv2.Dilate(dilation, dilation2, element2, null, 3);

            //腐蚀一次
            //Cv2.Erode(dilation2, dilation2, element1);

            //Cv2.ImShow("endddd", dilation2);

            Mat src = dilation2;
            Mat mask = new Mat();
            Cv2.InRange(src, new Scalar(255, 255, 255), new Scalar(255, 255, 255), mask);
            Mat result = new Mat();
            Cv2.BitwiseAnd(src, src, result, mask);
            Cv2.ImShow("dhg", result);
            // 6、 查找轮廓
            OpenCvSharp.Point[][] contours;
            Rect biggestContourRect;

            Cv2.FindContours(result, out contours, out HierarchyIndex[] hierarchly, RetrievalModes.External, ContourApproximationModes.ApproxNone);



            // 7. 轮询所有轮廓点，筛选那些面积小的
            int s = 1;
            StringBuilder sb = new StringBuilder();
            foreach (OpenCvSharp.Point[] contour in contours)
            {

                double area = Cv2.ContourArea(contour);
                //  面积小的都筛选掉
                /*if (area < 1000)
                {
                    continue;
                }*/
                //  轮廓近似，作用很小
                double epsilon = 0.001 * Cv2.ArcLength(contour, true);
                //  找到最小的矩形
                biggestContourRect = Cv2.BoundingRect(contour);
                if (biggestContourRect.Width < 200 * beishu || biggestContourRect.Height < 40 * beishu)
                {
                    continue;
                }
                // 在原图上画出矩形

                biggestContourRect.Height = (int)(biggestContourRect.Height / beishu);
                biggestContourRect.Width = (int)(biggestContourRect.Width / beishu);
                biggestContourRect.X = (int)(biggestContourRect.X / beishu);
                biggestContourRect.Y = (int)(biggestContourRect.Y / beishu);
                mat1.Rectangle(biggestContourRect, Scalar.Green, 2);

            }
            Cv2.ImShow("end", mat1);
            return mat1;
        }
        public  Mat OpenCVToCut(Mat inputMat, OpenCvSharp.Rect TargetRectangle)
        {
            Mat mat = inputMat;
            OpenCvSharp.Rect rect = new OpenCvSharp.Rect(TargetRectangle.X, TargetRectangle.Y, TargetRectangle.Width, TargetRectangle.Height);
            Mat RectMat = new Mat(mat, rect);
            return RectMat;
        }
        /// <summary>
        /// 运行OCR服务
        /// </summary>
        private async void Run()
        {
            await Task.Run(() =>
            {
                while (status.Equals("Start") )
                {
                    //Debug.WriteLine("requestingSum ---------- > " + requestingSum);
                    if (requestingSum != 0) 
                    {
                        continue;
                    }
                    string CDK = null;
                    long startTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                    /*Bitmap bmp = new Bitmap("C:/Users/hefan/Desktop/PGC/kk2.png");
                    MemoryStream ms = new MemoryStream();
                    bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    byte[] bmpBytes = ms.ToArray();
                    string base64String = Convert.ToBase64String(bmpBytes);*/
                    string path = screenshot();
                    string Body = $"{{\"image_path\": \"{path}\"}}{Environment.NewLine}";
                    //Debug.WriteLine("image_path --- > " + Body);
                    process.StandardInput.Write(Body);
                    process.StandardInput.Flush();
                    string jsonString = process.StandardOutput.ReadLine();
                    //Debug.WriteLine("识别到的信息 --- > " + jsonString);
                    if (jsonString == null) 
                    {
                        continue;
                    }
                    JsonDocument jsonDocument = JsonDocument.Parse(jsonString);
                    JsonElement root = jsonDocument.RootElement;
                    if (root.GetProperty("code").GetInt16() != 100) 
                    {
                        // 未识别到文字信息
                        continue;
                    }

                    JsonElement data = root.GetProperty("data");
                    foreach (JsonElement element in data.EnumerateArray())
                    {
                        string text = element.GetProperty("text").GetString();
                        if (text != null && text.Length == 12)
                        {
                            //Debug.WriteLine($" --- > {text} LEN = {text.Length}");
                            if (Regex.IsMatch(text, "^[A-Za-z0-9]+$")) 
                            {
                                CDK = text;
                                Debug.WriteLine($"跳出 --- > length = {text.Length} CDK = {CDK}");
                                break;
                            }
                            
                        }
                    }

                    long endTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();


                    if (CDK != null && !OLD_CDK.Equals(CDK))
                    {
                        // 网络请求
                        long startTime1 = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                        Redeem(CDK);
                        
                        long endTime1 = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                        dispatcherQueue.TryEnqueue(() =>
                        {
                            //Log($"识别到的CDK --- > {CDK}\nCDK识别耗时 --- > {endTime - startTime}ms\nCDK兑换耗时 --- > {endTime1 - startTime1}ms\n总耗时 --- > {endTime1 - startTime}ms");
                            Log($"正在兑换CDK --- > {CDK}\nCDK识别耗时 --- > {endTime - startTime}ms");
                        });
                        Debug.WriteLine($"识别到的CDK --- > {CDK}\nCDK识别耗时 --- > {endTime - startTime}ms\nCDK兑换耗时 --- > {endTime1 - startTime1}ms\n总耗时 --- > {endTime1 - startTime}ms");
                       
                    }
                   
                }
            });
        }

        /// <summary>
        /// 屏幕截图
        /// </summary>
        /// <returns>截图存储路径</returns>
        private String screenshot()
        {
            string screenshotFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Screenshots", "LiveCDKRedeem.png").Replace("\\","/");
            
            if (!Directory.Exists(screenshotFolder)) 
            {
                Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Screenshots"));
            }

            int width = DisplayArea.Primary.OuterBounds.Width;
            int height = DisplayArea.Primary.OuterBounds.Height;
            int X = 0;
            int Y = 0;
            // 如果是2560 * 1080分辨率，那么只截取1920 * 1080 大小，因为直播画面也是1920 * 1080
            // 其它分辨率的显示器我没有
            if (width == 2560 && height == 1080)
            {
                // 屏幕宽度减去1920得出多余的宽度
                int extraWidth = width - 1920;
                // 画面是居中的所有还需在多余的宽度上除2
                X = extraWidth / 2;
                width = width - extraWidth;
            }
            
            
            System.Drawing.Rectangle rectangle = new System.Drawing.Rectangle(X, Y, width,height); // 起始位置YX = 0,截取大小为屏幕大小
            Bitmap bit = new Bitmap(width, height); //Bitmap 的大小为截图大小
            Graphics g = Graphics.FromImage(bit);
            g.CopyFromScreen(rectangle.Location, System.Drawing.Point.Empty, bit.Size);

            //// 修改图片大小
            width = (int)((100f / 100f) * width);
            height = (int)((100f / 100f) * height);
            var destRect = new System.Drawing.Rectangle(0, 0, width, height);
            var destImage = new Bitmap(width, height);

            destImage.SetResolution(bit.HorizontalResolution, bit.VerticalResolution);

            using (var graphics = Graphics.FromImage(destImage))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                using (var wrapMode = new ImageAttributes())
                {
                    wrapMode.SetWrapMode(WrapMode.TileFlipXY);
                    graphics.DrawImage(bit, destRect, 0, 0, bit.Width, bit.Height, GraphicsUnit.Pixel, wrapMode);
                }
            }


            destImage.Save(screenshotFolder, System.Drawing.Imaging.ImageFormat.Png);
            return screenshotFolder;
        }

        private async void SaveErrorInfo(String CDK)
        {
            if (IsLog)
            {
                IsLog = false;
                string sourceFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Screenshots", "LiveCDKRedeem.png");
                string destinationFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Screenshots", "Error");
                StorageFile sourceStorageFile = await StorageFile.GetFileFromPathAsync(sourceFile);
                // StorageFolder destinationStorageFolder = await StorageFolder.GetFolderFromPathAsync(destinationFolder);
                if (!Directory.Exists(destinationFolder))
                {
                    Directory.CreateDirectory(destinationFolder);
                }
                /* if (destinationStorageFolder == null) 
                 {
                     destinationStorageFolder = await StorageFolder.GetFolderFromPathAsync(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Screenshots"));
                     await destinationStorageFolder.CreateFolderAsync(destinationFolder);
                 }*/
                string imageName = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + ".png";
                StorageFile storageFile = await sourceStorageFile.CopyAsync(await StorageFolder.GetFolderFromPathAsync(destinationFolder), imageName);
                
                string path = Path.Combine(destinationFolder, "error.log");
                await File.AppendAllTextAsync(path, $"CDK: {CDK}，识别错误！图片地址:{storageFile.Path}\n");
                IsLog = true;
            }
        }
    

        /// <summary>
        /// 初始化OCR
        /// </summary>
        private async void InitOCR()
        {
            StorageFile file = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/OCR/OCRservice.exe"));
            //StorageFile file1 = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/OCR/OCRservice.exe"));
            StorageFolder storageFolder = await file.GetParentAsync();
            Debug.WriteLine($"path --- > {file.Path}");
            Debug.WriteLine($"path --- > {storageFolder.Path}");
            process = new Process();
            //process.StartInfo.FileName = "C:\\Users\\hefan\\Downloads\\PaddleOCR-json_v.1.3.1\\XXXXX.exe";
            process.StartInfo.FileName = file.Path;
            process.StartInfo.WorkingDirectory = storageFolder.Path;
            //process.StartInfo.Arguments = "-limit_side_len=2880 -cpu_threads=16 -config_path=\"models/config_chinese.txt\"";
            //process.StartInfo.Arguments = "-limit_side_len=960 ";
            //process.StartInfo.WorkingDirectory = "C:\\Users\\hefan\\Downloads\\PaddleOCR-json_v.1.3.1";
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardInput = true;
            // 隐藏CMD窗口
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;

            process.Start();

            while (true)
            {
                string StartInfo = process.StandardOutput.ReadLine();
                if (StartInfo != null && StartInfo.Equals("OCR init completed."))
                {
                    dispatcherQueue.TryEnqueue(() =>
                    {
                        Log("初始化成功...");
                    });
                        
                    Debug.WriteLine($"启动管道进程成功 --- > {StartInfo}");
                    break;
                }
                dispatcherQueue.TryEnqueue(() =>
                {
                    Log("正在初始化...");
                });
                Debug.WriteLine($"正在启动管道进程 --- > {StartInfo}");
            }
        }
        /// <summary>
        /// 发起兑换CDK的请求
        /// </summary>
        /// <param name="redeemCode">要兑换的CDK</param>
        private void Redeem(String redeemCode)
        {
           
            ObservableCollection<AccountData> accounts = accountViewModel.Accounts;
            if (accounts != null && accounts.Count > 0)
            {
                OLD_CDK = redeemCode;
                requestingSum = accounts.Count;
                foreach (var item in accounts)
                {

                   //Redeem(item.displayName, item.accessToken, redeemCode);
                   //Debug.WriteLine($"执行完 --- > {ErrorLogFileName}");
                    
                }
                Debug.WriteLine($"正在兑换CDK ---> {redeemCode}");
            }
            
        }
        
        /// <summary>
        /// 释放资源，关闭PaddleOCR-json进程
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void Window_Closed(object sender, WindowEventArgs args)
        {
            if(process != null) 
            {
                process.StandardInput.Write("exit\n");
            }
            
        }




        /// <summary>
        /// 发起请求兑换直播CDK
        /// </summary>
        /// <param name="nickName">游戏内的昵称</param>
        /// <param name="token">pubg网站上的账户Token</param>
        /// <returns></returns>
        /// 兑换成功	{"_embedded": {"result": {"code": 0}},"_links": {"self": {"href": "http://api-foc.krafton.com/redeem/v2/register"}}}
        /// Token过期失效	{"code": "UNAUTHENTICATED_CONSOLE","message": null,"_embedded": {"result": {"code": 1000}},"_links": {"self": {"href": "http://api-foc.krafton.com/redeem/v2/register"}}}
        /// 一次性CDK码已被使用	{"code": "ALREADY_ACTIVATED","message": "이미 사용된 코드 : S06124-Z4QF-SZR4-3NSVI","_embedded": {"result": {"code": 500}},"_links": {"self": {"href": "http://api-foc.krafton.com/redeem/v2/register"}}}
        /// 赛事先到先得CDK已经被使用完	{"code": "LIMIT_OVER","message": "선착순 코드의 선착순 수량 소진 : W574795WNUEN","_embedded": {"result": {"code": 500}},"_links": {"self": {"href": "http://api-foc.krafton.com/redeem/v2/register"}}}
        /// 错误的CDK	{"code": "INVALID_CODE","message": "존재하지 않는 코드 : W57479DWNUEN","_embedded": {"result": {"code": 500}},"_links": {"self": {"href": "http://api-foc.krafton.com/redeem/v2/register"}}}
        /// 额外的返回Json {"code":"BAD_REQUEST","message":{"redeemCode":{"error":"REDEEMCODE_ERROR","message":"코드의 형태가 올바르지 않습니다."}},"_embedded":{"result":{"code":400}},"_links":{"self":{"href":"http://api-foc.krafton.com/redeem/v2/register"}}}
        public async void Redeem(String nickName, String token, String redeemCode)
        {
            HttpClient httpClient = Builder();
            String requestBody = $@"{{""lang"": ""zh-cn"",""namespace"": ""PUBG_OFFICIAL"",""platformType"": ""STEAM"",""nickName"": ""{nickName}"",""redeemCode"": ""{redeemCode}""}}";
            Debug.WriteLine($"requestBody --- > {requestBody}");
            HttpStringContent httpStringContent = new HttpStringContent(requestBody, Windows.Storage.Streams.UnicodeEncoding.Utf8, "application/json");
            httpClient.DefaultRequestHeaders.Add("Authorization", @$"Bearer {token}");
            httpClient.DefaultRequestHeaders.Add("Cookie", cookie);

            /*foreach (var item in httpClient.DefaultRequestHeaders)
            {
                Debug.WriteLine($"Header --- > {item.Key} = {item.Value}");
            }*/
            String log = "";
            try
            {
                HttpResponseMessage httpResponse = await httpClient.PostAsync(url, httpStringContent);
                Debug.WriteLine($"HttpStatusCode --- > {httpResponse.StatusCode} {Convert.ToInt32(httpResponse.StatusCode)}");
                string responseBody = await httpResponse.Content.ReadAsStringAsync();
                Debug.WriteLine($"responseBody --- > {responseBody}");
                
                if (httpResponse.StatusCode.Equals(Windows.Web.Http.HttpStatusCode.Ok))
                {
                    ResponseResult responseResult = JsonSerializer.Deserialize<ResponseResult>(responseBody);
                    if (responseResult.embeddes.result.code == 0)
                    {
                        log = $"兑换成功 --- > 账号: {nickName}， CDK: {redeemCode}";

                    }
                    else
                    {
                        log = $"兑换失败,未知错误 --- > 账号: {nickName}， CDK: {redeemCode} 错误信息: {responseBody}";
                    }
                    return;
                }

                if (httpResponse.StatusCode.Equals(Windows.Web.Http.HttpStatusCode.BadRequest))
                {
                    JsonDocument document = JsonDocument.Parse(responseBody);
                    
                    string code = document.RootElement.GetProperty("code").GetString();
                    if (code != null && code.Equals("LIMIT_OVER"))
                    {
                        log = $"手慢了已经被抢完了 --- > 账号: {nickName}， CDK: {redeemCode}";

                    }
                    else if (code != null && code.Equals("INVALID_CODE"))
                    {
                        // 这里存储图片
                        SaveErrorInfo(redeemCode);
                        log = $"CDK错误 --- > 账号: {nickName}， CDK: {redeemCode}";
                    }
                    else if (code != null && code.Equals("BAD_REQUEST"))
                    {
                        JsonElement jsonElement = document.RootElement.GetProperty("message").GetProperty("redeemCode").GetProperty("error");
                        string error = jsonElement.GetString();
                        if (error != null && error.Equals("REDEEMCODE_ERROR")) 
                        {
                            // 这里存储图片
                            SaveErrorInfo(redeemCode);
                            log = $"CDK错误 --- > 账号: {nickName}， CDK: {redeemCode}";
                        }
                       
                    }
                    else if (code != null && code.Equals("ALREADY_ACTIVATED"))
                    {

                        log = $"此一次性CDK已经被使用 --- > 账号: {nickName}， CDK:  {redeemCode}";
                    }
                    else if (code != null && code.Equals("UNAUTHENTICATED_CONSOLE"))
                    {

                        log = $"兑换失败！登录已经失效 --- > 账号: {nickName}， CDK: {redeemCode}";
                    }
                    else if (code != null && code.Equals("EXPIRED"))
                    {

                        log = $"CDK已过期 --- > 账号: {nickName}， CDK:  {redeemCode}";
                    }
                    else
                    {
                        log = $"兑换失败 --- > 账号: {nickName}， CDK: {redeemCode}， 错误信息: {responseBody}";
                    }

                }

            }
            catch (Exception ex)
            {
                Debug.WriteLine($"兑换CDK出现错误！请检查网络！账号: {nickName} CDK: {redeemCode} Error信息 --- > {ex.Message}");
                log = $"兑换CDK出现错误！请检查网络！账号: {nickName} CDK: {redeemCode} Error信息 --- > {ex.Message}";
                //log = $"CDK错误 --- > 账号: {nickName}， CDK: {redeemCode}";
            }
            finally
            {
                requestingSum--;
                dispatcherQueue.TryEnqueue(() =>
                {
                    Log(log);
                });
            }

        }

        /// <summary>
        /// 构建HttpClient对象
        /// </summary>
        /// <returns></returns>
        public HttpClient Builder()
        {
            HttpClient httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("Accept", "application/json, text/plain, */*");
            httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
            httpClient.DefaultRequestHeaders.Add("Accept-Language", "zh-CN,zh;q=0.9,en-US;q=0.8,en;q=0.7");
            httpClient.DefaultRequestHeaders.Add("Origin", "https://pubg.com");
            httpClient.DefaultRequestHeaders.Add("Referer", "https://pubg.com/");
            httpClient.DefaultRequestHeaders.Add("Sec-Ch-Ua", "\"Google Chrome\";ErrorLogFileName=\"119\", \"Chromium\";ErrorLogFileName=\"119\", \"Not?A_Brand\";ErrorLogFileName=\"24\"");
            httpClient.DefaultRequestHeaders.Add("Sec-Ch-Ua-Mobile", "?0");
            httpClient.DefaultRequestHeaders.Add("Sec-Ch-Ua-Platform", "\"Windows\"");
            httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "empty");
            httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "cors");
            httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Site", "cross-site");
            httpClient.DefaultRequestHeaders.Add("Service-Lang", "zh-cn");
            httpClient.DefaultRequestHeaders.Add("Service-Namespace", "PUBG_OFFICIAL");
            httpClient.DefaultRequestHeaders.Add("Service-Url", "https://pubg.com/zh-cn/events/redeem");
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/119.0.0.0 Safari/537.36");
            return httpClient;
        }






        
        public void FindTextRegion()
        {
            Bitmap bitmap = new Bitmap("C:\\Users\\hefan\\Desktop\\PGC\\2.png");
            Mat mat1 = BitmapConverter.ToMat(bitmap);

            Mat mat = BitmapConverter.ToMat(bitmap);
            Cv2.CvtColor(mat, mat, ColorConversionCodes.BGR2GRAY);
            Mat result1 = mat.Threshold(0, 255, ThresholdTypes.Otsu);

            // 1. 查找轮廓
            OpenCvSharp.Point[][] contours;
            HierarchyIndex[] hierarchly;
            Rect biggestContourRect = new Rect();

            Cv2.FindContours(mat, out contours, out hierarchly, RetrievalModes.Tree, ContourApproximationModes.ApproxSimple);

            // 2. 筛选那些面积小的
            int i = 0;
            foreach (OpenCvSharp.Point[] contour in contours)
            {
                double area = Cv2.ContourArea(contour);

                //面积小的都筛选掉
                if (area < 1000)
                {
                    continue;
                }

                //轮廓近似，作用很小
                double epsilon = 0.001 * Cv2.ArcLength(contour, true);

                //找到最小的矩形
                biggestContourRect = Cv2.BoundingRect(contour);

                if (biggestContourRect.Height > (biggestContourRect.Width * 1.2))
                {
                    continue;
                }
                //画线
                mat1.Rectangle(biggestContourRect, new Scalar(0, 255, 0), 2);
            }
            Cv2.ImShow("SSS1", mat1);
        }

        public void Preprocess()
        {
            string imgPath = "C:\\Users\\hefan\\Desktop\\PGC\\2.png";
            Mat dilation2 = new Mat();
            //读取灰度图
            using (Mat src = new Mat(imgPath, ImreadModes.Grayscale))
            {
                //1.Sobel算子，x方向求梯度
                Mat sobel = new Mat();
                Cv2.Sobel(src, sobel, MatType.CV_8U, 1, 0, 3);

                //2.二值化
                Mat binary = new Mat();
                Cv2.Threshold(sobel, binary, 0, 255, ThresholdTypes.Otsu | ThresholdTypes.Binary);

                //3. 膨胀和腐蚀操作的核函数
                Mat element1 = new Mat();
                Mat element2 = new Mat();
                OpenCvSharp.Size size1 = new OpenCvSharp.Size(30, 9);
                OpenCvSharp.Size size2 = new OpenCvSharp.Size(24, 6);

                element1 = Cv2.GetStructuringElement(MorphShapes.Rect, size1);
                element2 = Cv2.GetStructuringElement(MorphShapes.Rect, size2);

                //4. 膨胀一次，让轮廓突出
                Mat dilation = new Mat();
                Cv2.Dilate(binary, dilation, element2);

                //5. 腐蚀一次，去掉细节，如表格线等。注意这里去掉的是竖直的线
                Mat erosion = new Mat();
                Cv2.Erode(dilation, erosion, element1);

                //6. 再次膨胀，让轮廓明显一些
                Cv2.Dilate(erosion, dilation2, element2, null, 3);
            }
            Cv2.ImShow("SSS2", dilation2);
        }
    }
}
