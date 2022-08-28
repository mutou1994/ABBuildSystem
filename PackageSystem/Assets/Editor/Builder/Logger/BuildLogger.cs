using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Text;
using System.IO;

public class BuildLogger
{
    private static StringBuilder _logger;
    private static StringBuilder m_Logger
    {
        get
        {
            if(_logger == null)
            {
                _logger = new StringBuilder();
            }
            return _logger;
        }
    }

    private static string BuildLogPath
    {
        get
        {
            string path = string.Format("{0}/BuildLog/buildLog_{1}-{2}.txt", AssetBuildInfoUtils.BuildInfoPath, DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss"), BuildSetting.Instance.Version);
            string directoryName = Path.GetDirectoryName(path);
            if (!Directory.Exists(directoryName))
            {
                Directory.CreateDirectory(directoryName);
            }
            return path;
        }
    }

    public static void LogError(string format, params object[] args)
    {
        LogInternal("ERROR", format, args);
    }

    public static void LogInfo(string format, params object[] args)
    {
        LogInternal("INFO", format, args);
    }

    private static void LogInternal(string type, string format, object[] args)
    {
        m_Logger.AppendFormat("[{0}]  ¡¾{1}¡¿  ", DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss"), type);
        m_Logger.AppendFormat(format, args);
        m_Logger.AppendLine();
    }

    public static void LogWarning(string format, params object[] args)
    {
        LogInternal("WARNING", format, args);
    }

    public static void Clear()
    {
        _logger.Clear();
        _logger = null;
    }

    public static void SaveLog()
    {
        LogInfo("SaveLog ......");
        string path = BuildLogPath;
        if(File.Exists(path))
        {
            File.Delete(path);
        }
        File.WriteAllText(BuildLogPath, m_Logger.ToString());
        Clear();
    }
}
