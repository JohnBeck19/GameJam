using System.Collections.Generic;
using UnityEngine;

namespace GameJam.Combat
{
    [DisallowMultipleComponent]
    public class PlayerAttackFormController : MonoBehaviour
    {
        [System.Serializable]
        public class FormConfig
        {
            [Header("Patterns")]
            public ShotPattern[] activePatterns;
            public ShotPattern[] persistentPatterns;

            [Header("Firing Settings")]
            [Tooltip("Fire interval for this form.")]
            public float fireIntervalSeconds = 0.35f;

            [Header("Optional Overrides")]
            [Tooltip("If set, overrides the shooter's fire point for this form.")]
            public Transform formFirePoint;

            [Tooltip("If >= 0, overrides the player's damage percent for this form (100 = base). Leave < 0 to keep current.")]
            public float damagePercentOverride = -1f;
        }

        [Header("References")]
        [SerializeField] private PlayerShooter shooter;

        [Header("Forms (low -> high severity)")]
        [SerializeField] private List<FormConfig> forms = new List<FormConfig>();

        [Header("Mapping")] 
        [Tooltip("If true, map forms to GameManager.VisualSeverity01. Index = floor(t * (N-1)).")]
        [SerializeField] private bool mapBySeverity = true;
        [Tooltip("If true, form index never decreases (uses peak severity so far).")]
        [SerializeField] private bool neverDecrease = true;

        [Tooltip("If not mapping by severity, explicitly select the form index via script with SetFormIndex().")]
        [SerializeField, Range(0, 10)] private int manualFormIndex = 0;

        private int _lastAppliedIndex = -1;
        private float _peakSeverity01 = 0f;

        private void Awake()
        {
            if (shooter == null)
            {
                shooter = GetComponent<PlayerShooter>();
                if (shooter == null)
                {
                    shooter = gameObject.AddComponent<PlayerShooter>();
                }
            }
        }

        private void OnEnable()
        {
            ApplyCurrentForm(force: true);
            _peakSeverity01 = 0f;
        }

        private void Update()
        {
            ApplyCurrentForm();
        }

        public void SetFormIndex(int index, bool force = false)
        {
            manualFormIndex = Mathf.Clamp(index, 0, Mathf.Max(0, forms.Count - 1));
            if (!mapBySeverity)
            {
                ApplyForm(manualFormIndex, force: force);
            }
        }

        private void ApplyCurrentForm(bool force = false)
        {
            if (forms == null || forms.Count == 0 || shooter == null)
                return;

            int index = ComputeFormIndex();
            if (!force && index == _lastAppliedIndex)
                return;

            ApplyForm(index, force: force);
        }

        private int ComputeFormIndex()
        {
            if (!mapBySeverity)
            {
                return Mathf.Clamp(manualFormIndex, 0, Mathf.Max(0, forms.Count - 1));
            }

            var gm = GameManager.Instance;
            // Use VisualSeverity01 for forms (can include time-based green curve)
            float t = gm != null ? gm.VisualSeverity01 : 0f;
            if (neverDecrease)
            {
                if (t > _peakSeverity01) _peakSeverity01 = t;
                t = _peakSeverity01;
            }
            int count = forms.Count;
            int idx = Mathf.Clamp(Mathf.FloorToInt(t * (count - 1 + 0.0001f)), 0, count - 1);
            return idx;
        }

        private void ApplyForm(int index, bool force = false)
        {
            index = Mathf.Clamp(index, 0, Mathf.Max(0, forms.Count - 1));
            var form = forms[index];

            // Active patterns
            if (form.activePatterns != null)
                shooter.SetActivePatterns(form.activePatterns);

            // Persistent patterns
            if (form.persistentPatterns != null)
                shooter.SetPersistentPatterns(form.persistentPatterns);

            // Fire interval
            shooter.SetInterval(Mathf.Max(0.01f, form.fireIntervalSeconds));

            // Damage percent override
            if (form.damagePercentOverride >= 0f)
            {
                shooter.SetDamagePercent(form.damagePercentOverride);
            }

            // Fire point override
            if (form.formFirePoint != null)
            {
                shooter.SetFirePoint(form.formFirePoint);
            }

            _lastAppliedIndex = index;
        }
    }
}


