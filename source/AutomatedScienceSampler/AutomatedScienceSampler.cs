﻿using KerboKatz.Assets;
using KerboKatz.Extensions;
using KerboKatz.Toolbar;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace KerboKatz.ASS
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class AutomatedScienceSampler : KerboKatzBase<Settings>, IToolbar
    {
        public PerCraftSetting craftSettings;
        private static readonly List<GameScenes> _activeScences = new List<GameScenes>() { GameScenes.FLIGHT };
        private string settingsUIName;
        private Dictionary<string, int> shipCotainsExperiments = new Dictionary<string, int>();
        private Dictionary<Type, IScienceActivator> activators = new Dictionary<Type, IScienceActivator>();
        private double nextUpdate;
        private float CurrentFrame;
        private float lastFrameCheck;
        private Sprite _icon = AssetLoader.GetAsset<Sprite>("icon56", "Icons/AutomatedScienceSampler", "AutomatedScienceSampler/AutomatedScienceSampler");
        private List<ModuleScienceExperiment> experiments;
        private List<IScienceDataContainer> scienceContainers;
        private Dropdown transferScienceUIElement;
        private bool uiElementsReady;
        private KerbalEVA kerbalEVAPart;
        private Vessel parentVessel;
        private Transform uiContent;
        private bool _initialized;

        private float frameCheck { get { return 1 / settings.spriteFPS; } }

        #region init/destroy

        public AutomatedScienceSampler()
        {
        }

        protected override void Awake()
        {
            if (!_initialized)
            {
                modName = "AutomatedScienceSampler";
                displayName = "Automated Science Sampler";
                settingsUIName = "AutomatedScienceSampler";
                tooltip = "Use left click to turn AutomatedScienceSampler on/off.\n Use shift+left click to open the settings menu.";
                requiresUtilities = new Version(1, 5, 2);
                ToolbarBase.instance.Add(this);
                LoadSettings("AutomatedScienceSampler", "Settings");
                Log("Init done!");
                _initialized = true;
            }
            base.Awake();
        }

        public override void OnAwake()
        {
            if (!(HighLogic.CurrentGame.Mode == Game.Modes.CAREER || HighLogic.CurrentGame.Mode == Game.Modes.SCIENCE_SANDBOX))
            {
                Log("Game mode not supported!");
                Destroy(this);
                return;
            }
            GetScienceActivators();
            LoadUI(settingsUIName, "AutomatedScienceSampler/AutomatedScienceSampler");

            GameEvents.onVesselChange.Add(OnVesselChange);
            GameEvents.onVesselWasModified.Add(OnVesselChange);
            GameEvents.onCrewOnEva.Add(GoingEva);
            Log("Awake");
        }

        protected override void AfterDestroy()
        {
            GameEvents.onVesselChange.Remove(OnVesselChange);
            GameEvents.onVesselWasModified.Remove(OnVesselChange);
            GameEvents.onCrewOnEva.Remove(GoingEva);
            ToolbarBase.instance.Remove(this);
            Log("AfterDestroy");
        }

        private void GetScienceActivators()
        {
            Log("Starting search");
            Utilities.LoopTroughAssemblies(CheckTypeForScienceActivator);
        }

        private void CheckTypeForScienceActivator(Type type)
        {
            if (typeof(IScienceActivator).IsAssignableFrom(type) || typeof(IScienceActivatorFactory).IsAssignableFrom(type))
            {
                if (type.GetConstructor(Type.EmptyTypes) == null && !type.IsGenericType)
                {
                    Log("Skip ", type, ": No parameterless constructor");
                    return;
                }
                if (type.IsGenericType)
                {
                    Log("Skip ", type, ": Generic type definition");
                    return;
                }
                if (type.IsAbstract)
                {
                    Log("Skip ", type, ": Abstract");
                    return;
                }

                var instance = Activator.CreateInstance(type);
                var activator = instance as IScienceActivator ?? (instance as IScienceActivatorFactory).GetActivatorInstance();
                activator.AutomatedScienceSampler = this;
                Log("Found", activator.GetType());
                foreach (var validType in activator.GetValidTypes())
                {
                    Log("...for type: ", validType);
                    if (activators.ContainsKey(validType))
                    {
                        Log("......Skip, already valid via ", activators[validType]);
                        continue;
                    }
                    activators.Add(validType, activator);
                }
            }
        }

        private void GetCraftSettings()
        {
            string guid;
            if (settings.perCraftSetting)
            {
                if (FlightGlobals.ActiveVessel.isEVA)
                {
                    if (parentVessel != null)
                        guid = parentVessel.id.ToString();
                    else
                        guid = "EVA";
                }
                else
                {
                    guid = FlightGlobals.ActiveVessel.id.ToString();
                }
            }
            else
            {
                guid = "Single";
            }

            craftSettings = settings.GetSettingsForCraft(guid);
            UpdateUIVisuals();
        }

        #endregion init/destroy

        #region ui

        protected override void OnUIElemntInit(UIData uiWindow)
        {
            var prefabWindow = uiWindow.gameObject.transform as RectTransform;
            uiContent = prefabWindow.Find("Content");
            UpdateUIVisuals();
            InitToggle(uiContent, "DropOutOfWarp", settings.dropOutOfWarp, OnDropOutOfWarpChange);
            InitToggle(uiContent, "UsePerCraftSettings", settings.perCraftSetting, OnPerCraftSettingChange);
            InitToggle(uiContent, "Debug", settings.debug, OnDebugChange);
            InitToggle(uiContent, "UseKKToolbar", settings.useKKToolbar, OnToolbarChange);
            InitSlider(uiContent, "SpriteFPS", settings.spriteFPS, OnSpriteFPSChange);
            transferScienceUIElement = InitDropdown(uiContent, "TransferScience", OnTransferScienceChange);
        }

        private void UpdateUIVisuals()
        {
            if (craftSettings != null)
            {
                InitInputField(uiContent, "Threshold", craftSettings.threshold.ToString(), OnThresholdChange, true);
                InitToggle(uiContent, "SingleRunExperiments", craftSettings.oneTimeOnly, OnSingleRunExperimentsChange, true);
                InitToggle(uiContent, "ResetExperiments", craftSettings.resetExperiments, OnResetExperimentsChange, true);
                InitToggle(uiContent, "HideScienceDialog", craftSettings.hideScienceDialog, OnHideScienceDialogChange, true);
                InitToggle(uiContent, "TransferAllData", craftSettings.transferAllData, OnTransferAllDataChange, true);
                InitToggle(uiContent, "DumpDuplicates", craftSettings.dumpDuplicates, OnDumpDuplicatesChange, true);
            }
        }

        private void OnDropOutOfWarpChange(bool arg0)
        {
            settings.dropOutOfWarp = arg0;
            SaveSettings();
        }

        private void OnDumpDuplicatesChange(bool arg0)
        {
            craftSettings.dumpDuplicates = arg0;
            SaveSettings();
            Log("OnDumpDuplicatesChange");
        }

        private void OnTransferAllDataChange(bool arg0)
        {
            craftSettings.transferAllData = arg0;
            SaveSettings();
            Log("OnTransferAllDataChange");
        }

        private void OnSpriteFPSChange(float arg0)
        {
            settings.spriteFPS = arg0;
            SaveSettings();
            Log("OnSpriteFPSChange");
        }

        private void OnTransferScienceChange(int arg0)
        {
            craftSettings.currentContainer = arg0;
            Log("OnTransferScienceChange ");
            if (craftSettings.currentContainer != 0 && scienceContainers.Count >= craftSettings.currentContainer)
            {
                StartCoroutine(DisableHighlight(0.25f, ((PartModule)scienceContainers[craftSettings.currentContainer - 1]).part));
            }
            SaveSettings();
        }

        private IEnumerator DisableHighlight(float time, Part part)
        {
            part.SetHighlight(true, false);
            yield return new WaitForSeconds(time);
            part.SetHighlight(false, false);
        }

        private void OnPerCraftSettingChange(bool arg0)
        {
            settings.perCraftSetting = arg0;
            SaveSettings();
            GetCraftSettings();
            Log("OnPerCraftSettingChange");
        }

        private void OnDebugChange(bool arg0)
        {
            settings.debug = arg0;
            SaveSettings();
            Log("OnDebugChange");
        }

        private void OnToolbarChange(bool arg0)
        {
            if (settings.useKKToolbar != arg0)
            {
                settings.useKKToolbar = arg0;
                SaveSettings();
                ToolbarBase.SetDirty();
            }
            Log("OnToolbarChange");
        }

        private void OnHideScienceDialogChange(bool arg0)
        {
            craftSettings.hideScienceDialog = arg0;
            SaveSettings();
            Log("OnHideScienceDialog");
        }

        private void OnResetExperimentsChange(bool arg0)
        {
            craftSettings.resetExperiments = arg0;
            SaveSettings();
            Log("OnResetExperimentsChange");
        }

        private void OnSingleRunExperimentsChange(bool arg0)
        {
            craftSettings.oneTimeOnly = arg0;
            SaveSettings();
            Log("onSingleRunExperimentsChange");
        }

        private void OnThresholdChange(string arg0)
        {
            craftSettings.threshold = arg0.ToFloat();
            SaveSettings();
            Log("onThresholdChange");
        }

        private void OnToolbar()
        {
            Log((craftSettings == null), " ", craftSettings.guid, " ", FlightGlobals.ActiveVessel.id);
            if (craftSettings == null)
                GetCraftSettings();
            if (Input.GetMouseButtonUp(1))
            {
                var uiData = GetUIData(settingsUIName);
                if (uiData == null || uiData.canvasGroup == null)
                    return;
                settings.showSettings = !settings.showSettings;
                if (settings.showSettings)
                {
                    FadeCanvasGroup(uiData.canvasGroup, 1, settings.uiFadeSpeed);
                }
                else
                {
                    FadeCanvasGroup(uiData.canvasGroup, 0, settings.uiFadeSpeed);
                }
            }
            else
            {
                craftSettings.runAutoScience = !craftSettings.runAutoScience;
                if (!craftSettings.runAutoScience)
                {
                    icon = AssetLoader.GetAsset<Sprite>("icon56", "Icons/AutomatedScienceSampler", "AutomatedScienceSampler/AutomatedScienceSampler");//Utilities.GetTexture("icon56", "AutomatedScienceSampler/Textures");
                }
            }
            SaveSettings();
        }

        #endregion ui

        private static bool IsTimeWarping()
        {
            return TimeWarp.CurrentRateIndex > 0;
        }

        private static bool CanRunExperiment(ModuleScienceExperiment experiment, IScienceActivator activator, float value)
        {
            return activator.CanRunExperiment(experiment, value);
        }

        private void OnVesselChange(Vessel data)
        {
            Log("OnVesselChange");
            if (FlightGlobals.ActiveVessel == null)
            {
                Log("ActiveVessel is null! this shouldn't happen!");
                return;
            }
            GetCraftSettings();
            UpdateShipInformation();
        }

        private void GoingEva(GameEvents.FromToAction<Part, Part> parts)
        {
            Log("GoingEva");
            parentVessel = parts.from.vessel;
            nextUpdate = Planetarium.GetUniversalTime() + settings.refreshTime;
        }

        private void Update()
        {
            #region icon

            if (lastFrameCheck + frameCheck < Time.time && craftSettings.runAutoScience)
            {
                var frame = Time.deltaTime / frameCheck;
                if (CurrentFrame + frame < 55)
                    CurrentFrame += frame;
                else
                    CurrentFrame = 0;
                icon = AssetLoader.GetAsset<Sprite>("icon" + (int)CurrentFrame, "Icons/AutomatedScienceSampler", "AutomatedScienceSampler/AutomatedScienceSampler");//Utilities.GetTexture("icon" + (int)CurrentFrame, "ForScienceContinued/Textures");
                lastFrameCheck = Time.time;
            }

            #endregion icon

            var isTimeWarping = IsTimeWarping();
            if (!IsReady())
            {
                return;
            }
            if (isTimeWarping)
            {
                if (!settings.interruptTimeWarp)
                {
                    Log("waiting for next frame");
                    return;
                }
            }
            var sw = new System.Diagnostics.Stopwatch();
            sw.Start();
            if (nextUpdate == 0)
            {//add some delay so it doesnt run as soon as the vehicle launches
                if (!FlightGlobals.ready)
                    return;
                nextUpdate = Planetarium.GetUniversalTime() + settings.refreshTime;
                UpdateShipInformation();
                return;
            }

            if (!FlightGlobals.ready || !Utilities.canVesselBeControlled(FlightGlobals.ActiveVessel))
                return;
            if (Planetarium.GetUniversalTime() < nextUpdate)
                return;
            nextUpdate = Planetarium.GetUniversalTime() + settings.refreshTime;

            Log(sw.Elapsed.TotalMilliseconds);
            foreach (var experiment in experiments)
            {
                IScienceActivator activator;
                if (!activators.TryGetValue(experiment.GetType(), out activator))
                {
                    Log("Activator for ", experiment.GetType(), " not found! Using default!");
                    try
                    {
                        activator = DefaultActivator.GetDefaultScienceActivator(experiment.GetType(), this);
                        if (activator == null)
                            throw new InvalidOperationException("Created activator is null");
                        activators.Add(experiment.GetType(), activator);
                        Log("Added default activator of type", activator.GetType().FullName);
                    }
                    catch (Exception e)
                    {
                        Log("Unable to create and add activator for type", experiment.GetType(), ": ", e);
                    }
                }
                var subject = activator.GetScienceSubject(experiment);
                if (subject == null)
                {
                    Log("Subject is null! Skipping.");
                    continue;
                }
                var value = activator.GetScienceValue(experiment, shipCotainsExperiments, subject);
                if (!isTimeWarping)
                {
                    CheckExperimentOptions(experiment, activator, subject, value);
                }
                else
                {
                    if (settings.dropOutOfWarp)
                    {
                        if (CheckExitTimeWarp(experiment, activator, subject, value))
                        {
                            TimeWarp.SetRate(0, true);
                            break;
                        }
                    }
                }

                Log("Experiment checked in: ", sw.Elapsed.TotalMilliseconds);
            }
            Log("Total: ", sw.Elapsed.TotalMilliseconds);
        }

        private bool CheckExitTimeWarp(ModuleScienceExperiment experiment, IScienceActivator activator, ScienceSubject subject, float value)
        {
            if (CanRunExperiment(experiment, activator, value))
            {
                Log("exiting timewarp");
                return true;
            }
            return false;
        }

        private void CheckExperimentOptions(ModuleScienceExperiment experiment, IScienceActivator activator, ScienceSubject subject, float value)
        {
            if (CanRunExperiment(experiment, activator, value))
            {
                Log("Deploying ", experiment.part.name, " for :", value, " science! ", subject.id);
                activator.DeployExperiment(experiment);
                AddToContainer(subject.id);
            }
            else if (CanTransferExperiment(experiment, activator))
            {
                activator.Transfer(experiment, scienceContainers[craftSettings.currentContainer - 1]);
            }
            else if (CanResetExperiment(experiment, activator))
            {
                activator.Reset(experiment);
            }
        }

        private bool CanTransferExperiment(ModuleScienceExperiment experiment, IScienceActivator activator)
        {
            return BasicTransferCheck() && activator.CanTransfer(experiment, scienceContainers[craftSettings.currentContainer - 1]);
        }

        private bool CanResetExperiment(ModuleScienceExperiment experiment, IScienceActivator activator)
        {
            return craftSettings.resetExperiments && activator.CanReset(experiment);
        }

        private bool IsReady()
        {
            if (!uiElementsReady)
            {
                Log("UIElements aren't ready");
                return false;
            }
            if (!FlightGlobals.ready)
            {
                Log("FlightGlobals aren't ready");
                return false;
            }
            if (craftSettings == null)
                GetCraftSettings();

            if (!craftSettings.runAutoScience)
            {
                Log("AutoScience is off");
                return false;
            }
            if (FlightGlobals.ActiveVessel.packed)
            {
                Log("Vessel is packed");
                if (!settings.interruptTimeWarp)
                    return false;
                Log("But we want to check if we have experiments to run!");
            }
            if (!FlightGlobals.ActiveVessel.IsControllable)
            {
                Log("Vessel isn't controllable");
                return false;
            }
            if (!CheckEVA())
            {
                Log("EVA isn't ready");
                return false;
            }
            return true;
        }

        private bool BasicTransferCheck()
        {
            if (craftSettings.currentContainer == 0)
                return false;
            if (craftSettings.currentContainer > scienceContainers.Count)
                return false;
            if (((PartModule)scienceContainers[craftSettings.currentContainer - 1]).vessel != FlightGlobals.ActiveVessel)
                return false;
            return true;
        }

        private bool CheckEVA()
        {
            if (FlightGlobals.ActiveVessel.isEVA)
            {
                if (kerbalEVAPart == null)
                {
                    var kerbalEVAParts = FlightGlobals.ActiveVessel.FindPartModulesImplementing<KerbalEVA>();
                    kerbalEVAPart = kerbalEVAParts.First();
                }
                if (craftSettings.doEVAOnlyIfGroundedWhenLanded && (parentVessel.Landed || parentVessel.Splashed) && (kerbalEVAPart.OnALadder || (!FlightGlobals.ActiveVessel.Landed && !FlightGlobals.ActiveVessel.Splashed)))
                {
                    return false;
                }
            }

            return true;
        }

        private void UpdateShipInformation()
        {
            uiElementsReady = false;

            while (transferScienceUIElement.options.Count > 1)
            {
                transferScienceUIElement.options.RemoveAt(transferScienceUIElement.options.Count - 1);
            }
            experiments = FlightGlobals.ActiveVessel.FindPartModulesImplementing<ModuleScienceExperiment>();
            //scienceContainers = FlightGlobals.ActiveVessel.FindPartModulesImplementing<ModuleScienceContainer>();
            scienceContainers = FlightGlobals.ActiveVessel.FindPartModulesImplementing<IScienceDataContainer>();
            for (var i = scienceContainers.Count - 1; i >= 0; i--)
            {
                if (scienceContainers[i].GetType().IsTypeOf(typeof(ModuleScienceExperiment)))//dont want stock ModuleScienceExperiment as a transfer target as these can only contain the experiments results
                {
                    Log("Removing ", (scienceContainers[i] as PartModule).moduleName, " as transfer target");
                    scienceContainers.RemoveAt(i);
                }
            }
            shipCotainsExperiments.Clear();
            foreach (var currentContainer in scienceContainers)
            {
                AddOptionToDropdown(transferScienceUIElement, (currentContainer as PartModule).part.partInfo.title);
                foreach (var data in currentContainer.GetData())
                {
                    AddToContainer(data.subjectID);
                }
            }
            transferScienceUIElement.value = craftSettings.currentContainer;
            uiElementsReady = true;
        }

        private void AddToContainer(string subjectID, int add = 0)
        {
            if (shipCotainsExperiments.ContainsKey(subjectID))
            {
                Log(subjectID, "_Containing");
                shipCotainsExperiments[subjectID] = shipCotainsExperiments[subjectID] + 1 + add;
            }
            else
            {
                Log(subjectID, "_New");
                shipCotainsExperiments.Add(subjectID, add + 1);
            }
        }

        #region IToolbar

        public List<GameScenes> activeScences
        {
            get
            {
                return _activeScences;
            }
        }

        public UnityAction onClick
        {
            get
            {
                return OnToolbar;
            }
        }

        public Sprite icon
        {
            get
            {
                return _icon;
            }
            private set
            {
                if (_icon != value)
                {
                    _icon = value;
                    ToolbarBase.UpdateIcon(this, icon);
                }
            }
        }

        public bool useKKToolbar
        {
            get
            {
                return settings.useKKToolbar;
            }
        }

        #endregion IToolbar
    }
}