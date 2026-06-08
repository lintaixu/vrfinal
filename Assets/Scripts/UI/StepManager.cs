using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using RubiksCube.Data;
using RubiksCube.AR;

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

        public System.Action OnAllStepsComplete;

        private void Start()
        {
            if (nextButton != null)
                nextButton.onClick.AddListener(NextStep);
            if (prevButton != null)
                prevButton.onClick.AddListener(PrevStep);
        }

        public void LoadSteps(List<MoveStep> moveSteps)
        {
            steps = moveSteps;
            currentIndex = 0;
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
                stepCountText.text = $"步驟 {currentIndex + 1} / {steps.Count}";

            if (stepDescText != null)
                stepDescText.text = step.GetDescription();

            if (moveNotationText != null)
                moveNotationText.text = step.notation;

            if (prevButton != null)
                prevButton.interactable = currentIndex > 0;

            if (nextButton != null)
                nextButton.GetComponentInChildren<TextMeshProUGUI>().text =
                    currentIndex >= steps.Count - 1 ? "完成" : "下一步";

            // Update AR display
            if (arStepGuide != null)
                arStepGuide.ShowStep(step);
        }
    }
}
