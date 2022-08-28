using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;

public class Md5Tool
{
    public static MD5CryptoServiceProvider _md5Provider;
    public static MD5CryptoServiceProvider md5Provider
    {
        get
        {
            if (_md5Provider == null)
            {
                _md5Provider = new MD5CryptoServiceProvider();
            }
            return _md5Provider;
        }
    }

    public static void Clear()
    {
        if (_md5Provider != null)
        {
            _md5Provider.Dispose();
            _md5Provider = null;
        }
    }

    public static string fileMd5(string file)
    {
        if (!File.Exists(file))
        {
            return string.Empty;
        }
        else
        {
            try
            {
                FileStream stream = new FileStream(file.Replace("\\", "/"), (FileMode)FileMode.Open);
                byte[] buffer = md5Provider.ComputeHash((Stream)stream);
                stream.Close();
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < buffer.Length; i++)
                {
                    builder.Append(buffer[i].ToString("x2"));
                }
                return builder.ToString();
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning("md5file() fail, error:" + exception.Message);
                return string.Empty;
            }
        }
    }
}
