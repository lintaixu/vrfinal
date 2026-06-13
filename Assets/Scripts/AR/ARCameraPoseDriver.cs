using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;

namespace RubiksCube.AR
{
    /// <summary>
    /// Ensures the AR camera has a Tracked Pose Driver (Input System) so its
    /// transform follows the phone's real-world motion. Without this the camera
    /// never moves, so AR-anchored objects look static and can only be spun by
    /// hand — defeating the "walk around it 360°" experience.
    ///
    /// The scene was built manually (no prefab), which is why the driver was
    /// missing; we add it in code so no Inspector wiring is required.
    /// </summary>
    public static class ARCameraPoseDriver
    {
        public static void Ensure(Camera cam)
        {
            if (cam == null) return;
            if (cam.GetComponent<TrackedPoseDriver>() != null) return;

            var tpd = cam.gameObject.AddComponent<TrackedPoseDriver>();
            tpd.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
            tpd.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;

            var posAction = new InputAction(
                name: "CenterEyePosition",
                type: InputActionType.Value,
                binding: "<XRHMD>/centerEyePosition",
                expectedControlType: "Vector3");

            var rotAction = new InputAction(
                name: "CenterEyeRotation",
                type: InputActionType.Value,
                binding: "<XRHMD>/centerEyeRotation",
                expectedControlType: "Quaternion");

            tpd.positionInput = new InputActionProperty(posAction);
            tpd.rotationInput = new InputActionProperty(rotAction);

            posAction.Enable();
            rotAction.Enable();

            Debug.Log("[AR] Tracked Pose Driver added to AR camera — device tracking enabled");
        }
    }
}
