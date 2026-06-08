using UnityEngine;
using RubiksCube.Data;

namespace RubiksCube.AR
{
    public class ARStepGuide : MonoBehaviour
    {
        [Header("箭頭 Prefab（可先用 Cube 替代）")]
        [SerializeField] private GameObject arrowU;
        [SerializeField] private GameObject arrowD;
        [SerializeField] private GameObject arrowF;
        [SerializeField] private GameObject arrowB;
        [SerializeField] private GameObject arrowL;
        [SerializeField] private GameObject arrowR;

        [Header("箭頭位置偏移（相對於 Anchor）")]
        [SerializeField] private float cubeSize = 0.06f; // ~6cm standard cube

        private GameObject[] arrows;
        private Animator[] animators;

        private void Awake()
        {
            arrows = new GameObject[] { arrowU, arrowD, arrowF, arrowB, arrowL, arrowR };
        }

        public void Initialize(Transform anchorTransform)
        {
            transform.SetParent(anchorTransform, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;

            PositionArrows();
            HideAllArrows();
        }

        private void PositionArrows()
        {
            float offset = cubeSize * 0.5f + 0.02f; // slight offset from cube surface

            if (arrowU != null) arrowU.transform.localPosition = new Vector3(0, offset, 0);
            if (arrowD != null) arrowD.transform.localPosition = new Vector3(0, -offset, 0);
            if (arrowF != null) arrowF.transform.localPosition = new Vector3(0, 0, offset);
            if (arrowB != null) arrowB.transform.localPosition = new Vector3(0, 0, -offset);
            if (arrowL != null) arrowL.transform.localPosition = new Vector3(-offset, 0, 0);
            if (arrowR != null) arrowR.transform.localPosition = new Vector3(offset, 0, 0);
        }

        public void ShowStep(MoveStep step)
        {
            HideAllArrows();

            GameObject arrow = GetArrowByFace(step.face);
            if (arrow == null)
            {
                Debug.LogWarning($"[ARGuide] No arrow prefab for face {step.face}");
                return;
            }

            arrow.SetActive(true);

            // Set rotation direction
            var rotator = arrow.GetComponent<ArrowRotator>();
            if (rotator != null)
            {
                rotator.SetDirection(step.turns);
            }

            // Try animator
            var animator = arrow.GetComponent<Animator>();
            if (animator != null)
            {
                string animState = step.turns switch
                {
                    1 => "CW",
                    -1 => "CCW",
                    2 => "Half",
                    _ => "CW"
                };
                animator.Play(animState);
            }

            Debug.Log($"[ARGuide] Showing step: {step.notation} ({step.GetDescription()})");
        }

        public void HideAllArrows()
        {
            foreach (var arrow in arrows)
            {
                if (arrow != null)
                    arrow.SetActive(false);
            }
        }

        private GameObject GetArrowByFace(CubeFace face)
        {
            return face switch
            {
                CubeFace.U => arrowU,
                CubeFace.D => arrowD,
                CubeFace.F => arrowF,
                CubeFace.B => arrowB,
                CubeFace.L => arrowL,
                CubeFace.R => arrowR,
                _ => null
            };
        }
    }
}
