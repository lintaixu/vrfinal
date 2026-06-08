using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using RubiksCube.Data;
using RubiksCube.Solver;
using RubiksCube.AR;

namespace RubiksCube.UI
{
    public enum AppState
    {
        Start,
        PlaneDetection,
        Scanning,
        Confirm,
        Solving,
        StepGuide,
        Complete
    }

    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [Header("面板")]
        [SerializeField] private GameObject startPanel;
        [SerializeField] private GameObject planeDetectionPanel;
        [SerializeField] private GameObject scanningPanel;
        [SerializeField] private GameObject confirmPanel;
        [SerializeField] private GameObject solvingPanel;
        [SerializeField] private GameObject stepPanel;
        [SerializeField] private GameObject completionPanel;

        [Header("啟動畫面")]
        [SerializeField] private Button startButton;

        [Header("平面偵測")]
        [SerializeField] private TextMeshProUGUI planeHintText;

        [Header("掃描確認")]
        [SerializeField] private Image[] confirmPreviewCells; // 54 cells
        [SerializeField] private Button confirmOkButton;
        [SerializeField] private Button confirmRetakeButton;

        [Header("完成畫面")]
        [SerializeField] private TextMeshProUGUI completionText;
        [SerializeField] private Button restartButton;

        [Header("元件參考")]
        [SerializeField] private ScanningUI scanningUI;
        [SerializeField] private StepManager stepManager;
        [SerializeField] private KociembaSolver solver;
        [SerializeField] private ARSessionManager arSessionManager;
        [SerializeField] private ARStepGuide arStepGuide;
        [SerializeField] private ColorDetection.ColorDetector colorDetector;

