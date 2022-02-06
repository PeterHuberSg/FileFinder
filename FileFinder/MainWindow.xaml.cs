using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;


namespace FileFinder {


  /// <summary>
  /// Interaction logic for MainWindow.xaml
  /// </summary>
  public partial class MainWindow: Window {


    public MainWindow() {
      InitializeComponent();

      FilterTextBox.Text = ".cs;.xaml";
      FilterTextBox.Text = ".cs";

      //DirectoryTextBox.Text = @"D:\Visual Studio 2010\Projects\";
      DirectoryTextBox.Text = @"C:\Users\peter\Source\Repos";

      //SearchTextBox.Text = "window.Register(this);";
      //SearchTextBox.Text = "Ultra";
      //SearchTextBox.Text = "StandardColor";
      //SearchTextBox.Text = "BigBitSet";
      //SearchTextBox.Text = "Generic";
      //SearchTextBox.Text = "testMethodOverFlow"; 
      SearchTextBox.Text = "Environment.GetFolderPath";
      SearchTextBox.Text = "CustomControlBase";
      




      Loaded += mainWindow_Loaded;
      SearchButton.Click += searchButton_Click;
      ResultTextBox.MouseDoubleClick += ResultTextBox_MouseDoubleClick;
    }


    const string linePreamble = " >==";


    private void ResultTextBox_MouseDoubleClick(object sender, MouseButtonEventArgs e) {
      //==> C:\Users\peter\Source\Repos\Life\LifeLib\LifeGrid.cs: 211
      var text = ResultTextBox.Text;
      var startIndex = ResultTextBox.SelectionStart;
      var endIndex = int.MinValue;
      var searchEndIndex = startIndex;
      while (startIndex>=0) {
        var c = text[startIndex--];
        if (c=='\n') {
          //start of line found
          while (searchEndIndex<text.Length) {
            c = text[searchEndIndex];
            if (c=='\\') {
              endIndex = searchEndIndex;
            }
            if (c=='\r') {
              searchEndIndex = text.LastIndexOf('\\', searchEndIndex);
              if (searchEndIndex<0) {
                //user did not click on a line with a file
                return;
              }
              var r = text[(startIndex+6)..(searchEndIndex)];
              try {
                _=Process.Start("explorer.exe", r);
              } catch (Exception ex) {

                System.Diagnostics.Debugger.Break();
              }
              return;
            }
            searchEndIndex++;
          }
          return;
        }
      }

      //no start of line found
      return;
    }

    private void mainWindow_Loaded(object sender, RoutedEventArgs e) {
      DirectoryTextBox.Focus();
    }


    string searchString = "";
    byte[] searchBytes = new byte[0];
    string filterString = "";
    string[] fileExtensionsFilter = new string[0];
    bool isCancelled = false;


    private async void searchButton_Click(object sender, RoutedEventArgs e) {
      try {
        if ((string)SearchButton.Content == "_Search") {
          SearchButton.Content = "S_top";
          isCancelled = false;
        } else {
          isCancelled = true;
          return;
        }
        var searchPath = DirectoryTextBox.Text;
        searchString = SearchTextBox.Text;
        filterString = FilterTextBox.Text;
        fileExtensionsFilter = filterString.ToLowerInvariant().Split(';');

        if (string.IsNullOrWhiteSpace(searchPath)) {
          ResultTextBox.Text += $"Error: Directory cannot be empty." + Environment.NewLine;
          return;
        }

        if (string.IsNullOrWhiteSpace(searchString)) {
          ResultTextBox.Text += $"Error: Search string cannot be empty." + Environment.NewLine;
          return;
        }
        searchBytes = new byte[searchString.Length];
        int byteIndex = 0;
        for (int charIndex = 0; charIndex < searchString.Length; charIndex++) {
          char searchChar = searchString[charIndex];
          if (searchChar>=128) {
            ResultTextBox.Text += $"Error: Cannot only search for characters smaller 128, but search character was '{searchChar}'." + Environment.NewLine;
            return;
          }
          searchBytes[byteIndex++] = (byte)searchChar;
        }

        var rootDirectory = new DirectoryInfo(searchPath);
        if (!rootDirectory.Exists) {
          ResultTextBox.Text += $"Error: cannot find directory: {searchPath}." + Environment.NewLine;
        } else {
          var progress = new Progress<string>(s =>
          {
            ResultTextBox.Text =  s;
            ResultTextBox.ScrollToEnd();
          });
          await Task.Run(() => searchRootDirectory(rootDirectory, progress));
        }
      } catch (Exception ex) {
        ResultTextBox.Text += $"Exception: {ex.Message}." + Environment.NewLine;
      }
      SearchButton.Content = "_Search";
    }


