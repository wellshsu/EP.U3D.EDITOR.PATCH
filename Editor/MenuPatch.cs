//---------------------------------------------------------------------//
//                    GNU GENERAL PUBLIC LICENSE                       //
//                       Version 2, June 1991                          //
//                                                                     //
// Copyright (C) Wells Hsu, wellshsu@outlook.com, All rights reserved. //
// Everyone is permitted to copy and distribute verbatim copies        //
// of this license document, but changing it is not allowed.           //
//                  SEE LICENSE.md FOR MORE DETAILS.                   //
//---------------------------------------------------------------------//
using UnityEditor;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System;
using System.IO;
using PBT;
using System.Collections.Generic;
using EP.U3D.EDITOR.BASE;
using Preferences = EP.U3D.LIBRARY.BASE.Preferences;

namespace EP.U3D.EDITOR.PATCH
{
    [InitializeOnLoad]
    public static class MenuPatch
    {
        [MenuItem(Constants.MENU_PATCH_PUSH_PATCH)]
        public static void PushPatch()
        {
            if (string.IsNullOrEmpty(Preferences.Instance.PushPatch) == false)
            {
                string[] strs = Preferences.Instance.PushPatch.Split(':');
                if (strs.Length == 2)
                {
                    string host = strs[0];
                    int port;
                    int.TryParse(strs[1], out port);
                    Push(host, port);
                }
                else
                {
                    Helper.ShowToast("Please config PushPath in Preferences.");
                    EditorApplication.ExecuteMenuItem(Constants.MENU_WIN_PREF);
                }
            }
            else
            {
                Helper.ShowToast("Please config PushPath in Preferences.");
                EditorApplication.ExecuteMenuItem(Constants.MENU_WIN_PREF);
            }
        }

        [MenuItem(Constants.MENU_PATCH_BUNDLE_PATCH)]
        public static void BundlePatch()
        {
            string toast = "";
            string dir = Constants.BUILD_PATCH_ROOT;
            string[] plats = new string[] { "Windows", "Android", "iOS" };
#if EFRAME_ILR
            plats = ValidiateILR(new string[] { "Windows", "Android", "iOS" });
            if (plats.Length == 0)
            {
                toast = "No patch is avaliable for zipping, all platform's ilr aren't encrypted.";
                Helper.ShowToast(toast);
                Helper.Log(toast);
                return;
            }
#endif
#if EFRAME_LUA
            plats = ValidiateLua(plats);
            if (plats.Length == 0)
            {
                toast = "No patch is avaliable for zipping, all platform's lua aren't encrypted.";
                Helper.ShowToast(toast);
                Helper.Log(toast);
                return;
            }
#endif
#if EFRAME_ILR || EFRAME_LUA
            string tmp = Constants.BUILD_PATCH_ROOT.Substring(0, Constants.BUILD_PATCH_ROOT.LastIndexOf("/")) + "_TEMP/";
            string pstr = "(";
            for (int i = 0; i < plats.Length; i++)
            {
                var plat = plats[i];
                pstr += plat;
                if (i < plats.Length - 1) pstr += ", ";
                Helper.CopyDirectory(dir + plat, tmp + plat + "/");
            }
            pstr += ")";
            string zip = Helper.StringFormat("{0}{1}.zip", Constants.BUILD_PATCH_ROOT, CalculatePatchName());
            Helper.Zip(tmp, zip, new List<string>() { ".zip", ".rar" }, Constants.ZIP_CMD);
            toast = Helper.StringFormat("Bundle patch done, platforms: {0}.", pstr);
            Helper.ShowToast(toast);
            Helper.Log("[FILE@{0}] {1}", zip, toast);
            Helper.DeleteDirectory(tmp);
#endif
        }

        private static string CalculatePatchName()
        {
            string dir = Constants.BUILD_PATCH_ROOT;
            string prefix = Helper.StringFormat("{0}_Patch_{1}_", Constants.PROJ_NAME, Constants.LOCAL_VERSION);
            int maxIndex = 1;
            if (Directory.Exists(dir))
            {
                DirectoryInfo binDirectory = new DirectoryInfo(dir);
                FileSystemInfo[] fileInfos = binDirectory.GetFiles();
                if (fileInfos != null && fileInfos.Length > 0)
                {
                    for (int i = 0; i < fileInfos.Length; i++)
                    {
                        FileSystemInfo fileInfo = fileInfos[i];
                        if (fileInfo == null) continue;
                        string fileName = fileInfo.Name;
                        if (string.IsNullOrEmpty(fileName)) continue;
                        if (fileName.EndsWith(".zip") == false) continue;
                        if (fileName.StartsWith(prefix) == false) continue;
                        fileName = fileName.Replace(".zip", "");
                        fileName = fileName.Replace(prefix, "");
                        int index = 0;
                        int.TryParse(fileName, out index);
                        if (index >= maxIndex)
                        {
                            maxIndex = index + 1;
                        }
                    }
                }
            }

            return prefix + maxIndex;
        }

