using UnityEngine;

namespace GameJam.Combat
{
    public class Shooter : MonoBehaviour
    {
        [Header("Firing")]
        [SerializeField]
        private ShotPattern shotPattern;

        [SerializeField]
        private Transform firePoint;

        [SerializeField]
        private bool autoFire = false;

        [SerializeField]
        private float fireIntervalSeconds = 0.5f;

        [Header("Angle Offset Over Shots")]
        [Tooltip("If enabled, each shot will be rotated by an accumulating offset amount.")]
        [SerializeField]
        private bool useAngleOffsetOverShots = false;

        [SerializeField]
        private float angleOffsetPerShotDeg = 10f;

        private float accumulatedAngleOffsetDeg;
        private float timer;

        private void Reset()
        {
            firePoint = transform;
        }

        private void Update()
        {
            if (!autoFire || shotPattern == null)
            {
                return;
            }

            timer += Time.deltaTime;
            if (timer >= Mathf.Max(0.01f, fireIntervalSeconds))
            {
                timer = 0f;
                FireOnce();
            }
        }

        public void FireOnce()
        {
            if (shotPattern == null) return;
            Transform origin = firePoint != null ? firePoint : transform;

            float offsetToUse = useAngleOffsetOverShots ? accumulatedAngleOffsetDeg : 0f;
            shotPattern.FireWithAngleOffset(transform, origin, offsetToUse);

            if (useAngleOffsetOverShots)
            {
                accumulatedAngleOffsetDeg += angleOffsetPerShotDeg;
            }
        }

        public void SetPattern(ShotPattern pattern)
        {
            shotPattern = pattern;
        }

        public void SetAutofire(bool enabled)
        {
            autoFire = enabled;
        }

        public void SetInterval(float seconds)
        {
            fireIntervalSeconds = Mathf.Max(0.01f, seconds);
        }

        public void ResetAccumulatedAngle()
        {
            accumulatedAngleOffsetDeg = 0f;
        }

        public void SetAngleOffsetPerShot(float degrees)
        {
            angleOffsetPerShotDeg = degrees;
        }

        public void SetUseAngleOffsetOverShots(bool enabled)
        {
            useAngleOffsetOverShots = enabled;
        }
    }
} 