    IProgress<string>? progress;


    private void searchRootDirectory(DirectoryInfo directory, IProgress<string> progress) {
      this.progress = progress;
      startReport();
      report($"Search string '{searchString}' using filter '{filterString}' in directory : {directory.FullName}" +
        Environment.NewLine, true);

      searchDirectory(directory);
      finalReport();
    }


    private bool searchDirectory(DirectoryInfo directory) {
      if (directory.FullName.Contains("filefinder", StringComparison.InvariantCultureIgnoreCase)) {
        return true;
      }
      report(directory.FullName);
      try {
        foreach (FileInfo file in directory.GetFiles()) {
          if (isCancelled) {
            return false;
          }

          if (fileExtensionsFilter!=null) {
            //skip files with extension missing in fileExtensionsFilter
            string fileEntension = file.Extension.ToLowerInvariant();
            bool getsFiltered = true;
            foreach (string filter in fileExtensionsFilter) {
              if (fileEntension==filter) getsFiltered=false;
            }
            if (getsFiltered) {
              continue;
            }
          }

          int searchByteIndex = 0;
          try {
            using FileStream fileStream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read);
            int readByte;
            while (0 <= (readByte = fileStream.ReadByte())) {
              if (isCancelled) {
                return false;
              }
              if (readByte==searchBytes[searchByteIndex]) {
                searchByteIndex++;
                if (searchByteIndex>=searchBytes.Length) {
                  report($"==> {file.FullName}: {fileStream.Position}", true);
                  //Thread.Sleep(10);
                  break;
                }
              } else {
                searchByteIndex = 0;
              }
            }
          } catch (Exception ex) {
            report($"Cannot read file {file.Name}, Exception: {ex.Message}", true);
          }
        }
      } catch (Exception ex) {
        report($"Cannot access files in directory {directory.Name}, Exception: {ex.Message}", true);
      }

      try {
        foreach (DirectoryInfo childDirectory in directory.GetDirectories()) {
          if (childDirectory.FullName==@"C:\Users\peter\Source\Repos\Try") continue;

          if (isCancelled) {
            return false;
          }
          if (!searchDirectory(childDirectory)) return false;
        }
      } catch (Exception ex) {
        report($"Cannot access directories in directory {directory.Name}, Exception: {ex.Message}, true");
      }
      return true;
    }


    string tempLine = "";
    readonly StringBuilder stringBuilder = new StringBuilder();
    Stopwatch watch = new Stopwatch();
    long lastTick = 0;
    readonly long waitPeriod = Stopwatch.Frequency / 2;


    private void startReport() {
      lastTick = 0;
      watch.Start();
    }


    private void report(string text, bool isNewLine = false) {
      if (isNewLine) {
        stringBuilder.AppendLine(text);
      } else {
        tempLine = text;
      }

      if (watch.ElapsedTicks>lastTick + waitPeriod) {
        lastTick += waitPeriod;
        if (tempLine.Length>0) {
          progress!.Report(stringBuilder.ToString() + Environment.NewLine + tempLine);
        } else {
          progress!.Report(stringBuilder.ToString());
        }
      }
    }


    private void finalReport() {
      progress!.Report(stringBuilder.ToString());
      stringBuilder.Clear();
      watch.Stop();
    }
  }
}
