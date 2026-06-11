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
            // Recover references the editor auto-wirer missed (it used
            // GameObject.Find, which cannot see inactive panels, so buttons
            // inside hidden panels were never assigned).
            ResolveMissingReferences();

            // Set all UI text to English (default font doesn't support Chinese)
            InitializeUIText();

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
            bool touched = Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began;
            bool clicked = Input.GetMouseButtonDown(0);

            if (!touched && !clicked) return;

            Vector2 inputPos = touched ? Input.GetTouch(0).position : (Vector2)Input.mousePosition;

            switch (currentState)
            {
                case AppState.Start:
                    // Tap anywhere to start
                    OnStartClicked();
                    break;

                case AppState.PlaneDetection:
                    if (arSessionManager != null && arSessionManager.TryPlaceAnchor(inputPos))
                    {
                        if (arStepGuide != null && arSessionManager.CurrentAnchor != null)
                            arStepGuide.Initialize(arSessionManager.CurrentAnchor.transform);
                        SetState(AppState.Scanning);
                    }
                    #if UNITY_EDITOR
                    else
                    {
                        SetState(AppState.Scanning);
                    }
                    #endif
                    break;
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
                        planeHintText.text = "Point camera at a flat surface\nTap to place cube position";
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
                        completionText.text = $"Congratulations!\n{currentSolution.Count} moves total";
                    break;
            }

            Debug.Log($"[UI] State changed to: {state}");
        }

        private void SetPanel(GameObject panel, bool active)
        {
            if (panel != null) panel.SetActive(active);
        }

        private void ResolveMissingReferences()
        {
            if (startButton == null && startPanel != null)
                startButton = startPanel.GetComponentInChildren<Button>(true);

            if (confirmOkButton == null && confirmPanel != null)
                confirmOkButton = confirmPanel.GetComponentInChildren<Button>(true);

            if (restartButton == null && completionPanel != null)
                restartButton = completionPanel.GetComponentInChildren<Button>(true);

            if (planeHintText == null && planeDetectionPanel != null)
                planeHintText = planeDetectionPanel.GetComponentInChildren<TextMeshProUGUI>(true);

            if (completionText == null && completionPanel != null)
                completionText = completionPanel.GetComponentInChildren<TextMeshProUGUI>(true);

            Debug.Log($"[UI] References resolved: start={startButton != null}, confirmOk={confirmOkButton != null}, restart={restartButton != null}");
        }

        private void InitializeUIText()
        {
            // Override baked-in Chinese text with English
            SetPanelTitle(startPanel, "AR Rubik's Cube Solver");
            SetPanelButtonText(startPanel, "Start");
            SetPanelTitle(planeDetectionPanel, "Point camera at a flat surface...");
            SetPanelTitle(confirmPanel, "Confirm Scan Result");
            SetPanelButtonText(confirmPanel, "Confirm");
            SetPanelTitle(solvingPanel, "Solving...");
            SetPanelTitle(completionPanel, "Congratulations!");
            SetPanelButtonText(completionPanel, "Play Again");
        }

        private void SetPanelTitle(GameObject panel, string text)
        {
            if (panel == null) return;
            var title = panel.transform.Find("Title");
            if (title != null)
            {
                var tmp = title.GetComponent<TextMeshProUGUI>();
                if (tmp != null) tmp.text = text;
            }
        }

        private void SetPanelButtonText(GameObject panel, string text)
        {
            if (panel == null) return;
            var btn = panel.transform.Find("Button");
            if (btn != null)
            {
                var tmp = btn.GetComponentInChildren<TextMeshProUGUI>();
                if (tmp != null) tmp.text = text;
            }
        }

        #region Event Handlers

        private void OnStartClicked()
        {
            // Skip PlaneDetection for now - go straight to Scanning
            // TODO: Re-enable AR plane detection once scanning is working
            SetState(AppState.Scanning);
        }

        private void OnPlaneDetected(UnityEngine.XR.ARFoundation.ARPlane plane)
        {
            if (planeHintText != null)
                planeHintText.text = "Plane detected!\nTap to place cube position";
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
                // Stay on the confirm screen so the user can fix stickers
                // by tapping them instead of rescanning everything.
                var netView = confirmPanel != null ? confirmPanel.GetComponent<CubeNetView>() : null;
                if (netView != null)
                    netView.SetStatus($"Invalid: {error}", true);
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
            if (confirmPanel == null) return;

            CubeState state = scanningUI.ScannedState;

            // Interactive unfolded-cube preview: shows all 6 faces assembled
            // so the user can verify relative orientation, tap stickers to fix
            // colors, and rotate faces photographed at the wrong angle.
            var netView = confirmPanel.GetComponent<CubeNetView>();
            if (netView == null)
                netView = confirmPanel.AddComponent<CubeNetView>();

            netView.Show(state, colorDetector, confirmOkButton, () => SetState(AppState.Scanning));
        }

        private IEnumerator SolveCoroutine()
        {
            yield return null; // Wait one frame so UI updates

            CubeState state = scanningUI.ScannedState;

            // Run the solver on a background thread — it can take up to ~10s
            // and would otherwise freeze the whole UI.
            List<MoveStep> moves = null;
            string error = null;
            bool success = false;
            bool done = false;

            var thread = new System.Threading.Thread(() =>
            {
                try
                {
                    success = solver.Solve(state, out moves, out error);
                }
                catch (System.Exception e)
                {
                    success = false;
                    error = e.Message;
                }
                done = true;
            });
            thread.IsBackground = true;
            thread.Start();

            float elapsed = 0f;
            while (!done)
            {
                elapsed += Time.deltaTime;
                // First launch builds solver lookup tables (cached afterwards)
                SetPanelTitle(solvingPanel, elapsed > 3f
                    ? $"Solving... {elapsed:F0}s\n(first run builds tables, please wait)"
                    : $"Solving... {elapsed:F0}s");
                yield return null;
            }

            if (success && moves != null && moves.Count > 0)
            {
                currentSolution = moves;
                Debug.Log($"[UI] Solution found: {moves.Count} moves");
                SetState(AppState.StepGuide);
            }
            else if (success)
            {
                // Empty solution = cube is already solved
                currentSolution = new List<MoveStep>();
                Debug.Log("[UI] Cube is already solved!");
                SetState(AppState.Complete);
                if (completionText != null)
                    completionText.text = "Cube is already solved!\nScramble it and scan again.";
            }
            else
            {
                Debug.LogError($"[UI] Solve failed: {error}");
                // Return to the confirm screen — a failed solve usually means a
                // face was photographed at the wrong angle. The user can fix it
                // there (tap a center to rotate the face) instead of rescanning.
                SetState(AppState.Confirm);
                var netView = confirmPanel != null ? confirmPanel.GetComponent<CubeNetView>() : null;
                if (netView != null)
                    netView.SetStatus($"Solve failed: {error} — tap a center letter to fix face rotation, or Rescan", true);
            }
        }
    }
}