        private static string[] ValidiateILR(string[] inputs)
        {
            // TODO
            List<string> valids = new List<string>();
            valids.AddRange(inputs);
            return valids.ToArray();
        }

        public static string[] ValidiateLua(string[] inputs)
        {
            List<string> valids = new List<string>();
            for (int i = 0; i < inputs.Length; i++)
            {
                var plat = inputs[i];
                string core = $"{Constants.BUILD_PATCH_ROOT}{plat}/{Constants.BUILD_LUA_BUNDLE_PATH.Replace(Constants.BUILD_PATCH_PATH, "")}x64/libs{Constants.LUA_BUNDLE_FILE_EXTENSION}";
                if (File.Exists(core))
                {
                    string coretxt = File.ReadAllText(core);
                    if (coretxt.Contains("topameng@gmail.com"))
                    {
                        Helper.LogError("There has unencrypted script in {0}, please switch to LiveMode and recompile script.", plat);
                    }
                    else
                    {
                        valids.Add(plat);
                    }
                }
            }
            return valids.ToArray();
        }

        public static byte[] HEADER = new byte[EditorNet.MSG_HEAD_LENGTH];
        public static Socket Socket;
        public static Thread MainThread;
        public static double CurrentTime;
        public static double LastTime;

        static MenuPatch()
        {
            EditorEvtcat.OnUpdateEvent += () =>
            {
                if (EditorApplication.isCompiling)
                {
                    Reset();
                }
                else
                {
                    CurrentTime = EditorApplication.timeSinceStartup;
                    if (LastTime != 0)
                    {
                        double deltaTime = CurrentTime - LastTime;
                        if (deltaTime > 40)
                        {
                            Helper.LogError("Push patch timeout.");
                            Helper.ShowToast("Push patch timeout.");
                            Reset();
                        }
                    }
                }
            };
        }

        public static void Reset()
        {
            try
            {
                if (Socket != null)
                {
                    Socket.Shutdown(SocketShutdown.Both);
                    Socket.Close();
                }

                if (MainThread != null)
                {
                    MainThread.Abort();
                }
            }
            catch
            {
            }
            finally
            {
                Socket = null;
                MainThread = null;
                CurrentTime = 0;
                LastTime = 0;
            }
        }

        public static void Push(string host, int port)
        {
            if (MainThread == null)
            {
                string patchDir = Constants.BUILD_PATCH_PATH;
                string patchFile = patchDir.Substring(0, patchDir.LastIndexOf("/")) + ".zip";
                string patchUrl = Helper.StringFormat("{0}/{1}/{2}", Constants.PROJ_NAME, Preferences.Instance.Developer, Constants.PLATFORM_NAME);
                MainThread = new Thread(() =>
                {
                    LastTime = CurrentTime;
                    try
                    {
                        IPAddress[] addrs = Dns.GetHostAddresses(host);
                        if (addrs.Length == 0) throw new Exception("none addr for " + host);
                        IPAddress addr = addrs[0];
                        IPEndPoint rep = new IPEndPoint(addr, port);
                        Socket = new Socket(addr.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                        Socket.Connect(rep);
                        Helper.DeleteFile(patchFile);
                        Helper.Zip(patchDir, patchFile, null, Constants.ZIP_CMD);
                        Folder folder = new Folder();
                        folder.url = patchUrl;
                        folder.data = File.ReadAllBytes(patchFile);
                        byte[] bytes = EditorNet.EncodeMsg(1003, folder);
                        Socket.Send(bytes);
                        Helper.DeleteFile(patchFile);
                        Socket.Receive(HEADER);
                        int id = BitConverter.ToInt32(HEADER, 4);
                        while (!(id == 1004 || id == 1005))
                        {
                            Socket.Receive(HEADER);
                            id = BitConverter.ToInt32(HEADER, 4);
                        }

                        Loom.QueueInMainThread(() =>
                        {
                            if (id == 1004)
                            {
                                Helper.Log("Push {0} to {1} success.", patchUrl, host);
                                Helper.ShowToast(Helper.StringFormat("Push {0} success.", patchUrl));
                            }
                            else
                            {
                                Helper.LogError("Push {0} to {1} failed caused by server error.", patchUrl, host);
                                Helper.ShowToast(Helper.StringFormat("Push {0} failed caused by server error.", patchUrl));
                            }
                        });
                        Socket.Shutdown(SocketShutdown.Both);
                        Socket.Close();
                        LastTime = 0;
                        Socket = null;
                        MainThread = null;
                    }
                    catch (Exception e)
                    {
                        Loom.QueueInMainThread(() =>
                        {
                            Helper.LogError("Push {0} to {1} failed caused by internal error.", patchUrl, host);
                            Helper.ShowToast(Helper.StringFormat("Push {0} failed caused by internal error.", patchUrl));
                        });
                        MainThread = null;
                        throw e;
                    }
                });
                MainThread.Start();
            }
            else
            {
                Helper.ShowToast("Processing patch.");
            }
        }
    }
}