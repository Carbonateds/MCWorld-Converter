using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Security;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.WindowsAPICodePack.Dialogs;
using static MS.WindowsAPICodePack.Internal.CoreNativeMethods;

namespace MCWorld_Converter
{
    internal static class Program
    {
        [DllImport("XOREncryptDLL.dll", EntryPoint = "check_file_is_encrypt", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Auto)]
        static extern int FileEncryptionCheck(IntPtr Src);
        [DllImport("XOREncryptDLL.dll", EntryPoint = "decrypt_file", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Auto)]
        static extern int DecryptFile(IntPtr Src, int SrcLength, out IntPtr Buff, out int Length);
        [DllImport("XOREncryptDLL.dll", EntryPoint = "encrypt_file", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Auto)]
        static extern int EncryptFile(IntPtr Src, int SrcLength, out IntPtr Buff, out int Length);

        static string targetSavePath;
        static int type;
        static string logFilePath = Environment.CurrentDirectory + "\\ConvertLog.txt";

        [STAThread]
        static int Main(string[] args)
        {
            File.Delete(logFilePath);

            if (args.Length == 2)
            {
                if (Directory.Exists(args[0]))
                    targetSavePath = args[0];
                else
                    throw new Exception("命令行参数所指定的文件夹路径不存在");

                if (args[1] == "0" || args[1] == "1")
                {
                    switch (args[1])
                    {
                        case "0":
                            type = 0; break;
                        default:
                            type = 1; break;
                    }
                }
                else throw new Exception("0为解密；1为加密");

            }
            else
            {
                Console.Write("选择需要转换的存档文件夹路径：");
                CommonOpenFileDialog dialog = new()
                {
                    IsFolderPicker = true,
                    RestoreDirectory = true,
                    Title = "选择需要转换的存档文件夹路径："
                };
                if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    targetSavePath = dialog.FileName;
                    Console.WriteLine(targetSavePath);
                }
                else
                    return 0;

                bool success = false;
                while(!success)
                {
                    Console.Write("请输入目标任务类型，输入0为解密，1为加密：");
                    char key = (char)Console.Read();
                    switch (key) 
                    {
                        case '0':
                            type = 0;
                            success = true;
                            break;
                        case '1':
                            type = 1;
                            success = true;
                            break;
                        default:
                            Console.WriteLine("请输入正确值");
                            break;
                    }
                }
            }

