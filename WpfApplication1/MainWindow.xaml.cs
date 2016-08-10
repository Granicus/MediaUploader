using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using Granicus.MediaManager.SDK;
using System.IO;
using System.Threading;
using System.Windows.Threading;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.ComponentModel;
using System.Net;
using Newtonsoft.Json;
using GranicusMediaUploader.json;
using GranicusMediaUploader;

namespace WpfApplication1
{
    internal class UploadData
    {
        public string Domain = "";
        public FolderData RemoteFolder = null;
        public List<MediaData> MediaData = new List<MediaData>();
    }

    internal class MediaData
    {
        public Exception Exception = null;
        public string InputFile = "";
        public string OutputFile = "";
        public string Url = "";
        public string PublishPointUrl = "";
        public int ClipId = 0;
        public string ClipName = "";

        public string InputFileName
        {
            get
            {
                return Path.GetFileName(InputFile);
            }
        }
    }
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public MediaManager mema = new MediaManager();
        private bool Connected = false;
        private readonly string DRAG_DROP_TEMPLATE = "\n\n\n\n\n\n\n\nDrag and drop your files here to upload to {FolderName}.";
        private FolderData _selectedFolder;
        private string _host = "";
        public event PropertyChangedEventHandler PropertyChanged;


        private long currentPosition;
        private int defaultChunkSize = 1024 * 1024;
        private readonly BackgroundWorker uploadFileBackgroundWorker = new BackgroundWorker();
        private readonly BackgroundWorker transcodeFileBackgroundWorker = new BackgroundWorker();

        public MainWindow()
        {
            InitializeComponent();

            uploadFileBackgroundWorker.WorkerReportsProgress = true;
            uploadFileBackgroundWorker.DoWork += uploadFileBackgroundWorker_DoWork;
            uploadFileBackgroundWorker.RunWorkerCompleted += uploadFileBackgroundWorker_RunWorkerCompleted;
            uploadFileBackgroundWorker.ProgressChanged += uploadFileBackgroundWorker_ProgressChanged;
            transcodeFileBackgroundWorker.WorkerReportsProgress = true;
            transcodeFileBackgroundWorker.DoWork += transcodeFileBackgroundWorker_DoWork;
            transcodeFileBackgroundWorker.RunWorkerCompleted += transcodeFileBackgroundWorker_RunWorkerCompleted;
            transcodeFileBackgroundWorker.ProgressChanged += transcodeFileBackgroundWorker_ProgressChanged;

            this.Loaded += new RoutedEventHandler(MainWindow_Loaded);

            // bind the drag drop text property in this class
            Binding binding = new Binding("DragDropText");
            binding.Source = this;
            dragdropText.SetBinding(TextBlock.TextProperty, binding);

            this.Title = "Granicus Media Uploader - " + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
        }

