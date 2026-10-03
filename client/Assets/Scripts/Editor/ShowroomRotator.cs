using UnityEngine;

namespace Megame.Editor
{
    /// <summary>
    /// Simple rotator for showroom display
    /// </summary>
    public class ShowroomRotator : MonoBehaviour
    {
        [Header("Rotation")]
        public float rotationSpeed = 10f;
        public Vector3 rotationAxis = Vector3.up;
        public bool autoStart = true;
        
        [Header("Auto-spin on hover")]
        public bool spinOnHover = true;
        public float hoverSpinMultiplier = 3f;
        
        [Header("Vertical bob")]
        public bool enableBob = true;
        public float bobHeight = 0.05f;
        public float bobSpeed = 1f;
        
        private bool isHovering = false;
        private float bobTimer = 0f;
        private Vector3 startPosition;
        
        private void Start()
        {
            startPosition = transform.localPosition;
            enabled = autoStart;
        }
        
        private void Update()
        {
            float speed = rotationSpeed;
            
            if (spinOnHover && isHovering)
            {
                speed *= hoverSpinMultiplier;
            }
            
            transform.Rotate(rotationAxis, speed * Time.deltaTime);
            
            if (enableBob)
            {
                bobTimer += Time.deltaTime * bobSpeed;
                float bobOffset = Mathf.Sin(bobTimer) * bobHeight;
                transform.localPosition = startPosition + Vector3.up * bobOffset;
            }
        }
        
        private void OnMouseEnter()
        {
            isHovering = true;
        }
        
        private void OnMouseExit()
        {
            isHovering = false;
        }
    }
}