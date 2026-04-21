using System.Collections.Generic;
using UnityEngine;
using KSP.UI.Screens;
using System.Text;

namespace AegisDynamics
{
    public class ModuleAegisRingEngine : ModuleEnginesFX, ITorqueProvider
    {

        // ---- Editor tweakables ----
        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Thrust Chambers"),
         UI_FloatRange(minValue = 3f, maxValue = 24f, stepIncrement = 1f, scene = UI_Scene.Editor)]
        public float chamberCount = 6f;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true,
                  guiName = "TVC Authority", guiFormat = "P0"),
         UI_FloatRange(minValue = 0f, maxValue = 1f, stepIncrement = 0.05f, scene = UI_Scene.All)]
        public float tvcAuthority = 0.5f;   // fraction of per-chamber throttle available for TVC

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true,
                  guiName = "Differential TVC"),
         UI_Toggle(scene = UI_Scene.All)]
        public bool tvcEnabled = true;

        [KSPField(guiActive = true, guiName = "Chamber Throttles")]
        public string chamberDebug = "";

        // ---- Config tweakables ----
        [KSPField(isPersistant = true)] public float ringRadius = 0.6f;
        [KSPField(isPersistant = true)] public float thrustPerChamber = 60f;
        [KSPField] public float minThrottleFrac = 0.4f;    // minimum per-chamber throttle
        [KSPField] public float nozzleOffsetY = -0.2f;   // how far below the shield the chambers sit
        // Max fraction of full authority that can change per second
        [KSPField] public float tvcSlewRate = 4.0f;   // range [1..10]; higher = snappier, more oscillation

        // ---- Internal state ----
        private const string CLONE_TX_NAME = "aegisChamberTransform";
        private int lastBuiltCount = -1;
        private float lastBuiltRadius = -1f;
        private List<float> chamberAngles = new List<float>();  // radians, one per chamber

        public override void OnStart(StartState state)
        {
            BuildRing();
            base.OnStart(state);
            RebindTransforms();
            RescaleThrust();
        }

        public void Update()
        {
            if (!HighLogic.LoadedSceneIsEditor) return;

            bool countChanged = (int)chamberCount != lastBuiltCount;
            bool radiusChanged = !Mathf.Approximately(ringRadius, lastBuiltRadius);

            if (!countChanged && !radiusChanged) return;

            Debug.Log($"[AegisEngine] Update triggered: count={chamberCount} (was {lastBuiltCount}), " +
                      $"radius={ringRadius} (was {lastBuiltRadius})");

            BuildRing();
            RebindTransforms();
            RescaleThrust();

            if (EditorLogic.fetch != null)
                GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
        }

        // FixedUpdate = physics tick. Called before base-class thrust is applied,
        // so mutating thrustTransformMultipliers here affects this tick's thrust.
        public override void OnFixedUpdate()
        {
            base.OnFixedUpdate();

            if (!HighLogic.LoadedSceneIsFlight) return;
            if (vessel == null || !vessel.loaded) return;
            if (thrustTransforms == null || thrustTransformMultipliers == null) return;
            if (thrustTransforms.Count == 0) return;
            if (thrustTransforms.Count != thrustTransformMultipliers.Count) return;

            if (!tvcEnabled) return;
            if (!isOperational) return;
            if (!EngineIgnited) return;
            if (currentThrottle < 0.01f) return;

            var ctrl = vessel.ctrlState;
            ApplyDifferentialThrottle(ctrl.pitch, ctrl.yaw);

            // Per-chamber debug readout for the PAW
            int n = thrustTransformMultipliers.Count;
            if (n > 0 && n <= 12)
            {
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < n; i++)
                    sb.Append($"{(thrustTransformMultipliers[i] * n * 100f):F0}% ");
                chamberDebug = sb.ToString().TrimEnd();
            }
            else
            {
                chamberDebug = $"({n} chambers, too many to display)";
            }
        }

        // ---- ITorqueProvider ----
        // SAS calls this to ask how much torque authority we have on each axis.
        // pos = max positive torque, neg = max negative (use absolute values; SAS expects magnitudes).
        public void GetPotentialTorque(out Vector3 pos, out Vector3 neg)
        {
            pos = neg = Vector3.zero;
            if (!tvcEnabled || !isOperational) return;

            float thrustNow = finalThrust > 0f ? finalThrust : maxThrust;
            float effectiveArm = ringRadius * (1f - minThrottleFrac) / (1f + minThrottleFrac);
            float pitchYawTorque = thrustNow * effectiveArm * tvcAuthority * 0.6f;  // 0.6 = "honest" factor

            // x = pitch, y = roll, z = yaw. Engine provides no roll authority.
            pos = new Vector3(pitchYawTorque, 0f, pitchYawTorque);
            neg = pos;
        }

        // ---- Core TVC math ----
        private void ApplyDifferentialThrottle(float pitchCmd, float yawCmd)
        {
            int n = thrustTransforms.Count;
            if (n == 0 || chamberAngles.Count != n) return;

            const float INPUT_DEADZONE = 0.02f;
            if (Mathf.Abs(pitchCmd) < INPUT_DEADZONE) pitchCmd = 0f;
            if (Mathf.Abs(yawCmd) < INPUT_DEADZONE) yawCmd = 0f;

            float baseMult = 1f / n;
            float maxDelta = tvcSlewRate * TimeWarp.fixedDeltaTime;
            const float smoothFactor = 0.2f;
            float sum = 0f;

            for (int i = 0; i < n; i++)
            {
                float theta = chamberAngles[i];
                float cmd = pitchCmd * Mathf.Sin(theta) - yawCmd * Mathf.Cos(theta);
                float target = baseMult * (1f + cmd * tvcAuthority);
                target = Mathf.Clamp(target, baseMult * minThrottleFrac, baseMult * 2f);

                float current = thrustTransformMultipliers[i];
                float delta = target - current;
                float next = current + delta * smoothFactor;
                next = Mathf.Clamp(next, current - maxDelta * baseMult, current + maxDelta * baseMult);

                thrustTransformMultipliers[i] = next;
                sum += next;
            }

            if (sum > 0.0001f)
            {
                float k = 1f / sum;
                for (int i = 0; i < n; i++)
                    thrustTransformMultipliers[i] *= k;
            }
        }


        // ---- Ring construction ----
        private void BuildRing()
        {
            // Heatshield parts don't have a thrustTransform. Anchor the ring
            // under the part's model root instead.
            Transform anchor = part.transform.Find("model");
            if (anchor == null) anchor = part.transform;

            // Purge any prior clones
            var toKill = new List<GameObject>();
            foreach (Transform child in anchor)
                if (child.name == CLONE_TX_NAME) toKill.Add(child.gameObject);
            foreach (var go in toKill) DestroyImmediate(go);

            int n = Mathf.Max(1, (int)chamberCount);
            chamberAngles.Clear();
            for (int i = 0; i < n; i++)
            {
                float angleDeg = 360f * i / n;
                float a = angleDeg * Mathf.Deg2Rad;
                chamberAngles.Add(a);

                GameObject clone = new GameObject(CLONE_TX_NAME);
                clone.transform.SetParent(anchor, false);
                // Ring sits below the shield (negative Y in part-local space)
                // and thrust points down the -Y axis. Model roots in KSP use
                // Y as the vertical axis.
                clone.transform.localPosition = new Vector3(Mathf.Cos(a) * ringRadius,nozzleOffsetY,Mathf.Sin(a) * ringRadius);
                // Rotate so +Z of the transform (which is "forward" = exhaust direction
                // for KSP's engine system) points down.
                clone.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }

            lastBuiltCount = n;
            lastBuiltRadius = ringRadius;
        }

        private void RebindTransforms()
        {
            thrustTransforms.Clear();
            thrustTransforms.AddRange(part.FindModelTransforms(CLONE_TX_NAME));

            thrustTransformMultipliers.Clear();
            float share = 1f / Mathf.Max(1, thrustTransforms.Count);
            for (int i = 0; i < thrustTransforms.Count; i++)
                thrustTransformMultipliers.Add(share);
        }

        private void RescaleThrust()
        {
            int n = Mathf.Max(1, (int)chamberCount);
            maxThrust = thrustPerChamber * n;
            if (atmosphereCurve != null)
            {
                float vacIsp = atmosphereCurve.Evaluate(0f);
                if (vacIsp > 0.1f)
                    maxFuelFlow = maxThrust / (vacIsp * 9.80665f);
            }
            Debug.Log($"[AegisEngine] RescaleThrust: thrustPerChamber={thrustPerChamber}, " +
                      $"n={n}, maxThrust={maxThrust}, maxFuelFlow={maxFuelFlow}");
        }
    }
}