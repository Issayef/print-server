using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Runtime.InteropServices;
using System.Windows.Interop;

using Newtonsoft.Json.Linq;

using System.Drawing.Printing;
using System.IO;
using System.Drawing.Imaging;
using System.Net;

using System.Collections.Specialized;
using IniParser;
using IniParser.Model;
using Word = Microsoft.Office.Interop.Word;
using Excel = Microsoft.Office.Interop.Excel;
using System.Diagnostics;
using System.Net.Sockets;

namespace PrintServer
{

    /// <summary>
    /// Позиция окна на экране
    /// </summary>
    enum PrintServerPostion
    {
        tpLeftTop,
        tpRightTop,
        tpLeftBottom,
        tpRightBottom,
        tpScreenSenter
    };
   
    /// <summary>
    /// Размеры окна
    /// </summary>
    enum PrintSize { tsSmall, tsMedium, tsLarge };
    
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string TempDir;
        private string DownloadUrl;
        private string UploadUrl;
        private string SavePath;
        /// <summary>
        /// Таймер для переподключения
        /// </summary>
        private System.Timers.Timer reConnect;
        /// <summary>
        /// Прозрачность
        /// </summary>
        private int aOpacity = 100;
        /// <summary>
        /// Отображать или нет
        /// </summary>
        private bool is_visible = false;
        /// <summary>
        /// Позиция окна на экране
        /// </summary>
        private PrintServerPostion positionPrinter = PrintServerPostion.tpRightBottom;
        /// <summary>
        /// Размеры окна
        /// </summary>
        private PrintSize sizeTimer = PrintSize.tsSmall;
        /// <summary>
        /// <summary>
        /// Вспомогательная переменная для сброса активации окна
        /// </summary>
        WindowInteropHelper helper;

