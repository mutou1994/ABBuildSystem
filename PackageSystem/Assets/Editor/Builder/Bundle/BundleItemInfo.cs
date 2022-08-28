using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class BundleItemInfo
{
    public string bundleName;

    /// <summary>
    /// �����ҵ�AB
    /// </summary>
    private HashSet<BundleItemInfo> RefParentSet = new HashSet<BundleItemInfo>();

    /// <summary>
    /// ��������AB
    /// </summary>
    private HashSet<BundleItemInfo> RefChildSet = new HashSet<BundleItemInfo>();

    public BundleItemInfo(string bundleName)
    {
        this.bundleName = bundleName;
    }

    public int RefCount
    {
        get
        {
            return RefChildSet.Count;
        }
    }

    public BundleItemInfo[] GetRefChilds()
    {
        return RefChildSet.ToArray();
    }

    /// <summary>
    /// ��������ҵ���
    /// </summary>
    /// <param name="item"></param>
    public void AddRefParentAsset(BundleItemInfo item)
    {
        if (!RefParentSet.Contains(item))
        {
            RefParentSet.Add(item);
        }
    }

    /// <summary>
    /// �������������
    /// </summary>
    /// <param name="item"></param>
    public void AddRefChildAsset(BundleItemInfo item)
    {
        //���Ѿ����ҵ�ֱ�������������������������
        if (!AlreadyRefed(item))
        {
            //��������������ô���������������ҾͲ���Ҫֱ��������
            ClearChildRefRecursive(item);

            RefChildSet.Add(item);
            item.AddRefParentAsset(this);

            //��������������ô�����ҵ���Ͳ���Ҫֱ�������������
            ClearParentRefRecursive(item);
        }
    }

    /// <summary>
    /// ��������������ô���������������ҾͲ���Ҫֱ��������
    /// </summary>
    /// <param name="item"></param>
    public void ClearChildRefRecursive(BundleItemInfo item)
    {
        List<BundleItemInfo> removeList = new List<BundleItemInfo>();
        foreach(var child in RefChildSet)
        {
            if(item.AlreadyRefed(child))
            {
                removeList.Add(child);
            }
        }
        if(removeList.Count > 0)
        {
            foreach(var child in removeList)
            {
                RemoveRefChild(child);
            }
        }
    }

    /// <summary>
    /// �Ƿ��Ѿ�ֱ�ӻ�������
    /// </summary>
    /// <param name="item"></param>
    /// <returns></returns>
    public bool AlreadyRefed(BundleItemInfo item)
    {
        //ֱ������
        if (RefChildSet.Contains(item)) return true;

        //�������
        foreach(var child in RefChildSet)
        {
            if (child.AlreadyRefed(item))
                return true;
        }
        return false;
    }

    /// <summary>
    /// ��������������ôֱ�ӻ��������ҵ���Ͷ�����Ҫֱ�������������
    /// </summary>
    /// <param name="item"></param>
    public void ClearParentRefRecursive(BundleItemInfo item)
    {
        foreach(var parent in RefParentSet)
        {
            if(parent.RefChildSet.Contains(item))
            {
                parent.RemoveRefChild(item);
            }
            parent.ClearChildRefRecursive(item);
        }
    }

    /// <summary>
    /// �Ƴ���ֱ����������
    /// </summary>
    /// <param name="item"></param>
    public void RemoveRefChild(BundleItemInfo item)
    {
        if(this.RefChildSet.Contains(item))
        {
            this.RefChildSet.Remove(item);
        }

        //ͬʱ�Ƴ�Ŀ���еı���������
        if(item.RefParentSet.Contains(this))
        {
            item.RefParentSet.Remove(this);
        }
    }
}
