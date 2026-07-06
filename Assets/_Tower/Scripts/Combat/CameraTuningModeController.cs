using System.Globalization;
using System.IO;
using Tower.Core;
using UnityEngine;

namespace Tower.Combat
{
    // -devcam camera tuning mode: I/K pitch, +/- distance, [/] FOV. Current
    // values are drawn top-left; P dumps them to %TEMP%\tower-cam.json so a
    // tuned setup can be promoted into Tower.Core.CameraTuning defaults.
    public sealed class CameraTuningModeController : MonoBehaviour
    {
        private const string DumpFileName = "tower-cam.json";
        private const float PitchDegreesPerSecond = 20f;
        private const float DistanceMetersPerSecond = 5f;
        private const float FovDegreesPerSecond = 15f;

        private IsoCameraRig _rig;
        private string _lastDumpMessage = string.Empty;

        private void Update()
        {
            if (_rig == null)
            {
                _rig = FindFirstObjectByType<IsoCameraRig>();
                if (_rig == null)
                {
                    return;
                }
            }

            var tuning = _rig.Tuning;
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
                _rig.ApplyTuning(new CameraTuningState(pitch, distance, fov, tuning.FollowDamping));
            }

            if (Input.GetKeyDown(KeyCode.P))
            {
                DumpToTemp();
            }
        }

        private void OnGUI()
        {
            if (_rig == null)
            {
                return;
            }

            var tuning = _rig.Tuning;
            var text = string.Format(
                CultureInfo.InvariantCulture,
                "DevCam  pitch {0:0.#}  distance {1:0.#}  fov {2:0.#}  damping {3:0.##}\nI/K pitch  +/- distance  [/] fov  P dump\n{4}",
                tuning.Pitch,
                tuning.Distance,
                tuning.Fov,
                tuning.FollowDamping,
                _lastDumpMessage);
            GUI.Label(new Rect(10f, 10f, 720f, 72f), text);
        }

        private void DumpToTemp()
        {
            var path = Path.Combine(Path.GetTempPath(), DumpFileName);
            File.WriteAllText(path, _rig.Tuning.ToJson());
            _lastDumpMessage = "Dumped to " + path;
            Debug.Log("[DevCam] " + _lastDumpMessage);
        }
    }
}
