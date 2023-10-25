using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml.Linq;
using System.Xml;
using Path = System.IO.Path;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Formatting = Newtonsoft.Json.Formatting;
using System.Runtime.InteropServices;
using System.Security;

namespace Updater
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private HttpClient httpClient;
        private string ZipInstaller;
        private string InstallfolderPath;
        private string configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
        private Configuration config;

        public MainWindow()
        {
            InitializeComponent();
            httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "GithubReleaseDownloader");
            Initialize();
        }

        public async void Initialize()
        {
            config = Configuration.Load(configFilePath);
            await LauncherUpdateChecker("DrapNard", "InfiniteFusion-Launcher");
        }


        public async Task<long> GetFileSizeAsync(string url)
        {
            HttpResponseMessage response = await httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            return response.Content.Headers.ContentLength ?? 0;
        }

        public async Task ReleaseDownloaderAsync(string owner, string repo, string tree)
        {
            string archiveFormat = "zip";
            string apiUrl = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";

            try
            {
                HttpResponseMessage response = await httpClient.GetAsync(apiUrl);
                response.EnsureSuccessStatusCode();

                string releaseInfo = await response.Content.ReadAsStringAsync();
                var root = JObject.Parse(releaseInfo);
                string releaseName = root["name"].ToString();
                Console.WriteLine($"Latest release name: {releaseName}");

                Stream archiveStream = await httpClient.GetStreamAsync($"https://github.com/{owner}/{repo}/archive/refs/heads/{tree}.{archiveFormat}");


                string archiveFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{repo}-latest.{archiveFormat}");
                ZipInstaller = archiveFilePath;
                Console.WriteLine($"Downloading the latest release source code of {owner} has begun");
                using (FileStream fileStream = new FileStream(archiveFilePath, FileMode.Create))
                {
                    const int bufferSize = 8192;
                    var buffer = new byte[bufferSize];
                    int bytesRead;
                    long totalBytesRead = 0;

                    while ((bytesRead = await archiveStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead);
                        totalBytesRead += bytesRead;
                    }
                }

                Console.WriteLine($"The latest release source code has been downloaded: {archiveFilePath}");
                Console.WriteLine($"The name of the latest release has been saved in: {releaseName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error downloading the source code: {ex.Message}");
            }
        }

        public async Task DecompressZip(string zipFilePath, string extractPath)
        {
            if (!File.Exists(zipFilePath))
            {
                Console.WriteLine("The .zip file does not exist.");
                return;
            }

            if (!Directory.Exists(extractPath))
            {
                Console.WriteLine("The destination folder does not exist.");
                return;
            }

            try
            {
                using (ZipArchive archive = ZipFile.OpenRead(zipFilePath))
                {
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        string entryOutputPath = Path.Combine(extractPath, entry.FullName);

                        if (entry.FullName.EndsWith("/") || entry.FullName.EndsWith("\\"))
                        {
                            Directory.CreateDirectory(entryOutputPath);
                        }
                        else
                        {
                            entry.ExtractToFile(entryOutputPath, true);
                        }
                    }
                }
                Console.WriteLine("Decompression completed.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error decompressing the .zip file: {ex.Message}");
            }
        }

        public void RenameFolder(string currentFolderPath, string newFolderName)
        {
            if (!Directory.Exists(currentFolderPath))
            {
                Console.WriteLine("The current folder does not exist.");
                return;
            }

            if (string.IsNullOrWhiteSpace(newFolderName))
            {
                Console.WriteLine("The new folder name is not valid.");
                return;
            }

            string newFolderPath = Path.Combine(Path.GetDirectoryName(currentFolderPath), newFolderName);

            if (Directory.Exists(newFolderPath))
            {
                Console.WriteLine("A folder with the new name already exists.");
                return;
            }

            try
            {
                Directory.Move(currentFolderPath, newFolderPath);
                Console.WriteLine("Folder renamed successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error renaming the folder: {ex.Message}");
            }
        }

        public string[] FindFoldersByPartialName(string parentDirectory, string partialName)
        {
            if (!Directory.Exists(parentDirectory))
            {
                Console.WriteLine("The parent directory does not exist.");
                return new string[0];
            }

            try
            {
                string[] matchingFolders = Directory.GetDirectories(parentDirectory, "*" + partialName + "*");
                return matchingFolders;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error searching for folders: {ex.Message}");
                return new string[0];
            }
        }

        public async Task DeleteZipFile(string zipFilePath)
        {
            if (File.Exists(zipFilePath))
            {
                try
                {
                    File.Delete(zipFilePath);
                    Console.WriteLine("The .zip file has been successfully deleted.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error deleting the .zip file: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine("The .zip file does not exist.");
            }
        }

        private static void CopyFilesRecursively(DirectoryInfo source, DirectoryInfo destination, List<string> filesToExclude)
        {
            if (!destination.Exists)
            {
                destination.Create();
            }

            foreach (FileInfo file in source.GetFiles())
            {
                if (!filesToExclude.Contains(file.Name))
                {
                    string destinationFilePath = Path.Combine(destination.FullName, file.Name);
                    file.CopyTo(destinationFilePath, true);
                    Console.WriteLine("File Copied " + file + " to " + destinationFilePath);
                }
                else
                {
                    Console.WriteLine("Skipped copying " + file.Name);
                }
            }

            foreach (DirectoryInfo subDirectory in source.GetDirectories())
            {
                string destinationSubDirectoryPath = Path.Combine(destination.FullName, subDirectory.Name);
                CopyFilesRecursively(subDirectory, new DirectoryInfo(destinationSubDirectoryPath), filesToExclude);
            }
        }

        public void StartLauncher(bool start)
        {
            if (start)
            {
                string programPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Pokémon Infinite Fusion Launcher.exe");

                try
                {
                    Process.Start(programPath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error starting the program: " + ex.Message);
                }
            }
            else
            {
                string processName = "Pokémon Infinite Fusion Launcher";

                Process[] processes = Process.GetProcessesByName(processName);

                foreach (Process process in processes)
                {
                    process.Kill();
                }
            }
        }

        public async Task DelFile()
        {
            // Spécifiez le chemin du répertoire que vous souhaitez nettoyer
            string directoryPath = AppDomain.CurrentDomain.BaseDirectory;

            // Définissez les critères pour les fichiers à conserver
            string[] fichiersÀConserver = new string[] { "config.json", "Updater.exe", "Updater.dll", "Updater.runtimeconfig.json", "Updater.deps.json", "Newtonsoft.Json.dll" };

            
                string[] fichiers = Directory.GetFiles(directoryPath);

                foreach (string fichier in fichiers)
                {
                    // Vérifiez si le nom du fichier n'est pas dans la liste des fichiers à conserver
                    if (Array.IndexOf(fichiersÀConserver, Path.GetFileName(fichier)) == -1)
                    {
                        // Supprimez le fichier
                        File.Delete(fichier);
                        Console.WriteLine($"Le fichier {fichier} a été supprimé.");
                    }
                }

                Console.WriteLine("Nettoyage terminé.");
            
        }

        public async Task LauncherUpdateChecker(string owner, string repo)
        {
            try
            {
                string apiUrl = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";
                HttpResponseMessage response = await httpClient.GetAsync(apiUrl);
                response.EnsureSuccessStatusCode();
                string releaseName;
                string releaseInfo = await response.Content.ReadAsStringAsync();
                var root = JObject.Parse(releaseInfo);
                releaseName = root["name"].ToString();
                Console.WriteLine($"Latest release name: {releaseName}");
                Console.WriteLine($"Actuel release name: {config.Version}");
                if (config.Version != null)
                {
                    if (config.Version != releaseName)
                    {
                        MessageBoxResult result = System.Windows.MessageBox.Show("A new update of the launcher is available.", "Do you want to install it?", MessageBoxButton.YesNo, MessageBoxImage.Question);

                        if (result == MessageBoxResult.Yes)
                        {
                            StartLauncher(false);
                            Main.Visibility = Visibility.Visible;

                            string gameDirectory = AppDomain.CurrentDomain.BaseDirectory;
                            string tempDirectory = Path.Combine(gameDirectory, "UpdateTemp");

                            try
                            {
                                if (!Directory.Exists(tempDirectory))
                                    Directory.CreateDirectory(tempDirectory);

                                await ReleaseDownloaderAsync("DrapNard", "InfiniteFusion-Launcher", "Update");
                                Thread.Sleep(3000);
                                await DecompressZip(ZipInstaller, tempDirectory);
                                Thread.Sleep(3000);

                                string parentDirectory = tempDirectory;
                                string partialName = "InfiniteFusion-Launcher-Update";
                                string[] matchingFolders = FindFoldersByPartialName(parentDirectory, partialName);

                                if (matchingFolders.Length > 0)
                                {
                                    InstallfolderPath = matchingFolders[0];
                                }
                                await DelFile();

                                List<string> FileExecptions = new List<string>();
                                FileExecptions.Add("Newtonsoft.Json.dll");

                                CopyFilesRecursively(new DirectoryInfo(InstallfolderPath), new DirectoryInfo(gameDirectory), FileExecptions);
                                Thread.Sleep(3000);
                            }
                            catch (Exception ex)
                            {
                                System.Windows.MessageBox.Show("Error during the update: " + ex.Message);
                            }
                            finally
                            {
                                Directory.Delete(tempDirectory, true);
                                await DeleteZipFile(ZipInstaller);

                                config.Version = releaseName;
                                config.Save(configFilePath);

                                Thread.Sleep(3000);
                                StartLauncher(true);

                                Application.Current.Shutdown();
                            }
                        }
                        else if (result == MessageBoxResult.No)
                        {
                            Application.Current.Shutdown();
                            return;
                        }
                    }
                    else
                    {

                        Application.Current.Shutdown();
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking for updates: {ex.Message}");
                System.Windows.MessageBox.Show($"Error checking for updates: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            Application.Current.Shutdown();
        }
        public class Configuration
        {
            public string CloseMode { get; set; }
            public string Version { get; set; }
            public string GamePath { get; set; }
            public string GameVersion { get; set; }
            public string GameSpritePack { get; set; }

            public static Configuration Load(string filePath)
            {
                if (File.Exists(filePath))
                {
                    string json = File.ReadAllText(filePath);
                    return JsonConvert.DeserializeObject<Configuration>(json);
                }

                return new Configuration();
            }

            public void Save(string filePath)
            {
                string json = JsonConvert.SerializeObject(this, Formatting.Indented);
                File.WriteAllText(filePath, json);
            }
        }

        [SuppressUnmanagedCodeSecurity]
        public static class ConsoleManager
        {
            private const string Kernel32_DllName = "kernel32.dll";

            [DllImport(Kernel32_DllName)]
            private static extern bool AllocConsole();

            [DllImport(Kernel32_DllName)]
            private static extern bool FreeConsole();

            [DllImport(Kernel32_DllName)]
            private static extern IntPtr GetConsoleWindow();

            [DllImport(Kernel32_DllName)]
            private static extern int GetConsoleOutputCP();

            public static bool HasConsole
            {
                get { return GetConsoleWindow() != IntPtr.Zero; }
            }

            /// <summary>
            /// Creates a new console instance if the process is not attached to a console already.
            /// </summary>
            public static void Show()
            {
                //#if DEBUG
                if (!HasConsole)
                {
                    AllocConsole();
                }
                //#endif
            }

            /// <summary>
            /// If the process has a console attached to it, it will be detached and no longer visible. Writing to the System.Console is still possible, but no output will be shown.
            /// </summary>
            public static void Hide()
            {
                //#if DEBUG
                if (HasConsole)
                {
                    SetOutAndErrorNull();
                    FreeConsole();
                }
                //#endif
            }

            public static void Toggle()
            {
                if (HasConsole)
                {
                    Hide();
                }
                else
                {
                    Show();
                }
            }
            static void SetOutAndErrorNull()
            {
                Console.SetOut(TextWriter.Null);
                Console.SetError(TextWriter.Null);
            }
        }
    }
}

