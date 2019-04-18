using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.IO;

namespace PrintServer
{
    public class PrintServerSettings
    {

        private string _tempDir;
        private string _downloadUrl;
        private string _uploadUrl;
        private string _printServerId;
        private string _host;
        private string _port;
        private string _protocol;

        public string TempDir
        {
            get
            {
                return _tempDir;
            }
            set
            {
                _tempDir = value;
            }
        }
        public string DownloadUrl
        {
            get
            {
                return _downloadUrl;
            }
            set
            {
                _downloadUrl = value;
            }
        }
        public string UploadUrl
        {
            get
            {
                return _uploadUrl;
            }
            set
            {
                _uploadUrl = value;
            }
        }
        public string PrintServerId
        {
            get
            {
                return _printServerId;
            }
            set
            {
                _printServerId = value;
            }
        }
        public string Host
        {
            get
            {
                return _host;
            }
            set
            {
                _host = value;
            }
        }

        public string Port
        {
            get
            {
                return _port;
            }
            set
            {
                _port = value;
            }
        }
        public string Protocol
        {
            get
            {
                return _protocol;
            }
            set
            {
                _protocol = value;
            }
        }
        public string getUri()
        {
            return (_protocol == "ssl" ? "wss" : "ws") + "://" + _host + ":" + _port + "/";
        }
    }

    /// <summary>
    /// Логика взаимодействия для App.xaml
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// Запуск одной копии приложения
        /// </summary>
        System.Threading.Mutex mut;
        private PrintServerSettings appSesstings = new PrintServerSettings();

        /// <summary>
        /// Настройки приложения
        /// </summary>
        public PrintServerSettings Settings
        {
            get
            {
                return this.appSesstings;
            }
        }

        /// <summary>
        /// Конструктор
        /// </summary>
        public App()
            : base()
        {
            string ini = AppDomain.CurrentDomain.BaseDirectory + @"printserver.ini";

            try
            {
                IniParser parser = new IniParser(ini);

                appSesstings.TempDir = parser.GetSetting("server", "tempdir");
                appSesstings.DownloadUrl = parser.GetSetting("server", "downloadUrl");
                appSesstings.UploadUrl = parser.GetSetting("server", "uploadURL");
                appSesstings.PrintServerId = parser.GetSetting("client", "identifier");
                appSesstings.Host = parser.GetSetting("server", "host");
                appSesstings.Port = parser.GetSetting("server", "port");
                appSesstings.Protocol = parser.GetSetting("server", "protocol");
            }
            catch (FileNotFoundException fn)
            {
                MessageBox.Show("Отсутствует файл конфигурации, приложение будет закрыто.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                Shutdown();
            }
            catch (Exception fn)
            {
                MessageBox.Show("Файл конфигурации повреждён, приложение будет закрыто", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }

        /// <summary>
        /// При старте приложения
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            bool createdNew;
            string mutName = "PrintServer";
            mut = new System.Threading.Mutex(true, mutName, out createdNew);
            if (!createdNew)
            {
                Shutdown();
            }
        }
    }
}