        private AppState currentState;
        private List<MoveStep> currentSolution;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(gameObject); return; }
        }

        private void Start()
        {
            // Wire up buttons
            if (startButton != null)
                startButton.onClick.AddListener(OnStartClicked);
            if (confirmOkButton != null)
                confirmOkButton.onClick.AddListener(OnConfirmOk);
            if (confirmRetakeButton != null)
                confirmRetakeButton.onClick.AddListener(OnConfirmRetake);
            if (restartButton != null)
                restartButton.onClick.AddListener(OnRestartClicked);

            // Wire up events
            if (scanningUI != null)
                scanningUI.OnScanComplete = OnScanComplete;
            if (stepManager != null)
                stepManager.OnAllStepsComplete = OnAllStepsComplete;
            if (arSessionManager != null)
                arSessionManager.OnPlaneDetected += OnPlaneDetected;

            SetState(AppState.Start);
        }

        private void Update()
        {
            // Handle plane detection touch
            if (currentState == AppState.PlaneDetection)
            {
                if (Input.touchCount > 0)
                {
                    Touch touch = Input.GetTouch(0);
                    if (touch.phase == TouchPhase.Began)
                    {
                        if (arSessionManager != null && arSessionManager.TryPlaceAnchor(touch.position))
                        {
                            // Initialize AR guide at anchor
                            if (arStepGuide != null && arSessionManager.CurrentAnchor != null)
                                arStepGuide.Initialize(arSessionManager.CurrentAnchor.transform);

                            SetState(AppState.Scanning);
                        }
                    }
                }

                // Mouse click for editor testing
                if (Input.GetMouseButtonDown(0))
                {
                    if (arSessionManager != null && arSessionManager.TryPlaceAnchor(Input.mousePosition))
                    {
                        if (arStepGuide != null && arSessionManager.CurrentAnchor != null)
                            arStepGuide.Initialize(arSessionManager.CurrentAnchor.transform);

                        SetState(AppState.Scanning);
                    }
                    #if UNITY_EDITOR
                    // In editor without AR, just skip to scanning
                    else
                    {
                        SetState(AppState.Scanning);
                    }
                    #endif
                }
            }
        }

        private void SetState(AppState state)
        {
            currentState = state;

            // Hide all panels
            SetPanel(startPanel, false);
            SetPanel(planeDetectionPanel, false);
            SetPanel(scanningPanel, false);
            SetPanel(confirmPanel, false);
            SetPanel(solvingPanel, false);
            SetPanel(stepPanel, false);
            SetPanel(completionPanel, false);

            switch (state)
            {
                case AppState.Start:
                    SetPanel(startPanel, true);
                    break;

                case AppState.PlaneDetection:
                    SetPanel(planeDetectionPanel, true);
                    if (planeHintText != null)
                        planeHintText.text = "請將相機對準桌面\n偵測到平面後，點擊桌面放置方塊位置";
                    break;

                case AppState.Scanning:
                    SetPanel(scanningPanel, true);
                    if (scanningUI != null)
                        scanningUI.StartScanning();
                    break;

                case AppState.Confirm:
                    SetPanel(confirmPanel, true);
                    ShowConfirmPreview();
                    break;

                case AppState.Solving:
                    SetPanel(solvingPanel, true);
                    StartCoroutine(SolveCoroutine());
                    break;

                case AppState.StepGuide:
                    SetPanel(stepPanel, true);
                    if (stepManager != null && currentSolution != null)
                        stepManager.LoadSteps(currentSolution);
                    break;

                case AppState.Complete:
                    SetPanel(completionPanel, true);
                    if (completionText != null && currentSolution != null)
                        completionText.text = $"恭喜完成！\n共 {currentSolution.Count} 步";
                    break;
            }

            Debug.Log($"[UI] State changed to: {state}");
        }

        private void SetPanel(GameObject panel, bool active)
        {
            if (panel != null) panel.SetActive(active);
        }

        #region Event Handlers

        private void OnStartClicked()
        {
            SetState(AppState.PlaneDetection);
        }

        private void OnPlaneDetected(UnityEngine.XR.ARFoundation.ARPlane plane)
        {
            if (planeHintText != null)
                planeHintText.text = "已偵測到平面！\n點擊桌面放置方塊位置";
        }

        private void OnScanComplete()
        {
            scanningUI.StopScanning();
            SetState(AppState.Confirm);
        }

        private void OnConfirmOk()
        {
            // Validate
            CubeState state = scanningUI.ScannedState;
            if (state.Validate(out string error))
            {
                SetState(AppState.Solving);
            }
            else
            {
                Debug.LogWarning($"[UI] Validation failed: {error}");
                // Show error and go back to scan
                if (planeHintText != null)
                    planeHintText.text = $"驗證失敗：{error}\n請重新掃描";
                SetState(AppState.Scanning);
            }
        }

        private void OnConfirmRetake()
        {
            SetState(AppState.Scanning);
        }

        private void OnAllStepsComplete()
        {
            SetState(AppState.Complete);
        }

        private void OnRestartClicked()
        {
            currentSolution = null;
            if (arSessionManager != null)
                arSessionManager.Reset();
            if (arStepGuide != null)
                arStepGuide.HideAllArrows();
            SetState(AppState.Start);
        }

        #endregion

        private void ShowConfirmPreview()
        {
            if (confirmPreviewCells == null || colorDetector == null) return;

            CubeState state = scanningUI.ScannedState;
            int cellIdx = 0;
            for (int face = 0; face < 6 && face < state.faces.Length; face++)
            {
                for (int i = 0; i < 9 && cellIdx < confirmPreviewCells.Length; i++, cellIdx++)
                {
                    if (confirmPreviewCells[cellIdx] != null)
                        confirmPreviewCells[cellIdx].color = colorDetector.GetPreviewColor(state.faces[face][i]);
                }
            }
        }

        private IEnumerator SolveCoroutine()
        {
            yield return null; // Wait one frame so UI updates

            CubeState state = scanningUI.ScannedState;

            bool success = solver.Solve(state, out List<MoveStep> moves, out string error);

            if (success)
            {
                currentSolution = moves;
                Debug.Log($"[UI] Solution found: {moves.Count} moves");
                SetState(AppState.StepGuide);
            }
            else
            {
                Debug.LogError($"[UI] Solve failed: {error}");
                // Go back to scanning
                SetState(AppState.Scanning);
            }
        }
    }
}
