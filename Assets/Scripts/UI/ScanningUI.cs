using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Collections;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using RubiksCube.Data;
using RubiksCube.ColorDetection;

namespace RubiksCube.UI
{
    /// <summary>
    /// Scans the six cube faces using the ARCore camera image (not WebCamTexture).
    /// Keeping AR running the whole time means ARCore tracking is already solid
    /// by the time the step guide needs to anchor the cube in space — and the
    /// live AR feed (ARCameraBackground) is the scanning preview, so no second
    /// camera owner ever fights ARCore for the device camera.
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
        [Tooltip("Rotate the sampled 3x3 grid: 0/90/180/270 to match screen orientation")]
        [SerializeField] private int captureRotation = 90;
        [Tooltip("ARCore CPU image often comes back as BGRA — swap R/B to fix colors")]
        [SerializeField] private bool swapRedBlue = false;

        [Header("Component References")]
        [SerializeField] private ColorDetector colorDetector;
        [SerializeField] private RawImage cameraPreview; // unused now (AR feed is the preview)

        private ARCameraManager arCameraManager;
        private Texture2D captureTex;

        private CubeState cubeState;
        private int currentFaceIndex = 0;
        private bool isScanning = false;
        private bool cameraReady = false;
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

            // Camera is "ready" once ARCore delivers a CPU image
            if (isScanning && !cameraReady && arCameraManager != null)
            {
                if (arCameraManager.TryAcquireLatestCpuImage(out XRCpuImage probe))
                {
                    probe.Dispose();
                    cameraReady = true;
                    UpdateUI();
                    Debug.Log("[Scan] ARCore camera image available — ready to capture");
                }
            }
        }

        public void StartScanning()
        {
            currentFaceIndex = 0;
            cubeState = new CubeState();
            isScanning = true;
            cameraReady = false;
            captureBlockTimer = 1.0f;

            // Hide the old WebCamTexture RawImage so the AR feed shows through
            if (cameraPreview != null) cameraPreview.gameObject.SetActive(false);

            if (arCameraManager == null)
                arCameraManager = FindFirstObjectByType<ARCameraManager>();

            MakeGridSquare();
            UpdateUI();
        }

        public void StopScanning()
        {
            isScanning = false;
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
            if (!cameraReady || arCameraManager == null)
            {
                Debug.LogWarning("[Scan] Camera not ready yet, ignoring capture");
                return;
            }
            if (captureBlockTimer > 0f) return;
            captureBlockTimer = 0.8f;

            if (!TryCaptureCenterSquare(out Texture2D square))
            {
                Debug.LogWarning("[Scan] Could not acquire camera image");
                return;
            }

            // Detect colors over the whole square (it is already the grid region)
            var region = new RectInt(0, 0, square.width, square.height);
            char[] raw = colorDetector.AnalyzeFace(square, region);
            char[] faceColors = RotateFacelets(raw, captureRotation);
            cubeState.faces[currentFaceIndex] = faceColors;

            // DIAGNOSTIC: sample the 9 cell centers' real RGB so we can see what
            // the camera image actually contains (channel order / cast / region).
            var sb = new System.Text.StringBuilder();
            for (int gr = 0; gr < 3; gr++)
                for (int gc = 0; gc < 3; gc++)
                {
                    int px = (int)((gc + 0.5f) / 3f * square.width);
                    int py = (int)((gr + 0.5f) / 3f * square.height);
                    Color cc = square.GetPixel(px, py);
                    sb.Append($"[{(int)(cc.r * 255)},{(int)(cc.g * 255)},{(int)(cc.b * 255)}] ");
                }
            Debug.Log($"[Scan] Face {currentFaceIndex} ({FaceNames[currentFaceIndex]}): {new string(faceColors)} (raw {new string(raw)})\n  cellRGB: {sb}");

            UpdateColorPreview(faceColors);
            Destroy(square);

            currentFaceIndex++;
            UpdateUI();

            if (currentFaceIndex >= 6)
            {
                isScanning = false;
                OnScanComplete?.Invoke();
            }
        }

        /// <summary>
        /// Acquire the latest ARCore CPU image and return its centered square
        /// cropped + downscaled to a small Texture2D for color analysis.
        /// </summary>
        private bool TryCaptureCenterSquare(out Texture2D result)
        {
            result = null;
            if (!arCameraManager.TryAcquireLatestCpuImage(out XRCpuImage image))
                return false;

            using (image)
            {
                // Crop only the centered region the user aims the cube into,
                // matching the on-screen grid box (gridScreenRatio of the view).
                // Taking the full square sampled background around the cube.
                int side = Mathf.RoundToInt(Mathf.Min(image.width, image.height) * gridScreenRatio);
                int ox = (image.width - side) / 2;
                int oy = (image.height - side) / 2;
                // Keep enough resolution that thin ring-sticker colors survive
                const int outSize = 240;

                var conv = new XRCpuImage.ConversionParams
                {
                    inputRect = new RectInt(ox, oy, side, side),
                    outputDimensions = new Vector2Int(outSize, outSize),
                    outputFormat = TextureFormat.RGBA32,
                    transformation = XRCpuImage.Transformation.None
                };

                int dataSize = image.GetConvertedDataSize(conv);
                var buffer = new NativeArray<byte>(dataSize, Allocator.Temp);
                image.Convert(conv, buffer);

                if (captureTex == null)
                    captureTex = new Texture2D(outSize, outSize, TextureFormat.RGBA32, false);
                captureTex.LoadRawTextureData(buffer);
                captureTex.Apply();
                buffer.Dispose();
            }

            // Return a copy the caller can Destroy, fixing channel order if needed
            Color[] px = captureTex.GetPixels();
            if (swapRedBlue)
            {
                for (int i = 0; i < px.Length; i++)
                {
                    var c = px[i];
                    px[i] = new Color(c.b, c.g, c.r, c.a);
                }
            }
            result = new Texture2D(captureTex.width, captureTex.height, TextureFormat.RGBA32, false);
            result.SetPixels(px);
            result.Apply();
            return true;
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
