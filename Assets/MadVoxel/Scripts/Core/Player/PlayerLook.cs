using UnityEngine;

namespace MadVoxel.Core.Player
{
    /// <summary>First-person mouse look. Yaw on the body, pitch on the camera.</summary>
    public class PlayerLook : MonoBehaviour
    {
        GameConfig _config;
        Transform _cameraPivot;
        float _pitch;

        public float Yaw { get { return transform.eulerAngles.y; } }
        public float Pitch { get { return _pitch; } }

        public void Init(GameConfig config, Transform cameraPivot)
        {
            _config = config;
            _cameraPivot = cameraPivot;
        }

        public void SetRotation(float yaw, float pitch)
        {
            _pitch = Mathf.Clamp(pitch, -89f, 89f);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (_cameraPivot != null) _cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        void Update()
        {
            if (_config == null || !InputBridge.Enabled) return;

            Vector2 look = InputBridge.Look * _config.mouseSensitivity;
            if (look.sqrMagnitude <= 0f) return;

            transform.Rotate(0f, look.x, 0f, Space.Self);
            _pitch = Mathf.Clamp(_pitch - look.y, -89f, 89f);
            if (_cameraPivot != null) _cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }
    }
}
