using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Text;
using System.IO;

public class AssetBuildInfoLogger
{
    private static Dictionary<string, StringBuilder> loggers = new Dictionary<string, StringBuilder>();

    public static void LogAssetBuild(string type, string bundleName, string filePath)
    {
        if(loggers == null)
        {
            loggers = new Dictionary<string, StringBuilder>();
        }
        if(!loggers.ContainsKey(type))
        {
            loggers.Add(type, new StringBuilder());
        }
        StringBuilder logger = loggers[type];
        AppendAssetBuildInfoLog(logger, type, bundleName, filePath);
    }

    public static void SaveLog()
    {
        string path = BuildLogPath;
        if (File.Exists(path))
        {
            File.Delete(path);
        }
        foreach(var pair in loggers)
        {
            pair.Value.Append("\n\n");
            File.AppendAllText(path, pair.Value.ToString());
        }
        Clear();
    }

    private static void Clear()
    {
        foreach(var pair in loggers)
        {
            pair.Value.Clear();
        }
        loggers = null;
    }

    private static void AppendAssetBuildInfoLog(StringBuilder logger, string type, string bundleName, string filePath)
    {
        logger.AppendFormat("[{0}] ¡¾{1}¡¿ ¡¾{2}¡¿ {3}\n", DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss"), type, bundleName, filePath);
    }

    private static string BuildLogPath
    {
        get
        {
            string path = string.Format("{0}/AssetLog/assets_{1}-{2}.txt", AssetBuildInfoUtils.BuildInfoPath, DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss"), BuildSetting.Instance.Version);
            string directoryName = Path.GetDirectoryName(path);
            if(!Directory.Exists(directoryName))
            {
                Directory.CreateDirectory(directoryName);
            }
            return path;
        }
    }

}
