using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System;
using System.Collections.Generic;

namespace RubiksCube.AR
{
    public class ARSessionManager : MonoBehaviour
    {
        public static ARSessionManager Instance { get; private set; }

        [SerializeField] private ARSession arSession;
        [SerializeField] private ARPlaneManager planeManager;
        [SerializeField] private ARRaycastManager raycastManager;
        [SerializeField] private ARAnchorManager anchorManager;

        public event Action<ARPlane> OnPlaneDetected;
        public event Action<Vector3, Quaternion> OnAnchorPlaced;

        public ARAnchor CurrentAnchor { get; private set; }
        public bool IsPlaneDetected { get; private set; }
        public bool IsAnchorPlaced => CurrentAnchor != null;

        private static List<ARRaycastHit> raycastHits = new List<ARRaycastHit>();

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(gameObject); return; }
        }

        private void OnEnable()
        {
            if (planeManager != null)
                planeManager.planesChanged += OnPlanesChanged;
        }

        private void OnDisable()
        {
            if (planeManager != null)
                planeManager.planesChanged -= OnPlanesChanged;
        }

        private void OnPlanesChanged(ARPlanesChangedEventArgs args)
        {
            if (!IsPlaneDetected && args.added.Count > 0)
            {
                IsPlaneDetected = true;
                OnPlaneDetected?.Invoke(args.added[0]);
                Debug.Log("[AR] First plane detected.");
            }
        }

        public bool TryPlaceAnchor(Vector2 screenPosition)
        {
            if (raycastManager == null) return false;

            if (raycastManager.Raycast(screenPosition, raycastHits, TrackableType.PlaneWithinPolygon))
            {
                var hit = raycastHits[0];
                var anchorGO = new GameObject("CubeAnchor");
                anchorGO.transform.position = hit.pose.position;
                anchorGO.transform.rotation = hit.pose.rotation;

                CurrentAnchor = anchorGO.AddComponent<ARAnchor>();
                OnAnchorPlaced?.Invoke(hit.pose.position, hit.pose.rotation);

                // Stop detecting planes once anchor is placed
                if (planeManager != null)
                    planeManager.enabled = false;

                Debug.Log($"[AR] Anchor placed at {hit.pose.position}");
                return true;
            }
            return false;
        }

        public void Reset()
        {
            if (CurrentAnchor != null)
            {
                Destroy(CurrentAnchor.gameObject);
                CurrentAnchor = null;
            }
            IsPlaneDetected = false;
            if (planeManager != null)
                planeManager.enabled = true;
        }
    }
}
