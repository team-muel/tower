using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Tower.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Linq;

namespace Tower.Combat
{
    // Line-oriented TCP QA endpoint. Localhost only; commands are parsed by
    // Tower.Core.QaCommandParser and executed on the main thread so that
    // uGUI/scene access stays on the Unity thread.
    public sealed class QaTcpServer : MonoBehaviour
    {
        private sealed class PendingRequest
        {
            public string Line;
            public StreamWriter Writer;
        }

        private readonly ConcurrentQueue<PendingRequest> _pending = new ConcurrentQueue<PendingRequest>();
        private TcpListener _listener;
        private Thread _acceptThread;
        private volatile bool _running;
        private bool _quitRequested;

        public void Configure(int port)
        {
            if (_listener != null)
            {
                return;
            }

            _listener = new TcpListener(IPAddress.Loopback, port);
            _listener.Start();
            _running = true;
            _acceptThread = new Thread(AcceptLoop) { IsBackground = true, Name = "QA Harness Accept" };
            _acceptThread.Start();
            Debug.Log($"[QaHarness] Listening on 127.0.0.1:{port}.");
        }

        private void Update()
        {
            while (_pending.TryDequeue(out var request))
            {
                string response;
                try
                {
                    response = Execute(request.Line);
                }
                catch (Exception exception)
                {
                    response = QaProtocol.Error(exception.Message);
                }

                try
                {
                    request.Writer.WriteLine(response);
                }
                catch
                {
                    // Client disconnected before the response was written.
                }

                if (_quitRequested)
                {
                    Shutdown();
                    Application.Quit();
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
#endif
                    return;
                }
            }
        }

        private void OnDestroy()
        {
            Shutdown();
            QaRuntime.Disable();
        }

        private string Execute(string line)
        {
            var parsed = QaCommandParser.Parse(line);
            if (parsed.IsFailure)
            {
                return QaProtocol.Error(parsed.Error);
            }

            switch (parsed.Value.Kind)
            {
                case QaCommandKind.Press:
                    return ExecutePress(parsed.Value.Argument);
                case QaCommandKind.State:
                    return ExecuteState();
                case QaCommandKind.Scene:
                    return ExecuteScene(parsed.Value.Argument);
                case QaCommandKind.Dump:
                    return ExecuteDump();
                case QaCommandKind.Quit:
                    _quitRequested = true;
                    return QaProtocol.Ok;
                default:
                    return QaProtocol.Error("Unhandled command kind.");
            }
        }

        private static string ExecuteDump()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("OK ");
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                sb.Append('[').Append(scene.name).Append(']');
                foreach (var root in scene.GetRootGameObjects())
                {
                    sb.Append(' ').Append(root.name).Append(root.activeSelf ? "" : "(off)");
                }
                sb.Append(" | ");
            }
            var probe = new UnityEngine.GameObject("__ddol_probe");
            UnityEngine.Object.DontDestroyOnLoad(probe);
            var ddol = probe.scene;
            sb.Append('[').Append(ddol.name).Append(']');
            foreach (var root in ddol.GetRootGameObjects())
            {
                if (root.name == "__ddol_probe") { continue; }
                sb.Append(' ').Append(root.name);
            }
            UnityEngine.Object.Destroy(probe);
            return sb.ToString();
        }
        private static string ExecutePress(string buttonName)
        {
            var registry = QaRuntime.Registry;
            if (registry == null)
            {
                return QaProtocol.Error("QA registry is not enabled.");
            }

            var pressed = registry.Press(buttonName);
            return pressed.IsSuccess ? QaProtocol.Ok : QaProtocol.Error(pressed.Error);
        }

        private static string ExecuteState()
        {
            var sceneName = SceneManager.GetActiveScene().name;
            var registry = QaRuntime.Registry;
            var snapshot = registry != null
                ? registry.BuildState(sceneName)
                : new QaStateSnapshot { sceneName = sceneName };
            return QaStateSerializer.ToJson(snapshot);
        }

        private static string ExecuteScene(string sceneName)
        {
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                return QaProtocol.Error($"Scene '{sceneName}' is not in the build.");
            }

            LoadSceneViaSequence(sceneName);
            return QaProtocol.Ok;
        }

        private static void LoadSceneViaSequence(string sceneName)
        {
            var uiAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Tower.UI");
            
            if (uiAssembly == null)
            {
                SceneManager.LoadScene(sceneName);
                return;
            }

            var sequenceManagerType = uiAssembly.GetType("Tower.UI.SceneSequenceManager");
            if (sequenceManagerType == null)
            {
                SceneManager.LoadScene(sceneName);
                return;
            }

            var instanceProp = sequenceManagerType.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            var instance = instanceProp?.GetValue(null);
            var loadMethod = sequenceManagerType.GetMethod("LoadSceneWithSequence", new Type[] { typeof(string) });
            
            if (instance != null && loadMethod != null)
            {
                loadMethod.Invoke(instance, new object[] { sceneName });
            }
            else
            {
                SceneManager.LoadScene(sceneName);
            }
        }

        private void AcceptLoop()
        {
            while (_running)
            {
                TcpClient client;
                try
                {
                    client = _listener.AcceptTcpClient();
                }
                catch
                {
                    break;
                }

                var clientThread = new Thread(() => ClientLoop(client)) { IsBackground = true, Name = "QA Harness Client" };
                clientThread.Start();
            }
        }

        private void ClientLoop(TcpClient client)
        {
            using (client)
            {
                try
                {
                    var stream = client.GetStream();
                    var reader = new StreamReader(stream, new UTF8Encoding(false));
                    var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" };
                    string line;
                    while (_running && (line = reader.ReadLine()) != null)
                    {
                        _pending.Enqueue(new PendingRequest { Line = line, Writer = writer });
                    }
                }
                catch
                {
                    // Client disconnected; dev harness tolerates dropped sessions.
                }
            }
        }

        private void Shutdown()
        {
            _running = false;
            try
            {
                _listener?.Stop();
            }
            catch
            {
                // Ignore shutdown races.
            }

            _listener = null;
        }
    }
}