        /// <summary>
        /// Свернуто в tray
        /// </summary>
        private bool inTray = true;
        /// <summary>
        WSClient ws;
        private string PrintServerID;
        private string _gsPath;
        private string _gsprintPath;
        /// <summary>
        /// Делегат
        /// </summary>
        /// <param name="res"></param>
        private delegate void TimerCallback(JObject res);
        private delegate void TimerCallbacks();
        private delegate void TimerCallbackSync(int res);
        private delegate void TimerCallbackTray(bool value);

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            helper = new WindowInteropHelper(this);
        }

        private void Window_Activated(object sender, EventArgs e)
        {
            // отключаем активацию окна
            SetWindowLong(helper.Handle, GWL_EXSTYLE,
                GetWindowLong(helper.Handle, GWL_EXSTYLE) | WS_EX_NOACTIVATE);
        }

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_NOACTIVATE = 0x08000000;

        [DllImport("user32.dll")]
        public static extern IntPtr SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        public MainWindow()
        {
            InitializeComponent();

            SetSize(0);
            PrintServerSettings st = ((PrintServer.App)System.Windows.Application.Current).Settings;
            var parser = new FileIniDataParser();
            string ini = AppDomain.CurrentDomain.BaseDirectory + @"printconfig.ini";
            IniData data = parser.ReadFile(ini);
            _gsPath = data["server"]["gswin"];
            _gsprintPath = data["server"]["gsprint"];

            TempDir = st.TempDir;
            DownloadUrl = st.DownloadUrl;
            UploadUrl = st.UploadUrl;
            PrintServerID = st.PrintServerId;
            SavePath = System.IO.Path.GetTempPath() + TempDir;
            CreateDir();

            ws = new WSClient(st.getUri());
            ws.OnEvent += ws_OnEvent;
            ws.OnClose += ws_OnClose;
            ws.OnOpen += ws_OnOpen;
            ws.OnError += ws_OnError;

            reConnect = new System.Timers.Timer();
            reConnect.Elapsed += reConnect_Elapsed;
            reConnect.Interval = 50;
            reConnect.AutoReset = false;
            reConnect.Enabled = false;
            Init();
            Dispatcher.Invoke(new TimerCallbacks(this.InstalledDevices));
        }

        /// <summary>
        /// Попытка установить соединение
        /// </summary>
        private void Init()
        {
            if (ws.IsAlive)
            {
                ws.Close();
            }

            ws.Connect();

            if (ws.IsAlive)
            {
                // канал синхронизации
                ws.Subscribe("synchronization");

                // идентификатор таймера
                ws.Subscribe(PrintServerID);

                reConnect.Enabled = false;
            }

            //this.Visibility = System.Windows.Visibility.Hidden;
        }

        /// <summary>
        /// Попытка переключиться
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void reConnect_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            Init();
        }

        void ws_OnError(object sender, WebSocketSharp.ErrorEventArgs e)
        {
            Console.WriteLine(e.Message);
            reConnect.Enabled = true;
        }

        private void SetToolTip(bool connected)
        {
            fNotifyIcon.ToolTipText = "Принт-сканер: " + (connected ? "(подключен)" : (reConnect.Enabled ? "(подключение ...)" : "(не подключен)"));
        }

        void ws_OnOpen(object sender, EventArgs e)
        {
            Dispatcher.Invoke(new TimerCallbackTray(this.SetToolTip), new object[] { true });
        }

        void ws_OnClose(object sender, WebSocketSharp.CloseEventArgs e)
        {
            Dispatcher.Invoke(new TimerCallbackTray(this.SetToolTip), new object[] { false });
        }

       
        /// <summary>
        /// Обработка данных от сервера
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void ws_OnEvent(object sender, CallbackEventArgs e)
        {
            if (e.Topic == "synchronization")
            {
                Console.WriteLine("synchronization: " + e.Data["value"]);
            }
            else if (e.Topic == PrintServerID)
            {
                switch (Convert.ToInt32((string)e.Data["msg_type"]))
                {
                    case 1:
                        // системные функции
                        break;
                    case 2:
                        // получение списка сканеров
                        Dispatcher.Invoke(new TimerCallbacks(this.InstalledScanners));
                        break;
                    case 3:
                        // сканирование
                        Dispatcher.Invoke(new TimerCallback(this.Scanning), new object[] { e.Data });
                        break;
                    case 4:
                        // получение списка принтеров
                        Dispatcher.Invoke(new TimerCallbacks(this.InstalledPrinters));
                        break;
                    case 5:
                        // печать
                        Dispatcher.Invoke(new TimerCallback(this.Prinnting), new object[] { e.Data });
                        break;
                }  
            }
        }
        private void InstalledDevices()
        {
            string server = Dns.GetHostName();
            IPHostEntry heserver = Dns.GetHostEntry(server);
            string ip = null;
            string ip2 = null;
            foreach (IPAddress curAdd in heserver.AddressList)
            {
                if (curAdd.AddressFamily.ToString() == ProtocolFamily.InterNetwork.ToString())
                {
                    Console.WriteLine("AddressFamily: " + curAdd.AddressFamily.ToString());
                    Console.WriteLine("Address: " + curAdd.ToString());
                    ip2 = curAdd.ToString();
                }
            }
            try
            {
                List<string> devices = GetDevices();
                JArray scaners = new JArray();
                JArray printers = new JArray();
                for (int i = 0; i < devices.Count; i++)
                {
                    scaners.Add(devices[i]);
                }
                for (int i = 0; i < PrinterSettings.InstalledPrinters.Count; i++)
                {
                    printers.Add(PrinterSettings.InstalledPrinters[i]);
                }
                JObject json = new JObject();
                json["Scaners"] = scaners;
                json["Printers"] = printers;
                json["username"] = Environment.UserName;
                json["machine_name"] = Environment.MachineName;
                json["ip"] = ip2;

                ws.Publish(PrintServerID, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void InstalledScanners()
        {
            try
            {
                List<string> devices = GetDevices();
                JArray scaners = new JArray();
                for (int i = 0; i < devices.Count; i++)
                {
                    scaners.Add(devices[i]);
                }
                JObject json = new JObject();
                json["Scaners"] = scaners;
                ws.Publish(PrintServerID, json);               
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void InstalledPrinters()
        {
            JArray printers = new JArray();
            for (int i = 0; i < PrinterSettings.InstalledPrinters.Count; i++)
            {
                printers.Add(PrinterSettings.InstalledPrinters[i]);
            } 
            JObject json = new JObject();
            json["Printers"] = printers;
            ws.Publish(PrintServerID, json);
        }

        string id = null;
        string gswinPath = null;
        string gsprintPath = null;

        #region функция для печати файла
        private void Prinnting(JObject res)
        {
            PrintDocument PD = new PrintDocument();
            PrinterSettings PS = new PrinterSettings();
            string PrinterName = (string)res["device_name"];
            id = (string)res["file_id"];
            string type = (string)res["mtype"];
            string format_bumagi = (string)res["format"];
            // печать пдф с помощью ghostscript
            if (type == "pdf")
            {
                string fileName = DateTime.Now.ToString("yyyy-MM-dd HHmmss");
                DownloadFromServer(SavePath, fileName, DownloadUrl, id);
                //путь до файла печати
                string path = SavePath + fileName;
                var parser = new FileIniDataParser();
                string ini = AppDomain.CurrentDomain.BaseDirectory + @"printconfig.ini";
                IniData data = parser.ReadFile(ini);
                gswinPath = data["server"]["gswin"];
                gsprintPath = data["server"]["gsprint"];
                //путь до приложения ghostscript
                string gsExecutable ="";
                string gsPrintExecutable = gsprintPath + @"\gsprint.exe";
                if (Environment.Is64BitOperatingSystem == true)
                {
                    gsExecutable = gswinPath + @"\gswin64c.exe";
                }
                else
                {
                    gsExecutable = gswinPath + @"\gswin32c.exe";
                }


                string processArgs = string.Format("-ghostscript \"{0}\" -copies=1 -sPAPERSIZE=\"{1}\" -dFIXEDMEDIA -margins=[0,0] -all -printer \"{2}\" \"{3}\"", gsExecutable, format_bumagi, PrinterName, path);
                var gsProcessInfo = new ProcessStartInfo
                {
                    WindowStyle = ProcessWindowStyle.Hidden,
                    FileName = gsPrintExecutable,
                    Arguments = processArgs
                };
                using (var gsProcess = Process.Start(gsProcessInfo))
                {
                    gsProcess.WaitForExit();
                }
            }
            if (type == "xlsx" || type == "xls")
            {
                string fileName = DateTime.Now.ToString("yyyy-MM-dd HHmmss");
                DownloadFromServer(SavePath, fileName, DownloadUrl, id);
                string path = SavePath + fileName;

                Excel.Application excelApp = new Excel.Application();
                
                Excel.Workbook wb = excelApp.Workbooks.Open(
                    path,
                    Type.Missing, Type.Missing, Type.Missing, Type.Missing, Type.Missing,
                    Type.Missing, Type.Missing, Type.Missing, Type.Missing, Type.Missing,
                    Type.Missing, Type.Missing, Type.Missing, Type.Missing);

                wb.PrintOut(
                    Type.Missing, Type.Missing, Type.Missing, Type.Missing,
                    PrinterName, Type.Missing, Type.Missing, Type.Missing);

                GC.Collect();
                GC.WaitForPendingFinalizers();
                wb.Close(false, Type.Missing, Type.Missing);
                Marshal.FinalReleaseComObject(wb);

                excelApp.Quit();
                Marshal.FinalReleaseComObject(excelApp);
            }
            if (type == "docx" || type == "doc")
            {
                string fileName = DateTime.Now.ToString("yyyy-MM-dd HHmmss");
                DownloadFromServer(SavePath, fileName, DownloadUrl, id);
                string path = SavePath + fileName;

                Word.Application wordapp = new Word.Application();
                wordapp.Visible = false;

                object filename = path;
                object missingValue = Type.Missing;
                Word.Document document = wordapp.Documents.OpenOld(ref filename,
                ref missingValue, ref missingValue,
                ref missingValue, ref missingValue, ref missingValue,
                ref missingValue, ref missingValue, ref missingValue, ref missingValue);

                wordapp.ActivePrinter = PrinterName;

                object myTrue = true; // Фоновая печать
                object myFalse = false;
                wordapp.ActiveDocument.PrintOut(
                    ref myTrue,
                    ref myFalse,
                    ref missingValue,
                    ref missingValue,
                    ref missingValue,
                    ref missingValue,
                    ref missingValue,
                    ref missingValue,
                    ref missingValue,
                    ref missingValue,
                    ref myFalse,
                    ref missingValue,
                    ref missingValue,
                    ref missingValue);

                GC.Collect();
                GC.WaitForPendingFinalizers();

                document.Close(false, Type.Missing, Type.Missing);
                Marshal.FinalReleaseComObject(document);

                wordapp.Quit();
                Marshal.FinalReleaseComObject(wordapp);
            }

            if (type == "tiff" || type == "png" || type == "jpg" )
            {
                try
                {
                    PS.PrinterName = PrinterName;
                    PD.PrinterSettings = PS;
                    PD.PrintController = new StandardPrintController();
                    //Установка ориентации страницы
                    PD.PrinterSettings.DefaultPageSettings.Landscape = false;
                    //Установка цвета печати
                    PD.PrinterSettings.DefaultPageSettings.Color = false;
                    PD.PrintPage += PrintPageHandler;
                    PD.Print();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }
            
        }

        private void PrintPageHandler(object sender, PrintPageEventArgs e)
        {
            string fileName = DateTime.Now.ToString("yyyy-MM-dd HHmmss") + "." + "tiff";
            DownloadFromServer(SavePath, fileName, DownloadUrl, id);
            string path = SavePath + fileName;
            if (File.Exists(path))
                e.Graphics.DrawImage(System.Drawing.Image.FromFile(path), 0, 0);
        }
        #endregion

        #region функция сканирования
        private void Scanning(JObject res)
        {
            try
            {
                string ScanerName = (string)res["device_name"];
                string lichnost_id = (string)res["licnost_id"];
                List<System.Drawing.Image> images = WIAScanner.Scan(ScanerName);
                foreach (System.Drawing.Image image in images)
                {
                    string filename = DateTime.Now.ToString("yyyy-MM-dd HHmmss") + ".tiff";
                    string scan_path = SavePath + filename;
                    //сохранение в специальный файл
                    image.Save(scan_path, ImageFormat.Tiff);
                    NameValueCollection nvc = new NameValueCollection();
                    nvc.Add("id", lichnost_id);
                    UploadTOServer(UploadUrl, scan_path, "original", "image/tiff", nvc);
                }

            }
            catch
            {
                MessageBox.Show("Сканер неисправен! Выберите другой.");
            }
            
        }
        #endregion

        private void Show_button_Copy_Click(object sender, RoutedEventArgs e)
        {

                string spath = null;
                var dialog = new System.Windows.Forms.FolderBrowserDialog();
                dialog.Description =
                    "Выберите папку содержащую gsprint.exe.\n" +
                    @"По умолчанию: C:\Program Files\Ghostgum\gsview";
                dialog.ShowNewFolderButton = false;
                dialog.RootFolder = Environment.SpecialFolder.MyComputer;
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    spath = dialog.SelectedPath;
                ScanerLabel_Copy.Content = dialog.SelectedPath;
                    string ini = AppDomain.CurrentDomain.BaseDirectory + @"printconfig.ini";
                    var parser = new FileIniDataParser();
                    IniData data = parser.ReadFile(ini);
                    data["server"]["gsprint"] = spath;
                    parser.WriteFile(ini, data);
                }
        }
        //настройка конфигурации
        private void Show_button_Click(object sender, RoutedEventArgs e)
        {
            string spath = null;
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            dialog.Description =
                "Выберите папку содержащую gswin64c.exe.\n" +
                @"По умолчанию: C:\Program Files\gs\gs9.23\bin";
            dialog.ShowNewFolderButton = false;
            dialog.RootFolder = Environment.SpecialFolder.MyComputer;
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
                spath = dialog.SelectedPath;
                ScanerLabel.Content = dialog.SelectedPath;
                string ini = AppDomain.CurrentDomain.BaseDirectory + @"printconfig.ini";
                var parser = new FileIniDataParser();
                IniData data = parser.ReadFile(ini);
                data["server"]["gswin"] = spath;
                parser.WriteFile(ini, data);
            }
        }
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.Visibility = Visibility.Hidden;
            Init();
        }


        #region получение доступных сканеров
        private static List<string> GetDevices()
        {
            List<string> devices = new List<string>();
            WIA.DeviceManager manager = new WIA.DeviceManager();
            foreach (WIA.DeviceInfo info in manager.DeviceInfos)
            {
                devices.Add(info.DeviceID);
            }
            return devices;
        }
        #endregion

        #region Нахождение приложение в трее
        private void TrayClickOpen(object sender, RoutedEventArgs e)
        {
            if (this.Visibility == System.Windows.Visibility.Visible)
            {
                inTray = true;
                Hide();
            }
            else
            {
                inTray = false;
                Show();
            }
        }
        private void ChooseFolder(object sender, RoutedEventArgs e)
        {
            ScanerLabel.Content = _gsPath;
            ScanerLabel_Copy.Content = _gsprintPath;
            ScanerLabel.Visibility = Visibility.Visible;
            this.Visibility = Visibility.Visible;
        }
        private void Reconnect(object sender, RoutedEventArgs e)
        {
            Init();
        }

        #endregion

        #region Кнопка закрытия приложения
        private void TrayClickExit(object sender, RoutedEventArgs e)
        {
            //удаление временной директории и закрытие
            try
            {
                Directory.Delete(SavePath, true);
            }
            catch (Exception ex)
            {
                Console.WriteLine("The process failed: {0}", ex.Message);
            }
            this.Close();
        }
        #endregion

        #region Установить размер окна
        /// <param name="value"></param>
        private void SetSize(int value)
        {
            this.sizeTimer = (PrintSize)value;

            switch (this.sizeTimer)
            {
                case PrintSize.tsSmall:
                    this.Width = 380;
                    this.Height = 142.493;
                    break;
            }

            SetPosition(this.positionPrinter);
        }
        #endregion

        #region Установить позицию окна
        private void SetPosition(PrintServerPostion value)
        {
            double l = SystemParameters.WorkArea.Left;
            double t = SystemParameters.WorkArea.Top;
            double w = SystemParameters.WorkArea.Width;
            double h = SystemParameters.WorkArea.Height;
            double x = SystemParameters.WorkArea.X;
            double y = SystemParameters.WorkArea.Y;

            switch (value)
            {
                case PrintServerPostion.tpLeftTop:
                    Left = l;
                    Top = t;
                    break;

                case PrintServerPostion.tpRightTop:
                    Left = w - Width + x;
                    Top = t;
                    break;

                case PrintServerPostion.tpLeftBottom:
                    Left = l;
                    Top = h - Height + y;
                    break;

                case PrintServerPostion.tpRightBottom:
                    Left = w - Width + x;
                    Top = h - Height + y;
                    break;
                case PrintServerPostion.tpScreenSenter:
                    Left = (w - Width) / 2 + x;
                    Top = (h - Height) / 2 + y;
                    break;
            }

            this.positionPrinter = value;
        }
        #endregion

        #region Установка прозрачности
        /// <param name="Value">Значение прозрачности</param>
        private void SetOpacity(int Value)
        {
            if (Value < 0)
            {
                Value = 0;
            }
            else if (Value > 100)
            {
                Value = 100;
            }

            double d = Value / 100.0;

            this.Opacity = d;
            this.aOpacity = Value;
        }
        #endregion

        #region Скачивание файла с сервера
        private void DownloadFromServer(string spath, string fname, string url, string ID)
        {
            string downloadUrl = url;
            string save_path = spath;
            string id = ID;
            string fileName = fname;
            string myStringWebResource = null;
            WebClient myWebClient = new WebClient();
            // полный путь
            myStringWebResource = downloadUrl + id;
            myWebClient.DownloadFile(myStringWebResource, save_path + fileName);
        }
        #endregion

        #region метод для загрузки файла на сервер
        public void UploadTOServer(string url, string file, string paramName, string contentType, NameValueCollection nvc)
        {
            string boundary = "---------------------------" + DateTime.Now.Ticks.ToString("x");
            byte[] boundarybytes = System.Text.Encoding.ASCII.GetBytes("\r\n--" + boundary + "\r\n");

            HttpWebRequest wrequest = (HttpWebRequest)WebRequest.Create(url);
            wrequest.ContentType = "multipart/form-data; boundary=" + boundary;
            wrequest.Method = "POST";
            wrequest.ProtocolVersion = HttpVersion.Version10; 
            wrequest.KeepAlive = true;

            wrequest.Credentials = System.Net.CredentialCache.DefaultCredentials;

            Stream rs = wrequest.GetRequestStream();

            string formdataTemplate = "Content-Disposition: form-data; name=\"{0}\"\r\n\r\n{1}";
            foreach (string key in nvc.Keys)
            {
                rs.Write(boundarybytes, 0, boundarybytes.Length);
                string formitem = string.Format(formdataTemplate, key, nvc[key]);
                byte[] formitembytes = System.Text.Encoding.UTF8.GetBytes(formitem);
                rs.Write(formitembytes, 0, formitembytes.Length);
            }
            rs.Write(boundarybytes, 0, boundarybytes.Length);

            string headerTemplate = "Content-Disposition: form-data; name=\"{0}\"; filename=\"{1}\"\r\nContent-Type: {2}\r\n\r\n";
            string header = string.Format(headerTemplate, paramName, file, contentType);
            byte[] headerbytes = System.Text.Encoding.UTF8.GetBytes(header);
            rs.Write(headerbytes, 0, headerbytes.Length);

            FileStream fileStream = new FileStream(file, FileMode.Open, FileAccess.Read);
            byte[] buffer = new byte[4096];
            int bytesRead = 0;
            while ((bytesRead = fileStream.Read(buffer, 0, buffer.Length)) != 0)
            {
                rs.Write(buffer, 0, bytesRead);
            }
            fileStream.Close();

            byte[] trailer = System.Text.Encoding.ASCII.GetBytes("\r\n--" + boundary + "--\r\n");
            rs.Write(trailer, 0, trailer.Length);
            rs.Close();

            WebResponse wresp = null;
            try
            {
                wresp = wrequest.GetResponse();
                Stream stream2 = wresp.GetResponseStream();
                StreamReader reader2 = new StreamReader(stream2);
                if (reader2.ReadToEnd()== "Файл успешно загружен")
                {
                    JObject json = new JObject();
                    string js = "true";
                    json["Scanned"] = js;
                    ws.Publish(PrintServerID, json);
                }


            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                if (wresp != null)
                {
                    wresp.Close();
                    wresp = null;
                }
            }
            finally
            {
                wrequest = null;
            }
        }
        #endregion

        #region метод для передачи параметров в строку
        public static void UploadFile(string url, string file, string id)
        {
            MessageBox.Show(string.Format("Uploading {0} to {1}", file, url));
            FileInfo fileInf = new FileInfo(file);
            string parameters = "id=" + id + "&original=" + fileInf;
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url + parameters);
            request.Method = "POST";
            byte[] postData = Encoding.UTF8.GetBytes(parameters);
            var stream = request.GetRequestStream();
            stream.Write(postData, 0, postData.Length);
            FileStream fileStream = fileInf.OpenRead();
            byte[] buffer = new byte[4096];
            int bytesRead = 0;
            while ((bytesRead = fileStream.Read(buffer, 0, buffer.Length)) != 0)
            {
                stream.Write(buffer, 0, bytesRead);
            }
            stream.Close();
            fileStream.Close();


            WebResponse wresp = null;
            try
            {
                wresp = request.GetResponse();
                Stream stream2 = wresp.GetResponseStream();
                StreamReader reader2 = new StreamReader(stream2);
                MessageBox.Show(string.Format("File uploaded, server response is: {0}", reader2.ReadToEnd()));
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                if (wresp != null)
                {
                    wresp.Close();
                    wresp = null;
                }
            }
            finally
            {
                request = null;
            }
        }
        #endregion

        #region Создание директории для хранения временных файлов
        private void CreateDir()
        {
            Directory.CreateDirectory(SavePath);
        }

        #endregion

        
    }
}
