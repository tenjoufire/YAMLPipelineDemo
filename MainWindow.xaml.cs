using System;
using System.IO;
using System.Configuration;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using OpenCvSharp;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading;
using OpenCvSharp.WpfExtensions;
using OpenCvSharp.Internal;

namespace YAMLPipelineDemo
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : System.Windows.Window
    {
        //デモ用（本番時はシークレットとして扱う）
        static string subscriptionKey = ConfigurationManager.AppSettings["visionkey"];
        static string endpoint = ConfigurationManager.AppSettings["visionendpoint"];

        int WIDTH = 1280;
        int HEIGHT = 720;
        int captureCount = 0;
        static int peopleCount = 0;
        static string resultJsonString = "";

        private string currentDir = Directory.GetCurrentDirectory();
        private string filepath = "";

        //Video映像の取得に利用
        private VideoCapture capture;

        private Mat frame;
        private Mat imageframe;
        private Bitmap bmp;


        ComputerVisionClient client;

        //timer
        DispatcherTimer timer = new DispatcherTimer();
        int intervalSec = 1;

        DispatcherTimer captureTimer = new DispatcherTimer();

        BackgroundWorker videoDrawWorker;


        public MainWindow()
        {
            InitializeComponent();
            videoDrawWorker = new BackgroundWorker();
            videoDrawWorker.WorkerReportsProgress = true;
            videoDrawWorker.DoWork += new DoWorkEventHandler(VideoDrawWork_DoWork);
            videoDrawWorker.ProgressChanged += new ProgressChangedEventHandler(VideoDrawWork_ProgressChanged);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (!Directory.Exists(@$"{currentDir}\img\")) Directory.CreateDirectory(@$"{currentDir}\img\");
            filepath = @$"{currentDir}\img\capture";
            // Create a client
            client = Authenticate(endpoint, subscriptionKey);

            timer.Interval = new TimeSpan(0, 0, intervalSec);
            timer.Tick += new EventHandler(UIUpdate);

            capture = new VideoCapture(0);
            capture.FrameWidth = WIDTH;
            capture.FrameHeight = HEIGHT;
            frame = new Mat(HEIGHT, WIDTH, MatType.CV_8UC3);
            imageframe = new Mat(HEIGHT, WIDTH, MatType.CV_8UC3);
            videoDrawWorker.RunWorkerAsync();
        }

        public void DrawCameraImage()
        {
            //表示用のBitmap作成
            bmp = new Bitmap(frame.Cols, frame.Rows, (int)frame.Step(), System.Drawing.Imaging.PixelFormat.Format24bppRgb, frame.Data);

        }

        public void VideoDrawWork_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker bw = (BackgroundWorker)sender;
            while (!videoDrawWorker.CancellationPending)
            {
                //画像取得
                try
                {
                    capture.Grab();
                    NativeMethods.videoio_VideoCapture_operatorRightShift_Mat(capture.CvPtr, frame.CvPtr);
                    bw.ReportProgress(0);
                }
                catch
                {
                    resultTextBlock.Text = "画像取得に失敗しました";
                }

            }
        }

        public void VideoDrawWork_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            capturedImg.Source = BitmapSourceConverter.ToBitmapSource(frame);


        }

        private void TakePicButton_Click(object sender, RoutedEventArgs e)
        {
            GetCameraImage();
            timer.Start();
        }

        private void GetCameraImage()
        {
            captureCount++;

            frame = new Mat(HEIGHT, WIDTH, MatType.CV_8UC3);
            bmp = new Bitmap(frame.Cols, frame.Rows, (int)frame.Step(), System.Drawing.Imaging.PixelFormat.Format24bppRgb, frame.Data);

            CaptureImageWorker();

            imageframe.SaveImage($"{filepath}{captureCount}.png");
            sentImage.Source = new BitmapImage(new Uri($"{filepath}{captureCount}.png"));

            AnalyzeImageUrl(client, $"{filepath}{captureCount}.png");
            //CameraRelease();
        }

        private void CaptureImageWorker()
        {
            try
            {
                capture.Grab();
                NativeMethods.videoio_VideoCapture_operatorRightShift_Mat(capture.CvPtr, imageframe.CvPtr);
            }
            catch
            {
                resultTextBlock.Text = "画像取得に失敗しました";
            }

        }

        private void CameraRelease()
        {
            capture.Release();
        }

        public void UIUpdate(object sender, EventArgs e)
        {
            resultTextBlock.Text = $"{peopleCount}人 検出されました";
            resultTextBlockJson.Text = resultJsonString;
        }

        #region ComputerVisonMethods
        public static ComputerVisionClient Authenticate(string endpoint, string key)
        {
            ComputerVisionClient client =
              new ComputerVisionClient(new ApiKeyServiceClientCredentials(key))
              { Endpoint = endpoint };
            return client;
        }

        public static async Task AnalyzeImageUrl(ComputerVisionClient client, string imageUrl)
        {
            Console.WriteLine("----------------------------------------------------------");
            Console.WriteLine("ANALYZE IMAGE - URL");
            Console.WriteLine();

            // Creating a list that defines the features to be extracted from the image. 

            List<VisualFeatureTypes?> features = new List<VisualFeatureTypes?>()
            {
                    VisualFeatureTypes.Categories, VisualFeatureTypes.Description,
                    VisualFeatureTypes.Faces, VisualFeatureTypes.ImageType,
                    VisualFeatureTypes.Tags, VisualFeatureTypes.Adult,
                    VisualFeatureTypes.Color, VisualFeatureTypes.Brands,
                    VisualFeatureTypes.Objects
            };

            Console.WriteLine($"Analyzing the image {System.IO.Path.GetFileName(imageUrl)}...");
            Console.WriteLine();
            // Analyze the URL image 
            //ImageAnalysis results = await client.AnalyzeImageAsync(imageUrl, features);
            ImageAnalysis results = await client.AnalyzeImageInStreamAsync(new StreamReader(imageUrl).BaseStream, features);

            // Objects
            peopleCount = 0;
            foreach (var obj in results.Objects)
            {
                if (obj.ObjectProperty.Contains("person")) peopleCount++;
                resultJsonString += $"ObjectProperty:{obj.ObjectProperty}{Environment.NewLine} Confidence:{obj.Confidence}{Environment.NewLine} Rectangle:[x:{obj.Rectangle.X},y:{obj.Rectangle.Y},w:{obj.Rectangle.W},h:{obj.Rectangle.H}]{Environment.NewLine}";
            }
        }

        #endregion

        private void intervalCaptureButton_Click(object sender, RoutedEventArgs e)
        {
            intervalSec = Int32.Parse(intervalTimeText.Text.Trim());
            captureTimer.Interval = new TimeSpan(0, 0, intervalSec);
            captureTimer.Tick += new EventHandler(IntervalCapture);
            captureTimer.Start();
            timer.Start();
        }

        public void IntervalCapture(object sender, EventArgs e)
        {
            GetCameraImage();
        }
    }

    class PeopleCountEvent
    {
        [JsonProperty("PeopleCount")]
        public int PeopleCount { get; set; }
        [JsonProperty("TimeStamp")]
        public string TimeStamp { get; set; }
    }
}
