using System;
#if UNITY
using UnityEngine;
#endif

namespace PFound.NetworkLayer.Telepathy
{
    public static class Log
    {
#if !UNITY
        public static Action<string> Verbose = (verbose) =>
        {
            Console.WriteLine($"[VERBOSE] {verbose}");
        };
        public static Action<string> Info = (info) =>
        {
            Console.WriteLine(info);
        };
        public static Action<string> Warning = (log =>
        {
            Console.WriteLine($"[WARNING] {log}");
        });
        public static Action<string> Error = (log =>
        {
            Console.Error.WriteLine($"[ERROR] {log}");
        });
#else
	    public static Action<string> Info = Debug.Log;
	    public static Action<string> Warning = Debug.LogWarning;
	    public static Action<string> Error = Debug.LogError;
#endif
    }

}
