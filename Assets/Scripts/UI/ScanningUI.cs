using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RubiksCube.Data;
using RubiksCube.ColorDetection;

namespace RubiksCube.UI
{
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

        [Header("Component References")]
        [SerializeField] private ColorDetector colorDetector;
        [SerializeField] private RawImage cameraPreview;

        private WebCamTexture webCamTexture;
        private CubeState cubeState;
        private int currentFaceIndex = 0;
        private bool isScanning = false;
        private bool cameraReady = false;
        private float captureBlockTimer = 0f; // Prevent accidental rapid captures

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

            // Check if camera is ready (needs a few frames to initialize)
            if (webCamTexture != null && webCamTexture.isPlaying && !cameraReady)
            {
                if (webCamTexture.width > 100) // Real frames loaded
                {
                    cameraReady = true;
                    ApplyCameraRotation();
                    Debug.Log($"[Scan] Camera ready: {webCamTexture.width}x{webCamTexture.height}, rotation={webCamTexture.videoRotationAngle}");
                }
            }
        }

        public void StartScanning()
        {
            currentFaceIndex = 0;
            cubeState = new CubeState();
            isScanning = true;
            cameraReady = false;
            captureBlockTimer = 1.5f; // Block captures for 1.5s to prevent accidental taps
            InitCamera();
            UpdateUI();
        }

        public void StopScanning()
        {
            isScanning = false;
            StopCamera();
        }

        private void InitCamera()
        {
            if (webCamTexture == null)
            {
                WebCamDevice[] devices = WebCamTexture.devices;
                if (devices.Length == 0)
                {
                    Debug.LogError("[Scan] No camera found!");
                    return;
                }

                // Prefer back camera
                string camName = devices[0].name;
                foreach (var d in devices)
                {
                    if (!d.isFrontFacing)
                    {
                        camName = d.name;
                        break;
                    }
                }

                Debug.Log($"[Scan] Using camera: {camName}");
                webCamTexture = new WebCamTexture(camName, 1280, 720, 30);
            }

            webCamTexture.Play();

            if (cameraPreview != null)
            {
                cameraPreview.texture = webCamTexture;
                cameraPreview.gameObject.SetActive(true);
            }
        }

        private void ApplyCameraRotation()
        {
            if (cameraPreview == null || webCamTexture == null) return;

            // Fix rotation for Android cameras
            int rot = webCamTexture.videoRotationAngle;
            bool mirror = webCamTexture.videoVerticallyMirrored;

            cameraPreview.rectTransform.localEulerAngles = new Vector3(0, 0, -rot);

            // Adjust scale for mirroring
            if (mirror)
                cameraPreview.rectTransform.localScale = new Vector3(1, -1, 1);
            else
                cameraPreview.rectTransform.localScale = new Vector3(1, 1, 1);

            // Adjust aspect ratio to fill screen properly
            float aspectRatio = (float)webCamTexture.width / webCamTexture.height;
            if (rot == 90 || rot == 270)
            {
                // Camera is rotated, swap width/height for sizing
                float parentHeight = cameraPreview.rectTransform.parent.GetComponent<RectTransform>().rect.height;
                float parentWidth = cameraPreview.rectTransform.parent.GetComponent<RectTransform>().rect.width;
                float scale = Mathf.Max(parentWidth / (float)webCamTexture.height, parentHeight / (float)webCamTexture.width);
                cameraPreview.rectTransform.sizeDelta = new Vector2(webCamTexture.width * scale, webCamTexture.height * scale);
            }

            Debug.Log($"[Scan] Camera rotation applied: angle={rot}, mirror={mirror}");
        }

        private void StopCamera()
        {
            if (webCamTexture != null && webCamTexture.isPlaying)
                webCamTexture.Stop();
        }

        private void OnCaptureClicked()
        {
            if (!isScanning || currentFaceIndex >= 6) return;
            if (webCamTexture == null || !webCamTexture.isPlaying) return;
            if (!cameraReady)
            {
                Debug.LogWarning("[Scan] Camera not ready yet, ignoring capture");
                return;
            }
            if (captureBlockTimer > 0f)
            {
                Debug.Log("[Scan] Capture blocked (cooldown)");
                return;
            }

            // Set cooldown to prevent rapid taps
            captureBlockTimer = 0.8f;

            // Capture current frame - handle rotation
            int w = webCamTexture.width;
            int h = webCamTexture.height;
            Texture2D snapshot = new Texture2D(w, h, TextureFormat.RGB24, false);
            snapshot.SetPixels(webCamTexture.GetPixels());
            snapshot.Apply();

            // Rotate snapshot to correct orientation if needed
            int rotation = webCamTexture.videoRotationAngle;
            bool mirrored = webCamTexture.videoVerticallyMirrored;
            Texture2D corrected = RotateTexture(snapshot, rotation, mirrored);
            Destroy(snapshot);

            // Define grid region (center of corrected frame)
            float gridSize = Mathf.Min(corrected.width, corrected.height) * gridScreenRatio;
            int gx = (int)((corrected.width - gridSize) / 2);
            int gy = (int)((corrected.height - gridSize) / 2);
            var region = new RectInt(gx, gy, (int)gridSize, (int)gridSize);

            // Detect colors
            char[] faceColors = colorDetector.AnalyzeFace(corrected, region);
            cubeState.faces[currentFaceIndex] = faceColors;

            // Log detected colors with debug info
            Color avgCenter = colorDetector.GetAverageColor(corrected, corrected.width / 2, corrected.height / 2, 40);
            Color.RGBToHSV(avgCenter, out float ch, out float cs, out float cv);
            Debug.Log($"[Scan] Face {currentFaceIndex} ({FaceNames[currentFaceIndex]}): {new string(faceColors)} | Center HSV=({ch * 360:F0},{cs:F2},{cv:F2}) | Tex={corrected.width}x{corrected.height}");

            // Update preview
            UpdateColorPreview(faceColors);

            Destroy(corrected);

            currentFaceIndex++;
            UpdateUI();

            if (currentFaceIndex >= 6)
            {
                isScanning = false;
                StopCamera();
                OnScanComplete?.Invoke();
            }
        }

        private Texture2D RotateTexture(Texture2D src, int angle, bool mirror)
        {
            if (angle == 0 && !mirror) return DuplicateTexture(src);

            Color[] srcPixels = src.GetPixels();
            int srcW = src.width;
            int srcH = src.height;

            int dstW, dstH;
            if (angle == 90 || angle == 270)
            {
                dstW = srcH;
                dstH = srcW;
            }
            else
            {
                dstW = srcW;
                dstH = srcH;
            }

            Color[] dstPixels = new Color[dstW * dstH];

            for (int y = 0; y < dstH; y++)
            {
                for (int x = 0; x < dstW; x++)
                {
                    int srcX, srcY;
                    switch (angle)
                    {
                        case 90:
                            srcX = y;
                            srcY = dstW - 1 - x;
                            break;
                        case 180:
                            srcX = srcW - 1 - x;
                            srcY = srcH - 1 - y;
                            break;
                        case 270:
                            srcX = srcH - 1 - y;
                            srcY = x;
                            break;
                        default:
                            srcX = x;
                            srcY = y;
                            break;
                    }

                    if (mirror) srcY = srcH - 1 - srcY;

                    if (srcX >= 0 && srcX < srcW && srcY >= 0 && srcY < srcH)
                        dstPixels[y * dstW + x] = srcPixels[srcY * srcW + srcX];
                }
            }

            Texture2D dst = new Texture2D(dstW, dstH, TextureFormat.RGB24, false);
            dst.SetPixels(dstPixels);
            dst.Apply();
            return dst;
        }

        private Texture2D DuplicateTexture(Texture2D src)
        {
            Texture2D dst = new Texture2D(src.width, src.height, src.format, false);
            dst.SetPixels(src.GetPixels());
            dst.Apply();
            return dst;
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
                hintText.text = $"Aim at: {FaceNames[currentFaceIndex]}";
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
