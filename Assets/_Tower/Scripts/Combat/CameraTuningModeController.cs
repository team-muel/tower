using System.Globalization;
using System.IO;
using Tower.Core;
using UnityEngine;

namespace Tower.Combat
{
    // -devcam camera tuning mode: I/K pitch, +/- distance, [/] FOV. Current
    // values are drawn top-left; P dumps them to %TEMP%\tower-cam.json so a
    // tuned setup can be promoted into Tower.Core.CameraTuning defaults.
    // T19: drives whichever rig the scene runs - the combat OrbitCameraRig
    // takes precedence, otherwise the iso follow rig.
    public sealed class CameraTuningModeController : MonoBehaviour
    {
        private const string DumpFileName = "tower-cam.json";
        private const float PitchDegreesPerSecond = 20f;
        private const float DistanceMetersPerSecond = 5f;
        private const float FovDegreesPerSecond = 15f;

        private IsoCameraRig _isoRig;
        private OrbitCameraRig _orbitRig;
        private string _lastDumpMessage = string.Empty;

        private void Update()
        {
            if (!TryResolveRig())
            {
                return;
            }

            var tuning = CurrentTuning();
            float pitch = tuning.Pitch;
            float distance = tuning.Distance;
            float fov = tuning.Fov;
            float delta = Time.unscaledDeltaTime;
            bool changed = false;

            if (Input.GetKey(KeyCode.I))
            {
                pitch += PitchDegreesPerSecond * delta;
                changed = true;
            }

            if (Input.GetKey(KeyCode.K))
            {
                pitch -= PitchDegreesPerSecond * delta;
                changed = true;
            }

            if (Input.GetKey(KeyCode.Equals) || Input.GetKey(KeyCode.Plus) || Input.GetKey(KeyCode.KeypadPlus))
            {
                distance += DistanceMetersPerSecond * delta;
                changed = true;
            }

            if (Input.GetKey(KeyCode.Minus) || Input.GetKey(KeyCode.KeypadMinus))
            {
                distance -= DistanceMetersPerSecond * delta;
                changed = true;
            }

            if (Input.GetKey(KeyCode.LeftBracket))
            {
                fov -= FovDegreesPerSecond * delta;
                changed = true;
            }

            if (Input.GetKey(KeyCode.RightBracket))
            {
                fov += FovDegreesPerSecond * delta;
                changed = true;
            }

            if (changed)
            {
                ApplyTuning(new CameraTuningState(pitch, distance, fov, tuning.FollowDamping));
            }

            if (Input.GetKeyDown(KeyCode.P))
            {
                DumpToTemp();
            }
        }

        private void OnGUI()
        {
            if (_orbitRig == null && _isoRig == null)
            {
                return;
            }

            var tuning = CurrentTuning();
            var rigName = _orbitRig != null ? "orbit" : "iso";
            var text = string.Format(
                CultureInfo.InvariantCulture,
                "DevCam ({4})  pitch {0:0.#}  distance {1:0.#}  fov {2:0.#}  damping {3:0.##}\nI/K pitch  +/- distance  [/] fov  P dump\n{5}",
                tuning.Pitch,
                tuning.Distance,
                tuning.Fov,
                tuning.FollowDamping,
                rigName,
                _lastDumpMessage);
            GUI.Label(new Rect(10f, 10f, 720f, 72f), text);
        }

        // Rigs are recreated per encounter; re-resolve whenever both refs die.
        private bool TryResolveRig()
        {
            if (_orbitRig == null && _isoRig == null)
            {
                _orbitRig = FindFirstObjectByType<OrbitCameraRig>();
                if (_orbitRig == null)
                {
                    _isoRig = FindFirstObjectByType<IsoCameraRig>();
                }
            }

            return _orbitRig != null || _isoRig != null;
        }

        private CameraTuningState CurrentTuning()
        {
            return _orbitRig != null ? _orbitRig.Tuning : _isoRig.Tuning;
        }

        private void ApplyTuning(CameraTuningState state)
        {
            if (_orbitRig != null)
            {
                _orbitRig.ApplyTuning(state);
            }
            else if (_isoRig != null)
            {
                _isoRig.ApplyTuning(state);
            }
        }

        private void DumpToTemp()
        {
            var path = Path.Combine(Path.GetTempPath(), DumpFileName);
            File.WriteAllText(path, CurrentTuning().ToJson());
            _lastDumpMessage = "Dumped to " + path;
            Debug.Log("[DevCam] " + _lastDumpMessage);
        }
    }
}
