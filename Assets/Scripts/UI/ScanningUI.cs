using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.XR.ARFoundation;
using RubiksCube.Data;
using RubiksCube.ColorDetection;

namespace RubiksCube.UI
{
    /// <summary>
    /// Scans the six cube faces by reading back the on-screen pixels of the
    /// live AR feed. ARCameraBackground does the YUV→RGB conversion correctly
    /// on the GPU (what you see is correct), whereas the CPU-image path
    /// (XRCpuImage.Convert) produced a near-grayscale, blue-suppressed image on
    /// this device. AR runs continuously so tracking stays solid for the step
    /// guide; we just grab the framebuffer for one frame with the UI hidden.
    /// </summary>
    public class ScanningUI : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private TextMeshProUGUI progressText;
        [SerializeField] private TextMeshProUGUI hintText;
        [SerializeField] private Button captureButton;
        [SerializeField] private Button retakeButton;
        [SerializeField] private RectTransform gridOverlay;
        [SerializeField] private Image[] colorPreviewCells; // 9 cells for preview

        [Header("Grid Settings")]
        [SerializeField] private float gridScreenRatio = 0.6f;
        [Tooltip("Rotate the sampled 3x3 grid: 0/90/180/270 to match orientation")]
        [SerializeField] private int captureRotation = 0;

        [Header("Component References")]
        [SerializeField] private ColorDetector colorDetector;
        [SerializeField] private RawImage cameraPreview; // unused (AR feed is the preview)

        private ARCameraManager arCameraManager;
        private CanvasGroup panelGroup;

        private CubeState cubeState;
        private int currentFaceIndex = 0;
        private bool isScanning = false;
        private bool cameraReady = false;
        private bool capturing = false;
        private float captureBlockTimer = 0f;

        // Face scan order: U, R, F, D, L, B
        private static readonly string[] FaceNames = {
            "Top (U) - White center", "Right (R) - Red center",
            "Front (F) - Green center", "Bottom (D) - Yellow center",
            "Left (L) - Orange center", "Back (B) - Blue center"
        };

        public CubeState ScannedState => cubeState;
        public bool IsComplete => currentFaceIndex >= 6;

        public System.Action OnScanComplete;

        private void Start()
        {
            cubeState = new CubeState();

            // Make the scanning panel transparent so the live AR feed shows
            var bg = GetComponent<Image>();
            if (bg != null)
            {
                var c = bg.color;
                bg.color = new Color(c.r, c.g, c.b, 0f);
            }

            // CanvasGroup lets us hide all overlay UI for the capture frame
            panelGroup = GetComponent<CanvasGroup>();
            if (panelGroup == null) panelGroup = gameObject.AddComponent<CanvasGroup>();

            if (captureButton != null)
            {
                captureButton.onClick.AddListener(OnCaptureClicked);
                var tmp = captureButton.GetComponentInChildren<TextMeshProUGUI>();
                if (tmp != null) tmp.text = "Capture";
            }
            if (retakeButton != null)
            {
                retakeButton.onClick.AddListener(OnRetakeClicked);
                var tmp = retakeButton.GetComponentInChildren<TextMeshProUGUI>();
                if (tmp != null) tmp.text = "Retake";
            }
        }

        private void Update()
        {
            if (captureBlockTimer > 0f)
                captureBlockTimer -= Time.deltaTime;
        }

        private void OnFrameReceived(ARCameraFrameEventArgs args)
        {
            if (!cameraReady)
            {
                cameraReady = true;
                UpdateUI();
                Debug.Log("[Scan] AR feed live — ready to capture");
            }
        }

        public void StartScanning()
        {
            currentFaceIndex = 0;
            cubeState = new CubeState();
            isScanning = true;
            cameraReady = false;
            captureBlockTimer = 1.0f;

            if (cameraPreview != null) cameraPreview.gameObject.SetActive(false);

            if (arCameraManager == null)
                arCameraManager = FindFirstObjectByType<ARCameraManager>();
            if (arCameraManager != null)
                arCameraManager.frameReceived += OnFrameReceived;

            MakeGridSquare();
            UpdateUI();
        }

        public void StopScanning()
        {
            isScanning = false;
            if (arCameraManager != null)
                arCameraManager.frameReceived -= OnFrameReceived;
        }

        private void OnDisable()
        {
            if (arCameraManager != null)
                arCameraManager.frameReceived -= OnFrameReceived;
        }

        public void ShowMessage(string message)
        {
            if (hintText != null) hintText.text = message;
        }

