using System.Collections.Generic;
using UnityEngine;

namespace AegisDynamics
{
    /// <summary>
    /// Aegis ring engine: a heatshield-engine combo with N chambers in a ring,
    /// gimbaled together via stock ModuleGimbal for thrust vector control.
    /// </summary>
    public class ModuleAegisRingEngine : ModuleEnginesFX, IPartMassModifier
    {
        // ===== Editor tweakables (PAW slider) =====

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Thrust Chambers"),
         UI_FloatRange(minValue = 6f, maxValue = 36f, stepIncrement = 2f, scene = UI_Scene.Editor)]
        public float chamberCount = 18f;

        // ===== Cfg-driven =====

        [KSPField] public float ringRadius = 1.7f;
        [KSPField] public float thrustPerChamber = 60f;
        [KSPField] public float nozzleOffsetY = 0f;
        [KSPField] public float baseMass = 5.6f;
        [KSPField] public float massPerChamber = 0.10f;
        [KSPField] public float gimbalThrustPenalty = 0.10f;

        // ===== Internal state =====

        private const string CHAMBER_TX_NAME = "aegisChamberTransform";
        private const string GIMBAL_ANCHOR_NAME = "aegisGimbalAnchor";
        private const string PLUME_TX_NAME = "aegisPlumeTransform";
        private int lastBuiltCount = -1;
        private float lastBuiltRadius = -1f;
        private float lastRescaleIsp = -1f;
        private float baseMaxFuelFlow = -1f;

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
            if (HighLogic.LoadedSceneIsFlight && vessel != null && vessel.loaded)
            {
                ApplyGimbalThrustReduction();
            }

            base.OnFixedUpdate();
        }

        // ===== Ring construction =====

        private void BuildRing()
        {
            Transform anchor = part.transform.Find("model");
            if (anchor == null) anchor = part.transform;

            // Purge old transforms
            var toRemove = new List<GameObject>();
            foreach (Transform child in anchor)
            {
                if (child.name == CHAMBER_TX_NAME ||
                    child.name == GIMBAL_ANCHOR_NAME ||
                    child.name == PLUME_TX_NAME)
                {
                    toRemove.Add(child.gameObject);
                }
            }
            foreach (var go in toRemove) DestroyImmediate(go);

            // Gimbal anchor at part origin, oriented so its Z points along part -Y (thrust direction)
            GameObject gimbalAnchor = new GameObject(GIMBAL_ANCHOR_NAME);
            gimbalAnchor.transform.SetParent(anchor, false);
            gimbalAnchor.transform.localPosition = Vector3.zero;
            gimbalAnchor.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            int n = Mathf.Max(1, (int)chamberCount);
            for (int i = 0; i < n; i++)
            {
                float angle = 2f * Mathf.PI * i / n;
                float cosA = Mathf.Cos(angle);
                float sinA = Mathf.Sin(angle);

                // Chamber transform (used for thrust): under gimbal anchor, gimbals with engine
                GameObject chamber = new GameObject(CHAMBER_TX_NAME);
                chamber.transform.SetParent(gimbalAnchor.transform, false);
                chamber.transform.localPosition = new Vector3(
                    cosA * ringRadius,
                    sinA * ringRadius,
                    -nozzleOffsetY
                );
                chamber.transform.localRotation = Quaternion.identity;

                // Plume transform (used for Waterfall): under model directly, stays fixed
                GameObject plume = new GameObject(PLUME_TX_NAME);
                plume.transform.SetParent(anchor, false);
                plume.transform.localPosition = new Vector3(
                    cosA * ringRadius,
                    nozzleOffsetY,
                    sinA * ringRadius
                );
                plume.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
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

        // ===== Gimbal-driven thrust reduction =====

        private void ApplyGimbalThrustReduction()
        {
            Transform model = part.transform.Find("model");
            if (model == null) return;
            Transform anchor = model.Find(GIMBAL_ANCHOR_NAME);
            if (anchor == null) return;

            Quaternion defaultRot = Quaternion.Euler(90f, 0f, 0f);
            Quaternion currentRot = anchor.localRotation;
            Quaternion diff = currentRot * Quaternion.Inverse(defaultRot);
            float angleDeg = Mathf.Acos(Mathf.Clamp(diff.w, -1f, 1f)) * 2f * Mathf.Rad2Deg;

            const float MAX_GIMBAL_DEG = 5f;
            float deflectionFraction = Mathf.Clamp01(angleDeg / MAX_GIMBAL_DEG);
            float reduction = 1f - (deflectionFraction * gimbalThrustPenalty);

            // Recompute base maxFuelFlow from current state, then apply reduction
            if (atmosphereCurve != null)
            {
                float vacIsp = atmosphereCurve.Evaluate(0f);
                if (vacIsp > 0.1f)
                    maxFuelFlow = (maxThrust / (vacIsp * 9.80665f)) * reduction;
            }
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