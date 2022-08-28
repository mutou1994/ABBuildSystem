using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;

public class PatchBuildUtils
{
    public static readonly int MaxFile = 400;
    public static readonly long MaxSize = 0x100000;

    public static uint fileSize;
    public static List<PatchItem> fileList = new List<PatchItem>();
    public static PatchInfos patchInfos = new PatchInfos();

    public static string SavePath
    {
        get
        {
            return string.Format("{0}/Patchs/{1}", AssetBuildInfoUtils.BuildInfoPath, BuildSetting.Instance.Version);
        }
    }

    public static void GeneratePatchs()
    {
        BuildLogger.LogInfo("Begain GeneratePatchs...");
        EditorUtility.DisplayProgressBar("GeneratePatchs...", "", 0);
        Reset();
        patchInfos.Clear();
        var ChangedBundles = AssetBundleBuildUtils.ChangedBundles;
        if(ChangedBundles.Count == 0)
        {
            BuildLogger.LogError("No Resource Changed !!! PatchCount is 0");
            BuildLogger.LogError("GeneratePatchs Finished...");
            EditorUtility.DisplayProgressBar("GeneratePatchs", "Finish...", 1);
            EditorUtility.ClearProgressBar();
            return;
        }

        if(Directory.Exists(SavePath))
        {
            Directory.Delete(SavePath, true);
        }
        if(!Directory.Exists(SavePath))
        {
            Directory.CreateDirectory(SavePath);
        }
        int index = 0;
        int count = ChangedBundles.Count;
        foreach(string abName in ChangedBundles)
        {
            EditorUtility.DisplayProgressBar("GeneratePatchs", abName, (float)(index) / count);
            string abPath = string.Format("{0}/ABResources/{1}", Application.streamingAssetsPath, abName);
            if(!File.Exists(abPath))
            {
                Debug.LogError(string.Format("GeneratePatchs Error!!! file:{0} Not Exists", abPath));
                BuildLogger.LogError(string.Format("GeneratePatchs Error!!! file:{0} Not Exists", abPath));
                continue;
            }
            uint length = (uint)File.ReadAllBytes(abPath).Length;
            fileList.Add(new PatchItem(abName, fileSize, length));
            fileSize += length;
            if(fileSize >= MaxSize || fileList.Count >= MaxFile)
            {
                Write(patchInfos.Count);
                Reset();
            }
        }
        if(patchInfos.Count > 0 || fileList.Count > 0)
        {
            if(fileList.Count > 0)
            {
                Write(patchInfos.Count);
                Reset();
            }
            patchInfos.allPublishVersion = BuildSetting.Instance.Version;
            string json = JsonUtility.ToJson(patchInfos);
            string path = string.Format("{0}/PatchList.txt", SavePath);
            File.WriteAllText(path, json);
            patchInfos.Clear();
        }
        BuildLogger.LogInfo("GeneratePatchs Finish...");
        EditorUtility.DisplayProgressBar("GeneratePatchs", "", 1);
        EditorUtility.ClearProgressBar();
    }

    public static void Write(int index)
    {
        string pkgName = string.Format("patch-{0}.package{1}", BuildSetting.Instance.Version, index+1);
        string relaPath = string.Format("{0}/{1}/{2}", BuildSetting.Instance.Version, AssetBuildInfoUtils.GetPlatformName(), pkgName);
        string pkgPath = string.Format("{0}/{1}", SavePath, pkgName);
        if (fileList.Count > 0)
        {
            int size = 0;
            BuildLogger.LogInfo("PatchFlush: {0}", pkgPath);
            using (FileStream stream = File.OpenWrite(pkgPath))
            {
                for (int i = 0; i < fileList.Count; i++)
                {
                    string abPath = string.Format("{0}/ABResources/{1}", Application.streamingAssetsPath, fileList[i].p);
                    byte[] buffer = File.ReadAllBytes(abPath);
                    stream.Write(buffer, 0, buffer.Length);
                    size += buffer.Length;
                }
                stream.Flush();
                stream.Close();
            }
        }
        patchInfos.AddPackageList(fileList, fileSize, relaPath);
    }

    public static void Reset()
    {
        fileList.Clear();
    }

    public static void Clear()
    {
        Reset();
        patchInfos.Clear();
    }
}