        /// <summary>Center the on-screen grid overlay as a square.</summary>
        private void MakeGridSquare()
        {
            if (gridOverlay == null) return;
            var parent = gridOverlay.parent as RectTransform;
            if (parent == null) return;

            float side = parent.rect.width * gridScreenRatio;
            gridOverlay.anchorMin = new Vector2(0.5f, 0.5f);
            gridOverlay.anchorMax = new Vector2(0.5f, 0.5f);
            gridOverlay.pivot = new Vector2(0.5f, 0.5f);
            gridOverlay.anchoredPosition = Vector2.zero;
            gridOverlay.sizeDelta = new Vector2(side, side);
        }

        private void OnCaptureClicked()
        {
            if (!isScanning || currentFaceIndex >= 6) return;
            if (!cameraReady)
            {
                Debug.LogWarning("[Scan] Camera not ready yet, ignoring capture");
                return;
            }
            if (captureBlockTimer > 0f || capturing) return;

            captureBlockTimer = 0.8f;
            StartCoroutine(CaptureRoutine());
        }

        private IEnumerator CaptureRoutine()
        {
            capturing = true;

            // Hide overlay UI so the screenshot is just the AR camera feed
            if (panelGroup != null) panelGroup.alpha = 0f;
            yield return new WaitForEndOfFrame();

            Texture2D screen = ScreenCapture.CaptureScreenshotAsTexture();

            if (panelGroup != null) panelGroup.alpha = 1f;

            // Centered square matching the on-screen grid box
            int boxSide = Mathf.Min(
                (int)(screen.width * gridScreenRatio),
                Mathf.Min(screen.width, screen.height));
            int ox = (screen.width - boxSide) / 2;
            int oy = (screen.height - boxSide) / 2;
            var region = new RectInt(ox, oy, boxSide, boxSide);

            char[] raw = colorDetector.AnalyzeFace(screen, region);
            char[] faceColors = RotateFacelets(raw, captureRotation);
            cubeState.faces[currentFaceIndex] = faceColors;

            // Diagnostic: real RGB at each cell center
            var sb = new System.Text.StringBuilder();
            for (int gr = 0; gr < 3; gr++)
                for (int gc = 0; gc < 3; gc++)
                {
                    int px = ox + (int)((gc + 0.5f) / 3f * boxSide);
                    int py = oy + (int)((gr + 0.5f) / 3f * boxSide);
                    Color cc = screen.GetPixel(px, py);
                    sb.Append($"[{(int)(cc.r * 255)},{(int)(cc.g * 255)},{(int)(cc.b * 255)}] ");
                }
            Debug.Log($"[Scan] Face {currentFaceIndex} ({FaceNames[currentFaceIndex]}): {new string(faceColors)} (raw {new string(raw)})\n  screen={screen.width}x{screen.height} box={boxSide} cellRGB: {sb}");

            Destroy(screen);
            UpdateColorPreview(faceColors);

            currentFaceIndex++;
            UpdateUI();

            if (currentFaceIndex >= 6)
            {
                isScanning = false;
                OnScanComplete?.Invoke();
            }

            capturing = false;
        }

        /// <summary>Rotate a 3x3 facelet array by 0/90/180/270 degrees (CW).</summary>
        private static char[] RotateFacelets(char[] f, int degrees)
        {
            int steps = ((degrees / 90) % 4 + 4) % 4;
            char[] cur = (char[])f.Clone();
            for (int s = 0; s < steps; s++)
            {
                char[] outF = new char[9];
                for (int r = 0; r < 3; r++)
                    for (int c = 0; c < 3; c++)
                        outF[c * 3 + (2 - r)] = cur[r * 3 + c]; // 90° CW
                cur = outF;
            }
            return cur;
        }

        private void OnRetakeClicked()
        {
            if (currentFaceIndex > 0)
            {
                currentFaceIndex--;
                captureBlockTimer = 0.5f;
                UpdateUI();
            }
        }

        private void UpdateUI()
        {
            if (progressText != null)
                progressText.text = $"Scanned {currentFaceIndex} / 6 faces";

            if (hintText != null && currentFaceIndex < 6)
                hintText.text = cameraReady
                    ? $"Aim at: {FaceNames[currentFaceIndex]}"
                    : "Starting camera...";
            else if (hintText != null)
                hintText.text = "Scan complete!";

            if (retakeButton != null)
                retakeButton.interactable = currentFaceIndex > 0;

            if (captureButton != null)
                captureButton.interactable = currentFaceIndex < 6 && cameraReady;
        }

        private void UpdateColorPreview(char[] faceColors)
        {
            if (colorPreviewCells == null || colorPreviewCells.Length < 9) return;

            for (int i = 0; i < 9; i++)
            {
                if (colorPreviewCells[i] != null && colorDetector != null)
                    colorPreviewCells[i].color = colorDetector.GetPreviewColor(faceColors[i]);
            }
        }

        public Rect GetGridNormalizedRect()
        {
            float offset = (1f - gridScreenRatio) / 2f;
            return new Rect(offset, offset, gridScreenRatio, gridScreenRatio);
        }
    }
}
