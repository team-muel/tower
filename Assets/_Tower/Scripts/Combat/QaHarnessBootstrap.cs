using System;
using Tower.Core;
using UnityEngine;

namespace Tower.Combat
{
    // Dev-only entry points. Both features are fully inert unless the matching
    // command line argument is present (-qaPort <n> / -devcam).
    public static class QaHarnessBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            var args = Environment.GetCommandLineArgs();

            if (QaCommandLine.TryGetQaPort(args, out var port))
            {
                QaRuntime.Enable(new QaRegistry());
                var host = new GameObject("QA Harness");
                UnityEngine.Object.DontDestroyOnLoad(host);
                host.AddComponent<QaTcpServer>().Configure(port);
            }

            if (QaCommandLine.HasDevCameraFlag(args))
            {
                var host = new GameObject("Dev Camera Tuning");
                UnityEngine.Object.DontDestroyOnLoad(host);
                host.AddComponent<CameraTuningModeController>();
            }
        }
    }
}
