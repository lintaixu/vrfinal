using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RubiksCube.Data;
using RubiksCube.ColorDetection;

namespace RubiksCube.UI
{
    public class ScanningUI : MonoBehaviour
    {
        [Header("UI 元件")]
        [SerializeField] private TextMeshProUGUI progressText;
        [SerializeField] private TextMeshProUGUI hintText;
        [SerializeField] private Button captureButton;
        [SerializeField] private Button retakeButton;
        [SerializeField] private RectTransform gridOverlay;
        [SerializeField] private Image[] colorPreviewCells; // 9 cells for preview

        [Header("格線設定")]
        [SerializeField] private float gridScreenRatio = 0.6f;

        [Header("元件參考")]
        [SerializeField] private ColorDetector colorDetector;
        [SerializeField] private WebCamTexture webCamTexture;
        [SerializeField] private RawImage cameraPreview;

        private CubeState cubeState;
        private int currentFaceIndex = 0;
        private bool isScanning = false;

        // Face scan order: U, R, F, D, L, B
        private static readonly string[] FaceNames = {
            "上面 (U) - 白色中心朝上", "右面 (R) - 紅色中心",
            "前面 (F) - 綠色中心", "下面 (D) - 黃色中心朝上",
            "左面 (L) - 橙色中心", "後面 (B) - 藍色中心"
        };

        public CubeState ScannedState => cubeState;
        public bool IsComplete => currentFaceIndex >= 6;

        public System.Action OnScanComplete;

        private void Start()
        {
            cubeState = new CubeState();

            if (captureButton != null)
                captureButton.onClick.AddListener(OnCaptureClicked);
            if (retakeButton != null)
                retakeButton.onClick.AddListener(OnRetakeClicked);
        }

        public void StartScanning()
        {
            currentFaceIndex = 0;
            cubeState = new CubeState();
            isScanning = true;
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

                webCamTexture = new WebCamTexture(camName, 1280, 720, 30);
            }

            webCamTexture.Play();

            if (cameraPreview != null)
            {
                cameraPreview.texture = webCamTexture;
                cameraPreview.gameObject.SetActive(true);
            }
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

            // Capture current frame
            Texture2D snapshot = new Texture2D(webCamTexture.width, webCamTexture.height, TextureFormat.RGB24, false);
            snapshot.SetPixels(webCamTexture.GetPixels());
            snapshot.Apply();

            // Define grid region (center of frame)
            float gridSize = Mathf.Min(snapshot.width, snapshot.height) * gridScreenRatio;
            int gx = (int)((snapshot.width - gridSize) / 2);
            int gy = (int)((snapshot.height - gridSize) / 2);
            var region = new RectInt(gx, gy, (int)gridSize, (int)gridSize);

            // Detect colors
            char[] faceColors = colorDetector.AnalyzeFace(snapshot, region);
            cubeState.faces[currentFaceIndex] = faceColors;

            // Update preview
            UpdateColorPreview(faceColors);

            // Log
            Debug.Log($"[Scan] Face {currentFaceIndex} ({FaceNames[currentFaceIndex]}): {new string(faceColors)}");

            Destroy(snapshot);

            currentFaceIndex++;
            UpdateUI();

            if (currentFaceIndex >= 6)
            {
                isScanning = false;
                StopCamera();
                OnScanComplete?.Invoke();
            }
        }

        private void OnRetakeClicked()
        {
            if (currentFaceIndex > 0)
            {
                currentFaceIndex--;
                UpdateUI();
            }
        }

        private void UpdateUI()
        {
            if (progressText != null)
                progressText.text = $"已掃描 {currentFaceIndex} / 6 面";

            if (hintText != null && currentFaceIndex < 6)
                hintText.text = $"請對準：{FaceNames[currentFaceIndex]}";
            else if (hintText != null)
                hintText.text = "掃描完成！";

            if (retakeButton != null)
                retakeButton.interactable = currentFaceIndex > 0;

            if (captureButton != null)
                captureButton.interactable = currentFaceIndex < 6;
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
