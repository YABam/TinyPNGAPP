using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.IO;
using TinifyAPI;
using System.Net;
using System.Threading;

namespace TinyPNGAPP
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private string _currentKey;
        private string _currentPath;
        private string _outputPath;
        private int _newWidth;
        private int _totalImage;
        private bool _needToScale = false;
        private int _threadCount = 1;

        object _queueLocker = new object();
        object _failedLocker = new object();
        List<ManualResetEvent> manualResetEvents = new List<ManualResetEvent>();

        private Thread[] _workThreads;

        private Queue<string> targetFiles;
        private List<string> failedList;

        private Action progressBarDelegate;
        private Action progressMsgDelegate;
        public MainWindow()
        {
            InitializeComponent();
        }
        /// <summary>
        /// 关闭时保存Key
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Closed(object sender, EventArgs e)
        {
            string temp = _currentKey;
            try
            {
                DataSaver.SaveSettingData<string>(temp);
            }
            catch
            {
                System.Windows.MessageBox.Show("Key保存失败。");
            }
        }

        /// <summary>
        /// 加载时读取Key
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Initialized(object sender, EventArgs e)
        {
            try
            {
                DataSaver.LoadSettingData<string>(out _currentKey);
                tbKey.Text = _currentKey;
            }
            catch
            {
                System.Windows.MessageBox.Show("Key读取失败，可能没有保存的Key");
            }

            targetFiles = new Queue<string>();
            failedList = new List<string>();
            //定义跨线程委托
            progressBarDelegate = () =>
            {
                pbProgress.Value++;
            };
            progressMsgDelegate = () =>
            {
                msgProgress.Content = "正在压缩/下载图片...(" + pbProgress.Value.ToString() + "/" + _totalImage.ToString() + ")";
            };
        }

        /// <summary>
        /// 浏览操作目录
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnChoosePath_Click(object sender, RoutedEventArgs e)
        {
            FolderBrowserDialog folder = new FolderBrowserDialog();
            folder.ShowNewFolderButton = false;
            if (folder.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                _currentPath = folder.SelectedPath;
                tbWorkPath.Text = _currentPath;
            }
        }

        /// <summary>
        /// 文本改变
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TbKey_TextChanged(object sender, TextChangedEventArgs e)
        {
            _currentKey = tbKey.Text;
        }

        /// <summary>
        /// 压缩
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void BtnCompress_Click(object sender, RoutedEventArgs e)
        {
            Tinify.Key = _currentKey;
            if (cbScale.IsChecked == true)//需要缩放
            {
                _needToScale = true;
                try
                {
                    _newWidth = Math.Abs(Convert.ToInt32(tbNewWidth.Text));
                }
                catch
                {
                    System.Windows.MessageBox.Show("缩放图片宽度数据错误");
                    return;
                }
            }
            else
            {
                _needToScale = false;
            }
            msgProgress.Content = "向TinyPNG服务器发送请求...";
            try
            {
                await Tinify.Validate();
            }
            catch (System.Exception ex)
            {
                System.Windows.MessageBox.Show("连接失败" + ex.Message.ToString());
                return;
            }
            msgProgress.Content = "成功连接至TinyPNG服务器。";
            if (cbOverlay.IsChecked == true)//覆盖
            {
                _outputPath = _currentPath;
            }
            else
            {
                _outputPath = _currentPath + "/CompressedResult";
                CreateDirectory(_outputPath);
            }

            //开启多线程
            //if (cbThread.IsChecked == true)
            //{
            //    try
            //    {
            //        _threadCount = Convert.ToInt32(tbThreadCount.Text);
            //    }
            //    catch
            //    {
            //        System.Windows.MessageBox.Show("线程数量输入不合理");
            //        _threadCount = 1;
            //        return;
            //    }
            //}
            //else
            //{
            //    _threadCount = 1;
            //}
            _threadCount = 1;

            GetCompressedImg(_outputPath);
        }

        private async void WorkThread(object obj)
        {
            //如果队列非空
            while (targetFiles.Count != 0)
            {
                string sPath;
                //数据出队
                lock (_queueLocker)
                {
                    //检查队列是否为空
                    if (targetFiles.Count != 0)
                    {
                        sPath = targetFiles.Dequeue();
                    }
                    else
                    {
                        break;
                    }
                }

                string resultPath = sPath;
                resultPath = resultPath.Replace(_currentPath, _outputPath);
                CreateDirectory(Path.GetDirectoryName(resultPath));

                //上传下载图片
                await UploadAndDownloadAsync(sPath, resultPath);
            }

            ManualResetEvent mre = (ManualResetEvent)obj;
            mre.Set();//表示完成
        }

        /// <summary>
        /// 创建文件夹
        /// </summary>
        /// <param name="fileName"></param>
        private static void CreateDirectory(string fileName)
        {
            //文件夹存在则返回
            if (Directory.Exists(fileName))
            {
                return;
            }
            else
            {
                Directory.CreateDirectory(fileName);
            }
        }

        /// <summary>
        /// 递归遍历
        /// </summary>
        /// <param name="path"></param>
        private void SearchImage(string path)
        {
            DirectoryInfo theFolder = new DirectoryInfo(path);
            //遍历文件
            foreach (FileInfo NextFile in theFolder.GetFiles("*.png"))
            {
                targetFiles.Enqueue(NextFile.FullName);
                msgProgress.Content = "遍历目录文件夹及子文件夹png文件...  已发现" + targetFiles.Count.ToString() + "个PNG文件。";
            }
            //遍历文件夹
            foreach (DirectoryInfo NextFolder in theFolder.GetDirectories())
            {
                //递归遍历
                if (NextFolder.Name != "CompressedResult")//不遍历结果文件夹
                {
                    SearchImage(NextFolder.FullName);
                }
            }
        }

        /// <summary>
        /// TinyPNG操作核心方法
        /// </summary>
        /// <param name="sourcePath"></param>
        /// <param name="resultPath"></param>
        /// <returns></returns>
        private async Task UploadAndDownloadAsync(string sourcePath, string resultPath)
        {
            try
            {
                var source = Tinify.FromFile(sourcePath);
                //是否缩放
                if (_needToScale == true)
                {
                    var resized = source.Resize(new
                    {
                        method = "scale",
                        width = _newWidth
                    });
                    //下载图片
                    await resized.ToFile(resultPath);
                }
                else
                {
                    //下载图片
                    await source.ToFile(resultPath);
                }

                //进度条累加
                //pbProgress.Value += 1;
                pbProgress.Dispatcher.Invoke(progressBarDelegate);
                //更新文字
                msgProgress.Dispatcher.Invoke(progressMsgDelegate);
                //msgProgress.Content = "正在压缩/下载图片...(" + pbProgress.Value.ToString() + "/" + _totalImage.ToString() + ")";

            }
            catch (AccountException)
            {
                // Verify your API key and account limit.
                System.Windows.MessageBox.Show("Key错误或达到图片数量限制。");
                lock (_failedLocker)
                {
                    failedList.Add(sourcePath);
                }
            }
            catch (ClientException)
            {
                // Check your source image and request options.
                System.Windows.MessageBox.Show("源图片错误或参数异常。");
                lock (_failedLocker)
                {
                    failedList.Add(sourcePath);
                }
            }
            catch (ServerException)
            {
                // Temporary issue with the Tinify API.
                System.Windows.MessageBox.Show("服务器错误。");
                lock (_failedLocker)
                {
                    failedList.Add(sourcePath);
                }
            }
            catch (ConnectionException)
            {
                // A network connection error occurred.
                System.Windows.MessageBox.Show("检查网络连接。");
                lock (_failedLocker)
                {
                    failedList.Add(sourcePath);
                }
            }
            catch (System.Exception ex)
            {
                // Something else went wrong, unrelated to the Tinify API.
                System.Windows.MessageBox.Show("未知错误。\n" + ex.Message);
                lock (_failedLocker)
                {
                    failedList.Add(sourcePath);
                }
            }
        }

        /// <summary>
        /// 监视线程
        /// </summary>
        private void MonitorMethod()
        {
            foreach (ManualResetEvent mre in manualResetEvents)
            {
                mre.WaitOne();
            }

            //monitorThreadEvent.Set();

            System.Windows.MessageBox.Show("操作已完成，共处理" + targetFiles.Count.ToString() + "张图片\n"
                            + failedList.Count.ToString() + "张失败。");
            //如果有失败
            if (failedList.Count != 0)
            {
                FileStream fs = new FileStream("failedlog.txt", FileMode.Create);
                StreamWriter sw = new StreamWriter(fs);

                //写入失败日志
                foreach (string s in failedList)
                {
                    sw.WriteLine(s);
                }

            }
            //启用控件
            Action<System.Windows.Controls.Control> controlDelegant = (x) => { x.IsEnabled = true; };
            btnCompress.Dispatcher.Invoke(controlDelegant, btnCompress);
            btnChoosePath.Dispatcher.Invoke(controlDelegant, btnChoosePath);
            cbOverlay.Dispatcher.Invoke(controlDelegant, cbOverlay);
            cbScale.Dispatcher.Invoke(controlDelegant, cbScale);
            tbKey.Dispatcher.Invoke(controlDelegant, tbKey);
            tbNewWidth.Dispatcher.Invoke(controlDelegant, tbNewWidth);
            tbWorkPath.Dispatcher.Invoke(controlDelegant, tbWorkPath);

            Action temp = () => { msgProgress.Content = "操作完成"; };
            msgProgress.Dispatcher.Invoke(temp);
            //btnCompress.IsEnabled = true;
            //btnChoosePath.IsEnabled = true;
            //cbOverlay.IsEnabled = true;
            //cbScale.IsEnabled = true;
            //tbKey.IsEnabled = true;
            //tbNewWidth.IsEnabled = true;
            //tbWorkPath.IsEnabled = true;
        }

        /// <summary>
        /// 获取压缩结果
        /// </summary>
        /// <param name="outputPath"></param>
        private void GetCompressedImg(string outputPath)
        {
            targetFiles.Clear();
            failedList.Clear();
            //遍历文件
            msgProgress.Content = "遍历目录文件夹及子文件夹png文件...";
            SearchImage(_currentPath);

            //检查数量
            int compressionCount = (int)Tinify.CompressionCount;
            if (targetFiles.Count > 500 - compressionCount)
            {
                System.Windows.MessageBox.Show("当前key剩余数量不足以完成本次" + _totalImage.ToString() + "个PNG的转换");
            }
            else
            {
                if (targetFiles.Count != 0)
                {
                    //报告结果并开始压缩
                    _totalImage = targetFiles.Count;
                    if (System.Windows.MessageBox.Show("查找到" + _totalImage.ToString() + "个文件，当前key剩余可用转换数量：" +
                        (500 - compressionCount).ToString() + "。\n按确定开始压缩。",
                        "结果", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                    {
                        //停用控件
                        btnCompress.IsEnabled = false;
                        btnChoosePath.IsEnabled = false;
                        cbOverlay.IsEnabled = false;
                        cbScale.IsEnabled = false;
                        tbKey.IsEnabled = false;
                        tbNewWidth.IsEnabled = false;
                        tbWorkPath.IsEnabled = false;

                        pbProgress.Maximum = targetFiles.Count;
                        pbProgress.Value = 0;

                        msgProgress.Content = "正在压缩/下载图片...(" + pbProgress.Value.ToString() + "/" + _totalImage.ToString() + ")";

                        //创建子线程
                        _workThreads = new Thread[_threadCount];
                        for (int i = 0; i < _threadCount; i++)
                        {
                            _workThreads[i] = new Thread(new ParameterizedThreadStart(WorkThread));
                            _workThreads[i].Name = "TinypngWorkThread" + i.ToString();
                            ManualResetEvent mre = new ManualResetEvent(false);
                            manualResetEvents.Add(mre);
                            //运行子线程
                            _workThreads[i].Start(mre);
                        }

                        Thread monitorThread = new Thread(new ThreadStart(MonitorMethod));
                        monitorThread.Name = "Monitor";
                        monitorThread.Start();
                        //monitorThreadEvent.WaitOne();

                        //while (targetFiles.Count != 0)
                        //{
                        //    //等待子线程
                        //    msgProgress.Content = "正在压缩/下载图片...(" + pbProgress.Value.ToString() + "/" + _totalImage.ToString() + ")";
                        //}


                    }
                }
                else
                {
                    System.Windows.MessageBox.Show("没有发现图片");
                }
            }
        }
    }
}