            if(type == 0)
            {
                DirectoryInfo info = new(targetSavePath);
                List<FileInfo> files = new();
                foreach (DirectoryInfo dirinfo in info.GetDirectories())
                {
                    foreach (FileInfo file in dirinfo.GetFiles())
                        files.Add(file);
                }
                foreach (FileInfo file in info.GetFiles())
                    files.Add(file);

                Console.WriteLine("开始检查目标存档文件夹并生成已加密文件列表：");
                File.AppendAllLines(logFilePath, new string[] { "开始检查目标存档文件夹并生成已加密文件列表：" });
                List<FileInfo> toDecrypt = new();

                foreach (FileInfo file in files)
                {
                    byte[] targetFilePath = Encoding.UTF8.GetBytes(file.FullName);
                    GCHandle handle = GCHandle.Alloc(targetFilePath, GCHandleType.Pinned);
                    IntPtr intPtr = handle.AddrOfPinnedObject();
                    if (FileEncryptionCheck(intPtr) == 1)
                    {
                        toDecrypt.Add(file);
                        Console.WriteLine("文件 " + file.Name + " 已加密");
                        File.AppendAllLines(logFilePath, new string[] { "文件 " + file.Name + " 已加密" });
                    }
                    handle.Free();
                }

                Console.WriteLine("开始解密列表中文件夹：");
                File.AppendAllLines(logFilePath, new string[] { "开始解密列表中文件夹：" });
                foreach (FileInfo file in toDecrypt)
                {
                    byte[] targetFilePath = Encoding.UTF8.GetBytes(file.FullName);
                    GCHandle handle = GCHandle.Alloc(targetFilePath, GCHandleType.Pinned);
                    IntPtr intPtr = handle.AddrOfPinnedObject();
                    IntPtr resultPathPtr = IntPtr.Zero;
                    int Length = 0;
                    if (DecryptFile(intPtr, file.FullName.Length, out resultPathPtr, out Length) != 0)
                    {
                        Console.WriteLine(file.Name + " 解密失败");
                        File.AppendAllLines(logFilePath, new string[] { file.Name + " 解密失败" });
                        return 1;
                    }
                    byte[] resultPath = new byte[Length];
                    Marshal.Copy(resultPathPtr, resultPath, 0, Length);
                    handle.Free();
                    Marshal.FreeHGlobal(resultPathPtr);

                    File.Copy(Encoding.UTF8.GetString(resultPath), file.FullName, true);
                    File.Delete(Encoding.UTF8.GetString(resultPath));
                    Console.WriteLine(file.Name + " 解密成功");
                    File.AppendAllLines(logFilePath, new string[] { file.Name + " 解密成功" });
                }
                Console.WriteLine("给定存档解密已完全成功，未出现错误");
                File.AppendAllLines(logFilePath, new string[] { "给定存档解密已完全成功，未出现错误" });
                MessageBox.Show("给定存档解密已完全成功，未出现错误。请将文件夹直接放入国际版游戏存档文件夹即可。", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);

                return 0;
            }
            else 
            {
                DirectoryInfo info = new(targetSavePath);
                List<FileInfo> files = new();
                foreach (DirectoryInfo dirinfo in info.GetDirectories())
                {
                    foreach (FileInfo file in dirinfo.GetFiles())
                        files.Add(file);
                }
                foreach (FileInfo file in info.GetFiles())
                    files.Add(file);

                Console.WriteLine("开始检查目标存档文件夹并生成需加密文件列表：");
                File.AppendAllLines(logFilePath, new string[] { "开始检查目标存档文件夹并生成需加密文件列表：" });
                List<FileInfo> toEncrypt = new();

                foreach(FileInfo file in files)
                {
                    if (file.Name.Contains(".ldb") ||
                       file.Name.Contains("CURRENT") ||
                       file.Name.Contains("MANIFEST"))
                    {
                        Console.WriteLine(file.Name + " 需加密");
                        toEncrypt.Add(file);
                    }
                }

                Console.WriteLine("开始加密列表中文件夹：");
                File.AppendAllLines(logFilePath, new string[] { "开始加密列表中文件夹：" });
                foreach (FileInfo file in toEncrypt)
                {
                    byte[] targetFilePath = Encoding.UTF8.GetBytes(file.FullName);
                    GCHandle handle = GCHandle.Alloc(targetFilePath, GCHandleType.Pinned);
                    IntPtr intPtr = handle.AddrOfPinnedObject();
                    IntPtr resultPathPtr = IntPtr.Zero;
                    int Length = 0;
                    if (EncryptFile(intPtr, file.FullName.Length, out resultPathPtr, out Length) != 0)
                    {
                        Console.WriteLine(file.Name + " 加密失败");
                        File.AppendAllLines(logFilePath, new string[] { file.Name + " 加密失败" });
                        return 1;
                    }
                    byte[] resultPath = new byte[Length];
                    Marshal.Copy(resultPathPtr, resultPath, 0, Length);
                    handle.Free();
                    Marshal.FreeHGlobal(resultPathPtr);

                    File.Copy(Encoding.UTF8.GetString(resultPath), file.FullName, true);
                    File.Delete(Encoding.UTF8.GetString(resultPath));
                    Console.WriteLine(file.Name + " 加密成功");
                    File.AppendAllLines(logFilePath, new string[] { file.Name + " 加密成功" });
                }
                File.WriteAllText(targetSavePath + "\\netease_world_behavior_packs.json", "null");
                File.WriteAllText(targetSavePath + "\\netease_world_recource_packs.json", "null");
                Console.WriteLine("给定存档加密已完全成功，未出现错误");
                File.AppendAllLines(logFilePath, new string[] { "给定存档加密已完全成功，未出现错误" });
                MessageBox.Show("给定存档加密已完全成功，未出现错误。请将存档文件导入至中国版即可", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);

                return 0; 
            }
        }
    }
}
