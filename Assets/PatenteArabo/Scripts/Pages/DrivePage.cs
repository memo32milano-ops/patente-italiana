using UnityEngine;
using UnityEngine.UIElements;

namespace PatenteArabo
{
    /// <summary>Hosts the 3D driving mode with its UI Toolkit HUD.</summary>
    public class DrivePage : Page
    {
        DriveMode _drive;
        public override bool Transparent => true;
        public override bool Busy => true;   // a language/translation event must not restart the drive

        public override void Build(VisualElement root)
        {
            root.style.flexGrow = 1;
            var go = new GameObject("DriveMode");
            _drive = go.AddComponent<DriveMode>();
            _drive.Begin(root);
        }

        public override void Tick(float dt) { Gamify.StudyTick(dt); }

        public override void OnHide()
        {
            Challenge.Submit();
            Time.timeScale = 1f;
            if (_drive != null) _drive.Shutdown();
            _drive = null;
            var cam = Camera.main;
            if (cam != null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = U.Hex("090E1C"); }
        }
    }
}