        protected void OnPropertyChanged(string name)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(name));
            }
        }

        void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            ShowLogin();
        }

        void transcodeFileBackgroundWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            uploadProgressBar.Value = e.ProgressPercentage;
        }

        void uploadFileBackgroundWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            uploadProgressBar.Value = e.ProgressPercentage;
        }

        void backgroundWorkersDone()
        {
            dragdropText.Visibility = System.Windows.Visibility.Visible;
            FolderListView.IsEnabled = true;
        }

        void transcodeFileBackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            // the result of the transcode worker is an array of transcoded files ready for upload
            if (e.Error != null)
            {
                MessageBox.Show("Error uploading file: " + e.Error.Message);
                statusLabel.Content = "Error while transcoding.";
                backgroundWorkersDone();
            }
            else
            {
                uploadFileBackgroundWorker.RunWorkerAsync(e.Result);
            }
        }

        void uploadFileBackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Error != null)
            {
                MessageBox.Show(e.Error.Message);
                statusLabel.Content = "Error while uploading.";
            }
            else
            {
                UploadData data = (UploadData)e.Result;
                statusLabel.Content = "Upload Complete.";
                //clipLinksText.visibility = System.Windows.Visibility.Visible;
                string results = "";

                foreach (MediaData media in data.MediaData)
                {
                    results += string.Format("File: {0}\r\n", media.InputFileName);
                    if (media.Exception != null)
                    {
                        results += string.Format("Error: {0}\r\n\r\n", media.Exception.Message);
                    }
                    else
                    {
                        results += string.Format("Url: {0}\r\nPublishingPoint: {1}\r\n\r\n", media.Url, media.PublishPointUrl);
                    }
                }
                Results resultsWindow = new Results();
                resultsWindow.Text = results;
                resultsWindow.ShowDialog();
            }
            backgroundWorkersDone();
        }

        void transcodeFileBackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            UploadData data = (UploadData)e.Argument;

            for(int i=0; i<data.MediaData.Count; i++)
            {
                try
                {
                    setStatusLabel(string.Format("{0}/{1} Transcoding: {2}", i, data.MediaData.Count, Path.GetFileName(data.MediaData[i].InputFile) + "..."));
                    switch (System.IO.Path.GetExtension(data.MediaData[i].InputFile).ToLower())
                    {
                        case ".mp4":
                            data.MediaData[i].OutputFile = Copy(data.MediaData[i].InputFile);
                            break;
                        case ".mp3":
                        case ".wma":
                            data.MediaData[i].OutputFile = TranscodeAudio(data.MediaData[i].InputFile);
                            break;
                        default:
                            data.MediaData[i].OutputFile = TranscodeVideo(data.MediaData[i].InputFile);
                            break;
                    }

                    if (!File.Exists(data.MediaData[i].OutputFile))
                    {
                        throw new Exception(string.Format("Unable to transcode file '{0}' to output '{1}'. Please verify it is a valid audio file.", data.MediaData[i].InputFileName, data.MediaData[i].OutputFile));
                    }
                }
                catch(Exception ex)
                {
                    data.MediaData[i].Exception = ex;
                }
            }
            e.Result = data;
        }

        private string Copy(string input)
        {
            var filename = System.IO.Path.GetFileName(input);
            var temp_file = System.IO.Path.GetTempPath() + filename;
            File.Copy(input, temp_file);
            return temp_file;
        }

        private string TranscodeVideo(string input)
        {
            var filename = System.IO.Path.GetFileNameWithoutExtension(input);
            var temp_file = System.IO.Path.GetTempPath() + filename + ".mp4";
            Process proc = new Process();
            proc.StartInfo.FileName = "ffmpeg.exe";
            proc.StartInfo.Arguments = String.Format(@"-i ""{0}"" -y -c:v libx264 -pix_fmt yuv420p -crf 23 -strict experimental -c:a aac ""{1}""", input, temp_file);
            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.CreateNoWindow = true;
            proc.StartInfo.RedirectStandardOutput = true;
            proc.StartInfo.RedirectStandardError = true;
            proc.Start();
            proc.BeginOutputReadLine();
            StreamReader reader = proc.StandardError;
            string line;
            string progressPattern = @"^frame=\s*([\d\.]+)\s*?fps=\s*([\d\.]+)\s*q=([\d\.]+)\s*size=\s*(\d+)kB\s*time=([\d:\.]+)\s*bitrate=";
            string durationPattern = @"Duration: (.*), start:";
            float duration = 0;

            while ((line = reader.ReadLine()) != null)
            {
                Console.WriteLine(line);

                var match = Regex.Match(line, progressPattern);
                if (match.Success)
                {
                    Console.WriteLine("progessPattern: " + line);
                    var durationString = match.Groups[5].Value;
                    string[] parts = durationString.Split(':').Reverse().ToArray<string>();
                    float progress = float.Parse(parts[0]) + (int.Parse(parts[1]) * 60) + (int.Parse(parts[2]) * 3600);
                    Action reportCurrentProgress = delegate ()
                    {
                        transcodeFileBackgroundWorker.ReportProgress((int)((progress / duration) * 100));
                    };
                    Dispatcher.Invoke(reportCurrentProgress, DispatcherPriority.Normal, null);

                    Console.WriteLine("progressString: " + durationString);
                    Console.WriteLine("progressDuration: " + duration.ToString());
                }

                match = Regex.Match(line, durationPattern);
                if (match.Success)
                {
                    Console.WriteLine("durationPattern: " + line);
                    var durationString = match.Groups[1].Value;
                    string[] parts = durationString.Split(':').Reverse().ToArray<string>();
                    duration = Math.Max(duration, float.Parse(parts[0]) + (int.Parse(parts[1]) * 60) + (int.Parse(parts[2]) * 3600));
                    Console.WriteLine("durationString: " + durationString);
                    Console.WriteLine("duration: " + duration.ToString());
                }
            }

            proc.WaitForExit();
            return temp_file;
        }

        private string TranscodeAudio(string input)
        {
            var filename = System.IO.Path.GetFileNameWithoutExtension(input);
            var temp_file = System.IO.Path.GetTempPath() + filename + ".mp4";
            Process proc = new Process();
            proc.StartInfo.FileName = "ffmpeg.exe";
            proc.StartInfo.Arguments = String.Format(@"-y -loop 1 -i audio_only.jpg -i ""{0}"" -c:v libx264 -pix_fmt yuv420p -strict experimental -c:a aac -shortest ""{1}""", input, temp_file);
            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.RedirectStandardOutput = true;
            proc.StartInfo.CreateNoWindow = true;
            proc.StartInfo.RedirectStandardError = true;
            proc.Start();
            proc.BeginOutputReadLine();
            StreamReader reader = proc.StandardError;
            string line;
            string progressPattern = @"^frame=\s*([\d\.]+)\s*?fps=\s*([\d\.]+)\s*q=([\d\.]+)\s*size=\s*(\d+)kB\s*time=([\d:\.]+)\s*bitrate=";
            string durationPattern = @"Duration: (.*), start:";
            float duration = 0;

            while ((line = reader.ReadLine()) != null)
            {
                Console.WriteLine(line);

                var match = Regex.Match(line, progressPattern);
                if (match.Success)
                {
                    Console.WriteLine("progessPattern: " + line);
                    var durationString = match.Groups[5].Value;
                    string[] parts = durationString.Split(':').Reverse().ToArray<string>();
                    float progress = float.Parse(parts[0]) + (int.Parse(parts[1]) * 60) + (int.Parse(parts[2]) * 3600);
                    Action reportCurrentProgress = delegate ()
                    {
                        transcodeFileBackgroundWorker.ReportProgress((int)((progress / duration) * 100));
                    };
                    Dispatcher.Invoke(reportCurrentProgress, DispatcherPriority.Normal, null);

                    Console.WriteLine("progressString: " + durationString);
                    Console.WriteLine("progressDuration: " + duration.ToString());
                }

                match = Regex.Match(line, durationPattern);
                if (match.Success)
                {
                    Console.WriteLine("durationPattern: " + line);
                    var durationString = match.Groups[1].Value;
                    string[] parts = durationString.Split(':').Reverse().ToArray<string>();
                    duration = Math.Max(duration, float.Parse(parts[0]) + (int.Parse(parts[1]) * 60) + (int.Parse(parts[2]) * 3600));
                    Console.WriteLine("durationString: " + durationString);
                    Console.WriteLine("duration: " + duration.ToString());
                }
            }

            proc.WaitForExit();
            Action reportFinalProgress = delegate ()
            {
                transcodeFileBackgroundWorker.ReportProgress(100);
            };
            Dispatcher.Invoke(reportFinalProgress, DispatcherPriority.Normal, null);
            return temp_file;
        }

        private void SetWindowCoordsCenter(Window child, Window parent)
        {
            // center the login window ontop of the main window (note using actualheight for parent window since it
            // is likely rendered but using the requested height for child since its likely not yet rendered)
            child.Left = parent.Left + (parent.ActualWidth - child.Width) / 2;
            child.Top = parent.Top + (parent.ActualHeight - child.Height) / 2;
        }

        private void ShowLogin()
        {
            while (!Connected)
            {
                try
                {
                    LoginWindow loginDialog = new LoginWindow();
                    SetWindowCoordsCenter(loginDialog, Application.Current.MainWindow);

                    if (loginDialog.ShowDialog() == true)
                    {
                        mema.Connect(loginDialog.Host, loginDialog.Login, loginDialog.Password);
                        Connected = true;
                        _host = loginDialog.Host;
                        loginText.Visibility = System.Windows.Visibility.Hidden;
                        selectFolderText.Visibility = System.Windows.Visibility.Visible;
                        List<FolderData> folders = mema.GetFolders().ToList<FolderData>();
                        FolderListView.ItemsSource = folders;
                    }
                    else
                    {
                        break;
                    }
                }
                catch (System.Web.Services.Protocols.SoapException soapEx)
                {
                    MessageBox.Show(soapEx.Message);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Unable to login. Please verify the host, username, and password are correct.");
                }
            }

            // user closed the login dialog, close down the app since theres currently no features
            // to use without authenticated user
            if (!Connected)
            {
                Application.Current.Shutdown();
            }
        }

        private void Login_MenuItem_Click(object sender, RoutedEventArgs e)
        {
            ShowLogin();
        }

        private string _dragdroptext = "";
        public string DragDropText
        {
            get
            {
                return _dragdroptext;
            }
            set
            {
                _dragdroptext = value;
                PropertyChanged(this, new PropertyChangedEventArgs("DragDropText"));
            }

        }
        private void FolderListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedFolder = (FolderData)FolderListView.SelectedItem;
            DragDropText = DRAG_DROP_TEMPLATE.Replace("{FolderName}", _selectedFolder.Name);
            dragdropText.Visibility = System.Windows.Visibility.Visible;
            selectFolderText.Visibility = System.Windows.Visibility.Hidden;
        }

        private void dragdropText_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                StartUpload(files);
            }
        }

        private void StartUpload(string[] files)
        {
            UploadData data = new UploadData();
            data.RemoteFolder = _selectedFolder;
            data.Domain = _host;
            foreach (string f in files)
            {
                MediaData d = new MediaData();
                d.InputFile = f;
                data.MediaData.Add(d);
            }
            dragdropText.Visibility = System.Windows.Visibility.Hidden;
            FolderListView.IsEnabled = false;
            transcodeFileBackgroundWorker.RunWorkerAsync(data);
        }
        private void publishClip(MediaData media, string host, FolderData folder)
        {
            string url =  "http://" + host + "/api/clips/v1/publish";
            WebRequest request = WebRequest.Create(url);

            ((HttpWebRequest)request).UserAgent = "Granicus Media Uploader Agent";
            request.Method = "POST";
            request.ContentType = "application/json";
            ((HttpWebRequest)request).CookieContainer = new CookieContainer();

            foreach (Cookie c in mema.CookieContainer.GetCookies(new Uri(mema.Url)))
            {
                ((HttpWebRequest)request).CookieContainer.Add(c);
            }

            // add debug cookie if local
            if (mema.Url.Contains("mm.lvh.me"))
            {
                ((HttpWebRequest)request).CookieContainer.Add(new Cookie("XDEBUG_SESSION", "XDEBUG_ECLIPSE"));
            }

            using (var streamWriter = new StreamWriter(request.GetRequestStream()))
            {
                PublishRequestData reqData = new PublishRequestData();
                reqData.clip_id = media.ClipId.ToString();
                reqData.name = string.Format("Publish Point For {0}", media.ClipName);
                string json = JsonConvert.SerializeObject(reqData);
                streamWriter.Write(json);
                streamWriter.Flush();
                streamWriter.Close();
            }

            HttpWebResponse response = (HttpWebResponse)request.GetResponse();

            using (var reader = new StreamReader(response.GetResponseStream()))
            {
                string resp = reader.ReadToEnd();
                PublishResponseData respData = JsonConvert.DeserializeObject<PublishResponseData>(resp);
                media.PublishPointUrl = respData.publish_point;
                media.Url = respData.url;
            }
        }

        private void uploadFileBackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            UploadData data = (UploadData)e.Argument;
            MediaVault mediaVault = null;
            string file = "";

            try
            {
                mediaVault = mema.GetMediaVault(data.RemoteFolder.ID);
            }
            catch (Exception ex)
            {
                throw new Exception(string.Format("Unable to upload files to folder '{0}'. Please verify the folder is setup properly with a Distributor.\n\n--------\n{1}", _selectedFolder.Name, ex.Message));
            }

            try
            {
                for (int i = 0; i < data.MediaData.Count; i++)
                {
                    if (data.MediaData[i].Exception != null)
                    {
                        continue;
                    }

                    try
                    {
                        file = data.MediaData[i].OutputFile;
                        setStatusLabel(string.Format("{0}/{1} Uploading: {2}", i, data.MediaData.Count, Path.GetFileName(file) + "..."));
                        FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read);
                        BinaryReader reader = new BinaryReader(fs);
                        long size = new FileInfo(file).Length;
                        if (size == 0) continue;
                        currentPosition = 0;
                        int chunksize = defaultChunkSize;
                        var currentBucket = mediaVault.StartUpload();
                        var currentFile = new FileInfo(file).Name;
                        while (currentPosition < size)
                        {
                            // background worker implementation details
                            Action reportCurrentProgress = delegate ()
                            {
                                uploadFileBackgroundWorker.ReportProgress((int)(((double)currentPosition / (double)size) * 100));
                            };
                            Dispatcher.Invoke(reportCurrentProgress, DispatcherPriority.Normal, null);
                            if (uploadFileBackgroundWorker.CancellationPending)
                            {
                                mediaVault.AbortUpload(currentBucket);
                                e.Cancel = true;
                                return;
                            }

                            // send chunk implementation
                            reader.BaseStream.Seek(currentPosition, SeekOrigin.Begin);
                            if (chunksize > (size - currentPosition))
                            {
                                chunksize = (int)(size - currentPosition);
                            }
                            byte[] chunk = new byte[chunksize];
                            reader.Read(chunk, 0, chunksize);
                            try
                            {
                                mediaVault.SendChunk(currentBucket, currentPosition, chunk);
                                currentPosition += chunksize;
                            }
                            catch (Exception)
                            {
                                // we want to retry on any other exception thrown by SendChunk()
                            }
                        }
                        Action reportFinalProgress = delegate ()
                        {
                            uploadFileBackgroundWorker.ReportProgress(100);
                        };

                        Dispatcher.Invoke(reportFinalProgress, DispatcherPriority.Normal, null);
                        // the entire file transferred, so register it with MediaManager and move on to the next file
                        mediaVault.FinishUpload(currentBucket);
                        string extension = new FileInfo(file).Extension.Substring(1);
                        data.MediaData[i].ClipName = currentFile.TrimEnd(("." + extension).ToCharArray());
                        data.MediaData[i].ClipId = mediaVault.RegisterUploadedFile(currentBucket, _selectedFolder.ID, data.MediaData[i].ClipName, extension);
                        // delete the uploaded file (we've already moved it to a temp folder)
                        //File.Delete(file); 

                        PublishClipData publishdata = new PublishClipData(
                            data.MediaData[i].ClipId,
                            true,
                            "Publishing Point For " + data.MediaData[i].ClipName,
                            "Publishing Point For " + data.MediaData[i].ClipName,
                            "Publishing Point For " + data.MediaData[i].ClipName,
                            -1,
                            -1);
                        setStatusLabel(string.Format("{0}/{1} Publishing: {2}", i, data.MediaData.Count, Path.GetFileName(file) + "..."));
                        PublishClipResult result = mema.PublishClip(publishdata);
                        data.MediaData[i].Url = result.URL;
                        data.MediaData[i].PublishPointUrl = result.PublishPoint;
                        //this.publishClip(data.MediaData[i], data.Domain, data.RemoteFolder);
                    }
                    catch (Exception ex)
                    {
                        data.MediaData[i].Exception = ex;
                    }
                }
                e.Result = data;      
            }
            catch (Exception ex)
            {
                throw new Exception(string.Format("Unable to upload file '{0}' to folder '{1}'. Please verify the file is a valid audio file.\n\n--------\n{2}", file != null ? Path.GetFileName(file) : "", data.RemoteFolder.Name, ex.Message));
            }
        }

        private void setStatusLabel(string text)
        {
            Action action = delegate ()
            {
                statusLabel.Content = text;
            };
            Dispatcher.Invoke(action, DispatcherPriority.Normal, null);
        }

        private void dragdropText_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog();
            dialog.Multiselect = true;
            dialog.Filter = "Media Files|*.*";
            if (dialog.ShowDialog() == true)
            {
                StartUpload(dialog.FileNames);
            }
        }

    }
}
