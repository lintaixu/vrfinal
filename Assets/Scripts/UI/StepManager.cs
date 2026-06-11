using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using RubiksCube.Data;
using RubiksCube.AR;
using RubiksCube.ColorDetection;

namespace RubiksCube.UI
{
    public class StepManager : MonoBehaviour
    {
        [Header("UI 元件")]
        [SerializeField] private TextMeshProUGUI stepCountText;
        [SerializeField] private TextMeshProUGUI stepDescText;
        [SerializeField] private TextMeshProUGUI moveNotationText;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button prevButton;

        [Header("元件參考")]
        [SerializeField] private ARStepGuide arStepGuide;

        private List<MoveStep> steps;
        private int currentIndex = 0;

        private CubeDemo3D cubeDemo;
        private ColorDetector colorDetector;

        public System.Action OnAllStepsComplete;

        private void Start()
        {
            if (nextButton != null)
            {
                nextButton.onClick.AddListener(NextStep);
                var tmp = nextButton.GetComponentInChildren<TextMeshProUGUI>();
                if (tmp != null) tmp.text = "Next";
            }
            if (prevButton != null)
            {
                prevButton.onClick.AddListener(PrevStep);
                var tmp = prevButton.GetComponentInChildren<TextMeshProUGUI>();
                if (tmp != null) tmp.text = "Prev";
            }
        }

        public void LoadSteps(List<MoveStep> moveSteps, CubeState scannedState, ColorDetector detector)
        {
            steps = moveSteps;
            currentIndex = 0;
            colorDetector = detector;

            // Build the 3D demo cube (the primary, color-matched guide)
            if (cubeDemo == null)
            {
                cubeDemo = gameObject.AddComponent<CubeDemo3D>();
                cubeDemo.Initialize(transform);
            }
            if (scannedState != null)
                cubeDemo.LoadSolution(scannedState, steps, colorDetector);

            UpdateDisplay();
        }

        public void NextStep()
        {
            if (steps == null) return;

            if (currentIndex < steps.Count - 1)
            {
                currentIndex++;
                UpdateDisplay();
            }
            else
            {
                // All steps complete
                if (arStepGuide != null)
                    arStepGuide.HideAllArrows();
                OnAllStepsComplete?.Invoke();
            }
        }

        public void PrevStep()
        {
            if (steps == null) return;

            if (currentIndex > 0)
            {
                currentIndex--;
                UpdateDisplay();
            }
        }

        private void UpdateDisplay()
        {
            if (steps == null || steps.Count == 0) return;

            MoveStep step = steps[currentIndex];

            if (stepCountText != null)
                stepCountText.text = $"Step {currentIndex + 1} / {steps.Count}";

            if (stepDescText != null)
                stepDescText.text = step.GetDescription();

            if (moveNotationText != null)
                moveNotationText.text = step.notation;

            if (prevButton != null)
                prevButton.interactable = currentIndex > 0;

            if (nextButton != null)
                nextButton.GetComponentInChildren<TextMeshProUGUI>().text =
                    currentIndex >= steps.Count - 1 ? "Done" : "Next";

            // 3D virtual cube — the primary guide (matches by color)
            if (cubeDemo != null)
                cubeDemo.ShowStep(currentIndex);

            // AR arrow overlay (secondary; only active when an anchor exists)
            if (arStepGuide != null)
                arStepGuide.ShowStep(step);
        }
    }
}
