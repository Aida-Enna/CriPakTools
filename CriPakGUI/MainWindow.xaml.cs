﻿using System;
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
using System.Diagnostics;
using System.Windows.Shapes;
using System.IO;
using Ookii.Dialogs.Wpf;
using System.Threading;
using System.Windows.Threading;
using LibCPK;

namespace CriPakGUI
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            SetBasicPrefs();
        }

        private void SetBasicPrefs()
        {
            menu_savefiles.IsEnabled = false;
            menu_importAssets.IsEnabled = false;
            progressbar0.Maximum = 100;
            myPackage.basePath = @"C:/";
        }
        private void menu_openfile_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Loading cpk");
            string fName;
            string baseName;
            VistaOpenFileDialog openFileDialog = new VistaOpenFileDialog();
            openFileDialog.InitialDirectory = "";
            openFileDialog.Filter = "Criware CPK|*.cpk";
            openFileDialog.RestoreDirectory = true;
            openFileDialog.FilterIndex = 1;
            if (openFileDialog.ShowDialog().Value)
            {
                fName = openFileDialog.FileName;
                baseName = System.IO.Path.GetFileName(fName);
                status_cpkname.Content = baseName;
                beginLoadCPK(fName);
                button_extract.IsEnabled = true;
                button_importassets.IsEnabled = true;
                
            }
        }

        private void beginLoadCPK(string fName)
        {
            ThreadPool.QueueUserWorkItem(o =>
            {
                Application.Current.Dispatcher.BeginInvoke(
                    DispatcherPriority.SystemIdle,
                    new Action(() =>
                    {
                        cpkwrapper cpk = new cpkwrapper(fName);
                        status_cpkmsg.Content = string.Format("{0} file(s) registered.", cpk.nums);
                        datagrid_cpk.ItemsSource = cpk.table;

                        menu_importAssets.IsEnabled = true;
                        menu_savefiles.IsEnabled = true;
                        myPackage.basePath = System.IO.Path.GetDirectoryName(fName);
                        myPackage.baseName = System.IO.Path.GetFileName(fName);

                    } )
               );
            });  
        }


        private void menu_importAssets_Click(object sender, RoutedEventArgs e)
        {
            CpkPatcher patcherWindow = new CpkPatcher();
            patcherWindow.ShowDialog();
        }


        private delegate void progressbarDelegate(float no);

        private delegate void datagridDelegate(bool value);

        private void updateDatagrid(bool value)
        {
            datagrid_cpk.IsEnabled = value;
            button_extract.IsEnabled = value;
            button_importassets.IsEnabled = value;
        }

        private void updateprogressbar(float no)
        {
            progressbar0.Value = no;
        }

        private void menu_savefiles_Click(object sender, RoutedEventArgs e)
        {
            VistaFolderBrowserDialog saveFilesDialog = new VistaFolderBrowserDialog();
            saveFilesDialog.SelectedPath = myPackage.basePath;
            if (saveFilesDialog.ShowDialog().Value)
            {
                Debug.Print(saveFilesDialog.SelectedPath + "/" + myPackage.baseName + "_unpacked");
                ThreadPool.QueueUserWorkItem(new WaitCallback(beginExtractCPK), saveFilesDialog.SelectedPath);
                
            }

        }

        private void beginExtractCPK(object foutDir)
        {
            string outDir;
            outDir = (string)(foutDir + "/" + myPackage.baseName + "_unpacked");
            if (myPackage.cpk != null)
            {
                if (!Directory.Exists(outDir))
                {
                    Directory.CreateDirectory(outDir);
                }
                BinaryReader oldFile = new BinaryReader(File.OpenRead(myPackage.cpk_name));
                List<FileEntry> entries = null;

                entries = myPackage.cpk.FileTable.Where(x => x.FileType == "FILE").ToList();

                if (entries.Count == 0)
                {
                    Debug.Print("err while extracting.");
                    oldFile.Close();
                    return;
                }

                int i = 0;

                this.Dispatcher.Invoke(new datagridDelegate(updateDatagrid), new object[] { (bool)false });

                while (i < entries.Count)
                {
                    this.Dispatcher.Invoke(new progressbarDelegate(updateprogressbar), new object[] { (float)i / (float)entries.Count * 100f });//异步委托

                    if (!String.IsNullOrEmpty((string)entries[i].DirName))
                    {
                        Directory.CreateDirectory(outDir + "/" + entries[i].DirName.ToString());
                    }


                    oldFile.BaseStream.Seek((long)entries[i].FileOffset, SeekOrigin.Begin);

                    string isComp = Encoding.ASCII.GetString(oldFile.ReadBytes(8));
                    oldFile.BaseStream.Seek((long)entries[i].FileOffset, SeekOrigin.Begin);

                    byte[] chunk = oldFile.ReadBytes(Int32.Parse(entries[i].FileSize.ToString()));

                    if (isComp == "CRILAYLA")
                    {
                        int size = Int32.Parse((entries[i].ExtractSize ?? entries[i].FileSize).ToString());

                        if (size != 0)
                        {
                            chunk = myPackage.cpk.DecompressLegacyCRI(chunk, size);
                        }
                    }

                    Debug.WriteLine(" FileName :{0}\n    FileOffset:{1:x8}    ExtractSize:{2:x8}   ChunkSize:{3:x8} {4}",
                                                                entries[i].FileName.ToString(),
                                                                (long)entries[i].FileOffset,
                                                                entries[i].ExtractSize,
                                                                entries[i].FileSize,
                                                                ((float)i / (float)entries.Count) * 100f);

                    File.WriteAllBytes(outDir + "/" + ((entries[i].DirName != null) ? entries[i].DirName + "/" : "") + entries[i].FileName.ToString(), chunk);
                    i += 1;
                }
                oldFile.Close();
                this.Dispatcher.Invoke(new progressbarDelegate(updateprogressbar), new object[] { 100f });//异步委托
                this.Dispatcher.Invoke(new datagridDelegate(updateDatagrid), new object[] { (bool)true });//异步委托
                MessageBox.Show("Extraction Complete.");

            }

        }

        private void button_extract_Click(object sender, RoutedEventArgs e)
        {
            VistaFolderBrowserDialog saveFilesDialog = new VistaFolderBrowserDialog();
            saveFilesDialog.SelectedPath = myPackage.basePath + "/";
            if (saveFilesDialog.ShowDialog().Value)
            {
                Debug.Print(saveFilesDialog.SelectedPath + "/" + myPackage.baseName + "_unpacked");
                ThreadPool.QueueUserWorkItem(new WaitCallback(beginExtractCPK), saveFilesDialog.SelectedPath);

            }
        }

        private void button_importassets_Click(object sender, RoutedEventArgs e)
        {
            CpkPatcher patcherWindow = new CpkPatcher();
            patcherWindow.ShowDialog();


        }

        private void menu_aboutgui_Click(object sender, RoutedEventArgs e)
        {
            WindowAboutGUI aboutwindow = new WindowAboutGUI();
            aboutwindow.ShowDialog();
        }

    }
}
