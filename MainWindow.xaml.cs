using Microsoft.Win32;
using NAudio.Dsp;
using NAudio.Extras;
using NAudio.Wave;
using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Security.Policy;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using WPFSong.Class;
using WPFSong.Controls;
using Application = System.Windows.Application;
using Point = System.Windows.Point;

namespace WPFSong
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        #region 可视化可用字段
        WasapiLoopbackCapture cap;//创建音频循环
        EqualizerExample equalizer;
        WaveOutEvent waveOut;
        AudioPlayer player;
        bool IsSlider = false;//是否在使用滑块
        #endregion
        #region 可视化可用方法
        private void VideoStart()
        {
            cap.DataAvailable += (sender, e) =>      // 录制数据可用时触发此事件, 参数中包含音频数据
            {
                // 假设你从 e.Buffer 中提取了音频数据（如音量值）
                // 通过 Dispatcher 更新 UI
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    //第一步
                    spectrumCanvas.Children.Clear();
                    //第二步
                    float[] allSamples = Enumerable      // 提取数据中的采样
                    .Range(0, e.BytesRecorded / 4)   // 除以四是因为, 缓冲区内每 4 个字节构成一个浮点数, 一个浮点数是一个采样
                    .Select(i => BitConverter.ToSingle(e.Buffer, i * 4))  // 转换为 float
                    .ToArray();    // 转换为数组
                                   // 获取采样后, 在这里进行详细处理
                                   //第三步
                    int channelCount = cap.WaveFormat.Channels;   // WasapiLoopbackCapture 的 WaveFormat 指定了当前声音的波形格式, 其中包含就通道数
                    float[][] channelSamples = Enumerable
                        .Range(0, channelCount)
                        .Select(channel => Enumerable
                            .Range(0, allSamples.Length / channelCount)
                            .Select(i => allSamples[channel + i * channelCount])
                            .ToArray())
                        .ToArray();
                    //第四步
                    float[] averageSamples = Enumerable
                    .Range(0, allSamples.Length / channelCount)
                    .Select(index => Enumerable
                    .Range(0, channelCount)
                    .Select(channel => channelSamples[channel][index])
                    .Average())
                    .ToArray();
                    //第五步
                    float log = (float)Math.Ceiling(Math.Log(averageSamples.Length, 2));   // 取对数并向上取整
                    int newLen = (int)Math.Pow(2, log);                             // 计算新长度
                    float[] filledSamples = new float[newLen];
                    Array.Copy(averageSamples, filledSamples, averageSamples.Length);   // 拷贝到新数组
                    Complex[] complexSrc = filledSamples
                        .Select(v => new Complex() { X = v })        // 将采样转换为复数
                        .ToArray();
                    FastFourierTransform.FFT(false, (int)log, complexSrc);   // 进行傅里叶变换
                    //第六步
                    Complex[] halfData = complexSrc
    .Take(complexSrc.Length / 2)
    .ToArray();    // 一半的数据
                    double[] dftData = halfData
                        .Select(v => Math.Sqrt(v.X * v.X + v.Y * v.Y))  // 取复数的模
                        .ToArray();    // 将复数结果转换为我们所需要的频率幅度
                    //第七步
                    Point[] points = dftData
    .Select((v, i) => new Point(i, (float)(spectrumCanvas.ActualHeight - v)))
    .ToArray();
                    //第八步
                    Polyline polyline = new Polyline
                    {
                        Stroke = System.Windows.Media.Brushes.AliceBlue,
                        StrokeThickness = 2,
                        Points = new PointCollection(points)
                    };
                    spectrumCanvas.Children.Add(polyline);


                    if(IsSlider == false)
                    {
                        //进度条
                        Slider.Value = (double)(10 * ((double)player.audioFile.Position / (double)player.audioFile.Length));
                    }
                }));
            };


        }
        private void OpenFile(object sender, RoutedEventArgs e)
        {
            string tempurl = GetUrl();
            richLog.AppendText("路径获取" + "\r\n");
            if(tempurl != "error")
            {
                richLog.AppendText("设备载入中..." + "\r\n");

                VideoDispose();
                richLog.AppendText("前设备关闭中..." + "\r\n");
   
                player = new AudioPlayer(tempurl);
                //// 初始化均衡器
                richLog.AppendText("设备模块载入..." + "\r\n");

                equalizer = new EqualizerExample();
                equalizer.ApplyEqualizer(player.GetSampleProvider());
                richLog.AppendText("均衡器模块载入..." + "\r\n");

                waveOut = new WaveOutEvent();
                richLog.AppendText("播放模块载入..." + "\r\n");

                cap = new WasapiLoopbackCapture();
                richLog.AppendText("声音采集模块载入..." + "\r\n");

                VideoStart();
                waveOut.Init(equalizer.equalizer);
                waveOut.Play();
                cap.StartRecording();
                richLog.AppendText("渲染开始" + "\r\n");

                ModifyAllTextBoxes(sliderPanel);


            }
            if(tempurl == "error")
            {
                richLog.AppendText("获取失败" + "\r\n");
            }
        }
        private string GetUrl()//获取路径
        {
            // 创建打开文件对话框
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
            string filePath = "error";

            // 设置文件类型过滤器（只显示MP3文件）
            openFileDialog.Filter = "MP3 files (*.mp3)|*.mp3|All files (*.*)|*.*";
            openFileDialog.FilterIndex = 1; // 默认选择第一个过滤器
            openFileDialog.InitialDirectory = @"C:\";
            if (openFileDialog.ShowDialog() == true)
            {
                // 获取选中文件的路径
                filePath = openFileDialog.FileName;
            }
            return filePath;
        }
        private void VideoDispose()
        {


            if (player != null)
            {
                player.Stop();
                player.Dispose();
            }

            if (equalizer != null)
            {
                equalizer = null;
            }

            if (waveOut != null)
            {
                waveOut.Stop();
                waveOut.Dispose();
            }
            if (cap != null)
            {
                cap.Dispose();
            }
        }

        private void Player(object sender, RoutedEventArgs e)
        {
            waveOut.Init(equalizer.equalizer);
            waveOut.Play();
            cap.StartRecording();
        }
        public void ModifyAllTextBoxes(Grid parent)//全员恢复
        {
            if (parent == null) return;

            int childrenCount = VisualTreeHelper.GetChildrenCount(parent);

            for (int i = 0; i < childrenCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is SlideValue slider)
                {
                    slider.ChangePro();
                }
            }
            this.Slider.Value = 0;
        }
        #endregion
        #region 滑块方法
        private void Slider_DragStarted(object sender, DragStartedEventArgs e)
        {
            IsSlider = true;
            richLog.AppendText("用户开始拖动" + "\r\n");
        }

        private void Slider_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            IsSlider = false;
            richLog.AppendText("用户结束拖动" + "\r\n");
            // 执行音频跳转
            player.audioFile.Position = (long)((double)player.audioFile.Length*((double)Slider.Value/(double)10));
        }

        private void Slider_DragDelta(object sender, DragDeltaEventArgs e)
        {

        }
        #endregion
        public MainWindow()
        {
            InitializeComponent();
        }
        private void Winfrom_Closed(object sender, EventArgs e)
        {
            base.OnClosed(e);
            VideoDispose();
            this.Close();
        }
        #region 滑块
        private void btn31_SliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var temp = (float)(e.NewValue-e.OldValue);
            UpdateBands(0, temp);
        }

        private void btn63_SliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var temp = (float)(e.NewValue - e.OldValue);
            UpdateBands(1, temp);
        }

        private void btn125_SliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var temp = (float)(e.NewValue - e.OldValue);
            UpdateBands(2, temp);
        }

        private void btn250_SliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var temp = (float)(e.NewValue - e.OldValue);
            UpdateBands(3, temp);
        }

        private void btn500_SliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var temp = (float)(e.NewValue - e.OldValue);
            UpdateBands(4, temp);
        }

        private void btn1k_SliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var temp = (float)(e.NewValue - e.OldValue);
            UpdateBands(5, temp);
        }

        private void btn2k_SliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var temp = (float)(e.NewValue - e.OldValue);
            UpdateBands(6, temp);
        }

        private void btn4k_SliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var temp = (float)(e.NewValue - e.OldValue);
            UpdateBands(7, temp);
        }

        private void btn8k_SliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var temp = (float)(e.NewValue - e.OldValue);
            UpdateBands(8, temp);
        }

        private void btn16k_SliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var temp = (float)(e.NewValue - e.OldValue);
            UpdateBands(9, temp);
        }
        #endregion
        private void UpdateBands(int index, float num)
        {
            if (equalizer != null)
            {
                float temp = num*3;
                equalizer.UpdateBandGain(index, temp);
            }
        }
        //private double mathpro(double value)
        //{
        //    return -22.5 + value * 45;
        //}
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            //var tenp = equalizer.two();
            //foreach (var band in tenp)
            //{
            //    richLog.AppendText(band.Gain.ToString() + "\r\n");
            //}
            ModifyAllTextBoxes(sliderPanel);
        }

        private void textchanged(object sender, TextChangedEventArgs e)
        {
            richLog.ScrollToEnd();
        }

        private void bofang(object sender, RoutedEventArgs e)
        {
            if (waveOut != null)
            {
                waveOut.Play();
            }
        }

        private void zanting(object sender, RoutedEventArgs e)
        {
            if (waveOut != null)
            {
                waveOut.Stop();
            }
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)//导出
        {
            if (equalizer != null)
            {
                using (var folderBrowserDialog = new FolderBrowserDialog())
                {
                    folderBrowserDialog.Description = "请选择一个目录作为路径：";
                    folderBrowserDialog.ShowNewFolderButton = true;
                    folderBrowserDialog.RootFolder = Environment.SpecialFolder.Desktop;
                    folderBrowserDialog.ShowDialog();
                    string url = folderBrowserDialog.SelectedPath;
                    if (Directory.Exists(url))
                    {
                        // 2. 选择合适的比特率（例如128kbps）
                        int bitrate = 128000;

                        // 3. 使用MediaFoundationEncoder进行编码
                        //    先将ISampleProvider转换为IWaveProvider
                        var waveProvider = equalizer.equalizer.ToWaveProvider();
                        MediaFoundationEncoder.EncodeToMp3(waveProvider, url, bitrate);
                    }
                    else
                    {
                        richLog.AppendText("导出url非法");
                    }
                }
            }
        }
    }
}