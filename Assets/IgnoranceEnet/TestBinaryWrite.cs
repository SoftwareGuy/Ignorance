using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using UnityEngine;

class TestBinaryWrite : MonoBehaviour
{
    void Start()
    {
        const int LOOP = 10000000, INDEX = 100, VALUE = 512;
        byte[] buffer = new byte[1024];
        Stopwatch watch;

        watch = Stopwatch.StartNew();
        for (int i = 0; i < LOOP; i++)
        {
            Set1(buffer, INDEX, VALUE);
        }
        watch.Stop();
        UnityEngine.Debug.Log("Set1: " + watch.ElapsedMilliseconds + "ms");

        watch = Stopwatch.StartNew();
        for (int i = 0; i < LOOP; i++)
        {
            Set2(buffer, INDEX, VALUE);
        }
        watch.Stop();
        UnityEngine.Debug.Log("Set2: " + watch.ElapsedMilliseconds + "ms");

        watch = Stopwatch.StartNew();
        for (int i = 0; i < LOOP; i++)
        {
            Set3(buffer, INDEX, VALUE);
        }
        watch.Stop();
        UnityEngine.Debug.Log("Set3: " + watch.ElapsedMilliseconds + "ms");

        watch = Stopwatch.StartNew();
        for (int i = 0; i < LOOP; i++)
        {
            Set4(buffer, INDEX, VALUE);
        }
        watch.Stop();
        UnityEngine.Debug.Log("Set4: " + watch.ElapsedMilliseconds + "ms");

        UnityEngine.Debug.Log("done");
    }
    unsafe static void Set1(byte[] target, int index, int value)
    {
        fixed (byte* p = &target[0])
        {
            Marshal.WriteInt32(new IntPtr(p), index, value);
        }
    }

    unsafe static void Set2(byte[] target, int index, int value)
    {
        int* p = &value;
        for (int i = 0; i < 4; i++)
        {
            target[index + i] = *((byte*)p + i);
        }
    }

    static void Set3(byte[] target, int index, int value)
    {
        byte[] data = BitConverter.GetBytes(value);
        Buffer.BlockCopy(data, 0, target, index, data.Length);
    }
    static void Set4(byte[] target, int index, int value)
    {
        target[index++] = (byte)value;
        target[index++] = (byte)(value >> 8);
        target[index++] = (byte)(value >> 16);
        target[index] = (byte)(value >> 24);
    }
}