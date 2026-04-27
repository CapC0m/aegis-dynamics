using System.Collections.Generic;
using UnityEngine;

namespace AegisDynamics
{
    /// <summary>
    /// Aegis ring engine: heatshield-engine combo with N chambers in a ring.
    /// Differential throttling provides pitch/yaw thrust vector control.
    /// </summary>
    public class ModuleAegisRingEngine : ModuleEnginesFX, ITorqueProvider, IPartMassModifier
    {
        // ===== Editor tweakables (PAW slider) =====

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Thrust Chambers"),
         UI_FloatRange(minValue = 6f, maxValue = 36f, stepIncrement = 2f, scene = UI_Scene.Editor)]
        public float chamberCount = 18f;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true,
                  guiName = "TVC Authority", guiFormat = "P0"),
         UI_FloatRange(minValue = 0f, maxValue = 1f, stepIncrement = 0.05f, scene = UI_Scene.All)]
        public float tvcAuthority = 0.5f;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true,
                  guiName = "Differential TVC"),
         UI_Toggle(scene = UI_Scene.All)]
        public bool tvcEnabled = true;

        // ===== Cfg-driven =====

        [KSPField] public float ringRadius = 1.7f;
        [KSPField] public float thrustPerChamber = 60f;
        [KSPField] public float minThrottleFrac = 0.4f;
        [KSPField] public float nozzleOffsetY = 0f;
        [KSPField] public float baseMass = 2.7f;
        [KSPField] public float massPerChamber = 0.10f;

        // ===== PAW readout =====

        [KSPField(guiActive = true, guiName = "Chamber Throttles")]
        public string chamberDebug = "";

        // ===== Internal state =====

        private const string CHAMBER_TX_NAME = "aegisChamberTransform";
        private int lastBuiltCount = -1;
        private float lastBuiltRadius = -1f;
        private List<float> chamberAngles = new List<float>();
        private float lastRescaleIsp = -1f;

        // ===== Lifecycle =====

        public override void OnStart(StartState state)
        {
            BuildRing();
            base.OnStart(state);
            BindThrustTransforms();
            ConfigureThrust();
        }

        public void Update()
        {
            if (!HighLogic.LoadedSceneIsEditor) return;

            bool countChanged = (int)chamberCount != lastBuiltCount;
            bool radiusChanged = !Mathf.Approximately(ringRadius, lastBuiltRadius);

            if (!countChanged && !radiusChanged)
            {
                // Detect atmosphereCurve changes (B9PartSwitch fuel mode swap)
                if (atmosphereCurve != null)
                {
                    float currentVacIsp = atmosphereCurve.Evaluate(0f);
                    if (Mathf.Abs(currentVacIsp - lastRescaleIsp) > 0.5f)
                    {
                        ConfigureThrust();
                        lastRescaleIsp = currentVacIsp;
                    }
                }
                return;
            }

            BuildRing();
            BindThrustTransforms();
            ConfigureThrust();

            if (EditorLogic.fetch != null)
                GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
        }

        public override void OnFixedUpdate()
        {
            base.OnFixedUpdate();

            if (!HighLogic.LoadedSceneIsFlight) return;
            if (vessel == null || !vessel.loaded) return;
            if (thrustTransforms == null || thrustTransformMultipliers == null) return;
            if (thrustTransforms.Count == 0) return;
            if (thrustTransforms.Count != thrustTransformMultipliers.Count) return;

            if (!tvcEnabled || !isOperational || !EngineIgnited) return;
            if (currentThrottle < 0.01f) return;

            ApplyDifferentialThrottle(vessel.ctrlState.pitch, vessel.ctrlState.yaw);
            UpdateChamberDebug();
        }

        // ===== Ring construction =====

        private void BuildRing()
        {
            Transform anchor = part.transform.Find("model");
            if (anchor == null) anchor = part.transform;

            // Purge old chamber transforms
            var toRemove = new List<GameObject>();
            foreach (Transform child in anchor)
                if (child.name == CHAMBER_TX_NAME) toRemove.Add(child.gameObject);
            foreach (var go in toRemove) DestroyImmediate(go);

            int n = Mathf.Max(1, (int)chamberCount);
            chamberAngles.Clear();

            for (int i = 0; i < n; i++)
            {
                float angle = 2f * Mathf.PI * i / n;
                chamberAngles.Add(angle);

                GameObject chamber = new GameObject(CHAMBER_TX_NAME);
                chamber.transform.SetParent(anchor, false);
                chamber.transform.localPosition = new Vector3(
                    Mathf.Cos(angle) * ringRadius,
                    nozzleOffsetY,
                    Mathf.Sin(angle) * ringRadius
                );
                chamber.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }

            lastBuiltCount = n;
            lastBuiltRadius = ringRadius;
        }

        private void BindThrustTransforms()
        {
            thrustTransforms.Clear();
            thrustTransforms.AddRange(part.FindModelTransforms(CHAMBER_TX_NAME));

            thrustTransformMultipliers.Clear();
            int n = thrustTransforms.Count;
            float share = 1f / Mathf.Max(1, n);
            for (int i = 0; i < n; i++)
                thrustTransformMultipliers.Add(share);
        }

        private void ConfigureThrust()
        {
            int n = Mathf.Max(1, (int)chamberCount);
            maxThrust = thrustPerChamber * n;
            if (atmosphereCurve != null)
            {
                float vacIsp = atmosphereCurve.Evaluate(0f);
                if (vacIsp > 0.1f)
                    maxFuelFlow = maxThrust / (vacIsp * 9.80665f);
                lastRescaleIsp = vacIsp;
            }
        }

        // ===== Differential throttle TVC =====

        private void ApplyDifferentialThrottle(float pitchCmd, float yawCmd)
        {
            int n = thrustTransforms.Count;
            if (n == 0 || chamberAngles.Count != n) return;

            const float deadzone = 0.02f;
            if (Mathf.Abs(pitchCmd) < deadzone) pitchCmd = 0f;
            if (Mathf.Abs(yawCmd) < deadzone) yawCmd = 0f;

            float baseMult = 1f / n;
            float sum = 0f;

            for (int i = 0; i < n; i++)
            {
                float theta = chamberAngles[i];
                float cmd = pitchCmd * Mathf.Sin(theta) - yawCmd * Mathf.Cos(theta);
                float target = baseMult * (1f + cmd * tvcAuthority);
                target = Mathf.Clamp(target, baseMult * minThrottleFrac, baseMult * 2f);
                thrustTransformMultipliers[i] = target;
                sum += target;
            }

            // Normalize so total thrust is preserved
            if (sum > 0.0001f)
            {
                float k = 1f / sum;
                for (int i = 0; i < n; i++)
                    thrustTransformMultipliers[i] *= k;
            }
        }

        private void UpdateChamberDebug()
        {
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
                chamberDebug = $"({n} chambers)";
            }
        }

        // ===== ITorqueProvider =====

        public void GetPotentialTorque(out Vector3 pos, out Vector3 neg)
        {
            pos = neg = Vector3.zero;
            if (!tvcEnabled || !isOperational) return;

            // Use raw ringRadius — TweakScale modifies this value via exponent override.
            // Don't multiply by anchor scale; that would double-count.
            float thrustNow = finalThrust > 0f ? finalThrust : maxThrust;
            float effectiveArm = ringRadius * (1f - minThrottleFrac) / (1f + minThrottleFrac);
            float pitchYawTorque = thrustNow * effectiveArm * tvcAuthority * 0.6f;

            pos = new Vector3(pitchYawTorque, 0f, pitchYawTorque);
            neg = pos;
        }

        // ===== IPartMassModifier =====

        public float GetModuleMass(float defaultMass, ModifierStagingSituation sit)
        {
            int n = Mathf.Max(1, (int)chamberCount);
            return (baseMass + n * massPerChamber) - defaultMass;
        }

        public ModifierChangeWhen GetModuleMassChangeWhen()
        {
            return ModifierChangeWhen.CONSTANTLY;
        }
    }


    /// <summary>
    /// Active heatshield cooling: consumes propellant during reentry to dissipate heat flux.
    /// Replaces stock ablator. Activates when net flux exceeds threshold.
    /// </summary>
    public class ModuleAegisActiveCooling : PartModule
    {
        [KSPField] public float fluxThreshold = 500f;
        [KSPField] public float coolingPerFuelUnit = 8000f;
        [KSPField] public float maxCoolingRate = 15000f;
        [KSPField] public string coolantResourceName = "LiquidFuel";
        [KSPField] public string oxidizerResourceName = "Oxidizer";
        [KSPField] public float fuelRatio = 0.9f;
        [KSPField] public float oxidizerRatio = 1.1f;

        [KSPField(guiActive = true, guiName = "Heat Flux", guiFormat = "F1", guiUnits = " kW")]
        public float currentFlux = 0f;

        [KSPField(guiActive = true, guiName = "Coolant Flow", guiFormat = "F2", guiUnits = " /s")]
        public float coolantFlowRate = 0f;

        [KSPField(guiActive = true, guiName = "Cooling Active")]
        public bool coolingActive = false;

        private int coolantResourceID;
        private int oxidizerResourceID;

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            ResolveResources();
        }

        private void ResolveResources()
        {
            var coolantDef = PartResourceLibrary.Instance.GetDefinition(coolantResourceName);
            if (coolantDef != null) coolantResourceID = coolantDef.id;

            var oxDef = PartResourceLibrary.Instance.GetDefinition(oxidizerResourceName);
            if (oxDef != null) oxidizerResourceID = oxDef.id;
        }

        public override void OnFixedUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight) return;
            if (vessel == null) return;

            currentFlux = (float)(part.thermalConvectionFlux + part.thermalRadiationFlux);

            if (currentFlux <= 0 || currentFlux < fluxThreshold)
            {
                coolingActive = false;
                coolantFlowRate = 0f;
                return;
            }

            float fluxToDissipate = Mathf.Min(currentFlux, maxCoolingRate);
            float fuelDemandPerSec = fluxToDissipate / coolingPerFuelUnit;
            float fuelDemand = fuelDemandPerSec * TimeWarp.fixedDeltaTime;

            double fuelObtained = part.RequestResource(coolantResourceID, fuelDemand,
                ResourceFlowMode.STAGE_PRIORITY_FLOW);
            float oxDemand = fuelDemand * (oxidizerRatio / fuelRatio);
            double oxObtained = part.RequestResource(oxidizerResourceID, oxDemand,
                ResourceFlowMode.STAGE_PRIORITY_FLOW);

            double fuelFraction = fuelDemand > 0 ? fuelObtained / fuelDemand : 0;
            double oxFraction = oxDemand > 0 ? oxObtained / oxDemand : 0;
            double effectiveFraction = System.Math.Min(fuelFraction, oxFraction);
            double effectiveFuel = fuelDemand * effectiveFraction;

            if (effectiveFuel > 0)
            {
                coolingActive = true;
                coolantFlowRate = (float)(effectiveFuel / TimeWarp.fixedDeltaTime);
                float heatDissipated = (float)(effectiveFuel * coolingPerFuelUnit / TimeWarp.fixedDeltaTime);
                part.AddSkinThermalFlux(-heatDissipated);
            }
            else
            {
                coolingActive = false;
                coolantFlowRate = 0f;
            }
        }
    }
}