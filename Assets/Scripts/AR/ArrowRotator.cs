using UnityEngine;

namespace RubiksCube.AR
{
    public class ArrowRotator : MonoBehaviour
    {
        [SerializeField] private float rotationSpeed = 120f;
        [SerializeField] private Vector3 rotationAxis = Vector3.up;

        private float direction = 1f;
        private bool isAnimating = false;

        public void SetDirection(int turns)
        {
            switch (turns)
            {
                case 1:
                    direction = 1f;
                    break;
                case -1:
                    direction = -1f;
                    break;
                case 2:
                    direction = 1f; // 180 just spins faster
                    rotationSpeed = 240f;
                    break;
            }
            isAnimating = true;
        }

        private void Update()
        {
            if (isAnimating)
            {
                transform.Rotate(rotationAxis, direction * rotationSpeed * Time.deltaTime, Space.Self);
            }
        }

        private void OnDisable()
        {
            isAnimating = false;
            rotationSpeed = 120f;
        }
    }
}
