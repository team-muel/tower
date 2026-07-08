using NUnit.Framework;
using Tower.Core;

namespace Tower.Tests.EditMode
{
    // T29 — DE interactable anchors: def validation, per-kind guarded state
    // transitions, resolver eligibility / disabled reasons, use recording, and
    // orb cue condition matching. Pure Core logic, deterministic.
    public sealed class InteractableStateTests
    {
        // ---- Def creation / validation -------------------------------------

        [Test]
        public void Create_Inspect_Succeeds()
        {
            var def = InteractableDef.Create("inspect_wall", InteractableKind.Inspect, "조사한다");

            Assert.That(def.IsSuccess, Is.True);
            Assert.That(def.Value.Kind, Is.EqualTo(InteractableKind.Inspect));
        }

        [Test]
        public void Create_EmptyId_Fails()
        {
            Assert.That(InteractableDef.Create("", InteractableKind.Inspect, "x").IsFailure, Is.True);
        }

        [Test]
        public void Create_EmptyPrompt_Fails()
        {
            Assert.That(InteractableDef.Create("id", InteractableKind.Inspect, "").IsFailure, Is.True);
        }

        [Test]
        public void Create_PortalWithoutPortalId_Fails()
        {
            var def = InteractableDef.Create("door_north", InteractableKind.Portal, "문");

            Assert.That(def.IsFailure, Is.True);
            Assert.That(def.Error, Does.Contain("PortalId"));
        }

        [Test]
        public void Create_PortalWithPortalId_Succeeds()
        {
            var def = InteractableDef.Create(
                "door_north", InteractableKind.Portal, "문", portalId: "portal_f03_r02_n");

            Assert.That(def.IsSuccess, Is.True);
            Assert.That(def.Value.PortalId, Is.EqualTo("portal_f03_r02_n"));
        }

        [Test]
        public void Create_NonPortalWithPortalId_Fails()
        {
            var def = InteractableDef.Create(
                "chest", InteractableKind.Chest, "상자", portalId: "nope");

            Assert.That(def.IsFailure, Is.True);
        }

        [Test]
        public void Create_ZeroMaxUses_Fails()
        {
            var def = InteractableDef.Create("id", InteractableKind.Inspect, "x", maxUses: 0);

            Assert.That(def.IsFailure, Is.True);
        }

        // ---- Default states per kind ---------------------------------------

        [Test]
        public void DefaultState_PerKind_IsExpected()
        {
            Assert.That(AnchorRuntime.DefaultState(InteractableKind.Portal), Is.EqualTo(AnchorState.Unlocked));
            Assert.That(AnchorRuntime.DefaultState(InteractableKind.Chest), Is.EqualTo(AnchorState.Unlooted));
            Assert.That(AnchorRuntime.DefaultState(InteractableKind.Shrine), Is.EqualTo(AnchorState.Dormant));
            Assert.That(AnchorRuntime.DefaultState(InteractableKind.Grave), Is.EqualTo(AnchorState.Sealed));
            Assert.That(AnchorRuntime.DefaultState(InteractableKind.Trap), Is.EqualTo(AnchorState.Armed));
            Assert.That(AnchorRuntime.DefaultState(InteractableKind.Resource), Is.EqualTo(AnchorState.Unlooted));
            Assert.That(AnchorRuntime.DefaultState(InteractableKind.Inspect), Is.EqualTo(AnchorState.Idle));
        }

        // ---- Per-kind transitions ------------------------------------------

        [Test]
        public void Door_LockedToUnlocked_Succeeds()
        {
            var rt = AnchorRuntime.Create(InteractableKind.Portal, AnchorState.Locked).Value;

            Assert.That(rt.Transition(AnchorState.Unlocked).IsSuccess, Is.True);
            Assert.That(rt.State, Is.EqualTo(AnchorState.Unlocked));
        }

        [Test]
        public void Door_UnlockedToLooted_IsIllegal()
        {
            var rt = AnchorRuntime.CreateDefault(InteractableKind.Portal);

            var result = rt.Transition(AnchorState.Looted);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error, Does.Contain("Illegal"));
        }

        [Test]
        public void Orb_DormantToRevealedToOpen_Succeeds()
        {
            var rt = AnchorRuntime.CreateDefault(InteractableKind.Shrine, maxUses: -1);

            Assert.That(rt.Transition(AnchorState.Revealed).IsSuccess, Is.True);
            Assert.That(rt.Transition(AnchorState.Open).IsSuccess, Is.True);
            Assert.That(rt.State, Is.EqualTo(AnchorState.Open));
        }

        [Test]
        public void Orb_DormantToOpen_SkippingRevealed_IsIllegal()
        {
            var rt = AnchorRuntime.CreateDefault(InteractableKind.Shrine);

            Assert.That(rt.Transition(AnchorState.Open).IsFailure, Is.True);
        }

        [Test]
        public void Container_UnlootedToLooted_Succeeds()
        {
            var rt = AnchorRuntime.CreateDefault(InteractableKind.Chest);

            Assert.That(rt.Transition(AnchorState.Looted).IsSuccess, Is.True);
            Assert.That(rt.State, Is.EqualTo(AnchorState.Looted));
        }

        [Test]
        public void Container_LootedIsTerminal()
        {
            var rt = AnchorRuntime.Create(InteractableKind.Chest, AnchorState.Looted, maxUses: -1).Value;

            Assert.That(rt.Transition(AnchorState.Unlooted).IsFailure, Is.True);
            Assert.That(rt.Transition(AnchorState.Unlocked).IsFailure, Is.True);
        }

        [Test]
        public void Gravestone_SealedToOpen_Succeeds()
        {
            var rt = AnchorRuntime.CreateDefault(InteractableKind.Grave);

            Assert.That(rt.Transition(AnchorState.Open).IsSuccess, Is.True);
        }

        [Test]
        public void Trap_ArmedToDisarmed_Succeeds()
        {
            var rt = AnchorRuntime.CreateDefault(InteractableKind.Trap);

            Assert.That(rt.Transition(AnchorState.Disarmed).IsSuccess, Is.True);
            Assert.That(rt.State, Is.EqualTo(AnchorState.Disarmed));
        }

        [Test]
        public void Trap_ArmedToTriggered_Succeeds()
        {
            var rt = AnchorRuntime.CreateDefault(InteractableKind.Trap);

            Assert.That(rt.Transition(AnchorState.Triggered).IsSuccess, Is.True);
        }

        [Test]
        public void Trap_TriggeredIsTerminal_CannotRearm()
        {
            var rt = AnchorRuntime.Create(InteractableKind.Trap, AnchorState.Triggered, maxUses: -1).Value;

            Assert.That(rt.Transition(AnchorState.Armed).IsFailure, Is.True);
            Assert.That(rt.Transition(AnchorState.Disarmed).IsFailure, Is.True);
        }

        // ---- Invalid-transition & spend guards -----------------------------

        [Test]
        public void Transition_ToSameState_Fails()
        {
            var rt = AnchorRuntime.Create(InteractableKind.Portal, AnchorState.Locked, maxUses: -1).Value;

            Assert.That(rt.Transition(AnchorState.Locked).IsFailure, Is.True);
        }

        [Test]
        public void Transition_WhenSpent_Fails()
        {
            var rt = AnchorRuntime.CreateDefault(InteractableKind.Chest, maxUses: 1);
            rt.Transition(AnchorState.Looted);

            Assert.That(rt.IsSpent, Is.True);
            var again = rt.Transition(AnchorState.Unlooted);
            Assert.That(again.IsFailure, Is.True);
        }

        [Test]
        public void Create_IllegalInitialStateForKind_Fails()
        {
            var rt = AnchorRuntime.Create(InteractableKind.Trap, AnchorState.Looted);

            Assert.That(rt.IsFailure, Is.True);
        }

        [Test]
        public void UsesRemaining_DecrementsPerTransition_UnlimitedStaysNegative()
        {
            var limited = AnchorRuntime.CreateDefault(InteractableKind.Trap, maxUses: 2);
            limited.Transition(AnchorState.Disarmed);
            Assert.That(limited.UsesRemaining, Is.EqualTo(1));

            var unlimited = AnchorRuntime.CreateDefault(InteractableKind.Grave, maxUses: -1);
            unlimited.Transition(AnchorState.Open);
            Assert.That(unlimited.UsesRemaining, Is.EqualTo(-1));
        }

        [Test]
        public void Restore_AllowsSpentRuntime()
        {
            var restored = AnchorRuntime.Restore(InteractableKind.Chest, AnchorState.Looted, 0);

            Assert.That(restored.IsSuccess, Is.True);
            Assert.That(restored.Value.IsSpent, Is.True);
            Assert.That(restored.Value.State, Is.EqualTo(AnchorState.Looted));
        }

        // ---- Determinism ----------------------------------------------------

        [Test]
        public void Resolve_IsDeterministic_ForSameInputs()
        {
            var def = InteractableDef.Create(
                "chest_relic", InteractableKind.Chest, "연다",
                rewardPreview: "유물").Value;
            var ctx = new InteractionContext(3, false, false, "ice");

            var a = InteractionResolver.Resolve(def, AnchorRuntime.CreateDefault(def.Kind), ctx);
            var b = InteractionResolver.Resolve(def, AnchorRuntime.CreateDefault(def.Kind), ctx);

            Assert.That(a.Visible, Is.EqualTo(b.Visible));
            Assert.That(a.Enabled, Is.EqualTo(b.Enabled));
            Assert.That(a.DisabledReason, Is.EqualTo(b.DisabledReason));
            Assert.That(a.Preview, Is.EqualTo(b.Preview));
        }

        // ---- Resolver: visibility ------------------------------------------

        [Test]
        public void Resolve_RegressorMemoryOrb_HiddenWithoutRegressor()
        {
            var def = InteractableDef.Create(
                "orb_memory", InteractableKind.Shrine, "기억",
                visibilityRule: VisibilityRule.RegressorMemoryOnly).Value;

            var hidden = InteractionResolver.Resolve(
                def, AnchorRuntime.CreateDefault(def.Kind),
                new InteractionContext(1, false, false, "ice"));
            Assert.That(hidden.Visible, Is.False);
            Assert.That(hidden.CanHover, Is.False);

            var shown = InteractionResolver.Resolve(
                def, AnchorRuntime.CreateDefault(def.Kind),
                new InteractionContext(1, false, false, "ice", new[] { "regressor" }));
            Assert.That(shown.Visible, Is.True);
        }

        [Test]
        public void Resolve_AfterDeathGrave_HiddenUntilDeath()
        {
            var def = InteractableDef.Create(
                "grave_lost", InteractableKind.Grave, "묘비",
                visibilityRule: VisibilityRule.AfterDeathOnly).Value;

            Assert.That(InteractionResolver.Resolve(
                def, AnchorRuntime.CreateDefault(def.Kind),
                new InteractionContext(1, false, false, "camp")).Visible, Is.False);

            Assert.That(InteractionResolver.Resolve(
                def, AnchorRuntime.CreateDefault(def.Kind),
                new InteractionContext(1, false, true, "camp")).Visible, Is.True);
        }

        // ---- Resolver: disabled reason (never silent) ----------------------

        [Test]
        public void Resolve_LockedAnchor_IsVisibleWithReason()
        {
            var def = InteractableDef.Create(
                "chest_locked", InteractableKind.Chest, "연다",
                disabledReason: "열쇠가 필요하다.",
                useRule: UseRule.RequiresTags,
                requiredTags: new[] { "key" }).Value;

            var state = InteractionResolver.Resolve(
                def, AnchorRuntime.CreateDefault(def.Kind),
                new InteractionContext(2, false, false, "ice"));

            Assert.That(state.Visible, Is.True);
            Assert.That(state.Enabled, Is.False);
            Assert.That(state.IsLockedButShown, Is.True);
            Assert.That(state.DisabledReason, Is.EqualTo("열쇠가 필요하다."));
        }

        [Test]
        public void Resolve_RequiredTagsPresent_IsEnabled()
        {
            var def = InteractableDef.Create(
                "chest_locked", InteractableKind.Chest, "연다",
                useRule: UseRule.RequiresTags,
                requiredTags: new[] { "key" }).Value;

            var state = InteractionResolver.Resolve(
                def, AnchorRuntime.CreateDefault(def.Kind),
                new InteractionContext(2, false, false, "ice", new[] { "key" }));

            Assert.That(state.Enabled, Is.True);
            Assert.That(state.DisabledReason, Is.Empty);
        }

        [Test]
        public void Resolve_DepartureGate_BlockedWhileRetreating()
        {
            var def = InteractableDef.Create(
                "camp_gate_departure", InteractableKind.Portal, "출발",
                portalId: "portal_depart",
                useRule: UseRule.NotWhileRetreating).Value;

            var state = InteractionResolver.Resolve(
                def, AnchorRuntime.CreateDefault(def.Kind),
                new InteractionContext(0, true, false, "camp"));

            Assert.That(state.Visible, Is.True);
            Assert.That(state.Enabled, Is.False);
            Assert.That(state.DisabledReason, Is.Not.Empty);
        }

        [Test]
        public void Resolve_Preview_CombinesRiskAndReward()
        {
            var def = InteractableDef.Create(
                "hazard_pit", InteractableKind.Trap, "함정",
                riskPreview: "낙하", rewardPreview: "지름길").Value;

            var state = InteractionResolver.Resolve(
                def, AnchorRuntime.CreateDefault(def.Kind),
                new InteractionContext(1, false, false, "ice"));

            Assert.That(state.Preview, Does.Contain("낙하"));
            Assert.That(state.Preview, Does.Contain("지름길"));
        }

        // ---- Registry: use recording & QA state ----------------------------

        [Test]
        public void Registry_DuplicateId_Fails()
        {
            var registry = new InteractableRegistry();
            registry.Add(Def("a", InteractableKind.Inspect));

            Assert.That(registry.Add(Def("a", InteractableKind.Inspect)).IsFailure, Is.True);
        }

        [Test]
        public void Registry_Use_TransitionsAndRecords()
        {
            var registry = new InteractableRegistry();
            var def = InteractableDef.Create(
                "chest_relic", InteractableKind.Chest, "연다",
                stateChanges: new[] { new AnchorStateChange(AnchorState.Looted) }).Value;
            registry.Add(def);

            var ctx = new InteractionContext(1, false, false, "ice");
            var used = registry.Use("chest_relic", ctx);

            Assert.That(used.IsSuccess, Is.True);
            Assert.That(registry.Find("chest_relic").Runtime.State, Is.EqualTo(AnchorState.Looted));
            Assert.That(registry.Find("chest_relic").Runtime.IsSpent, Is.True);
        }

        [Test]
        public void Registry_Use_SecondTime_Fails()
        {
            var registry = new InteractableRegistry();
            var def = InteractableDef.Create(
                "chest_relic", InteractableKind.Chest, "연다",
                stateChanges: new[] { new AnchorStateChange(AnchorState.Looted) }).Value;
            registry.Add(def);
            var ctx = new InteractionContext(1, false, false, "ice");

            registry.Use("chest_relic", ctx);
            var second = registry.Use("chest_relic", ctx);

            Assert.That(second.IsFailure, Is.True);
        }

        [Test]
        public void Registry_Use_LockedAnchor_FailsWithReason()
        {
            var registry = new InteractableRegistry();
            var def = InteractableDef.Create(
                "chest_locked", InteractableKind.Chest, "연다",
                disabledReason: "열쇠가 필요하다.",
                useRule: UseRule.RequiresTags,
                requiredTags: new[] { "key" },
                stateChanges: new[] { new AnchorStateChange(AnchorState.Looted) }).Value;
            registry.Add(def);

            var result = registry.Use("chest_locked", new InteractionContext(1, false, false, "ice"));

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error, Is.EqualTo("열쇠가 필요하다."));
        }

        [Test]
        public void Registry_Use_InspectAnchor_ConsumesUseWithoutStateChange()
        {
            var registry = new InteractableRegistry();
            registry.Add(InteractableDef.Create("inspect_mural", InteractableKind.Inspect, "조사").Value);
            var ctx = new InteractionContext(1, false, false, "ice");

            var used = registry.Use("inspect_mural", ctx);

            Assert.That(used.IsSuccess, Is.True);
            Assert.That(registry.Find("inspect_mural").Runtime.State, Is.EqualTo(AnchorState.Idle));
            Assert.That(registry.Find("inspect_mural").Runtime.IsSpent, Is.True);
        }

        [Test]
        public void RuntimeStore_CaptureAndRestore_PreservesSpentAnchor()
        {
            var def = InteractableDef.Create(
                "chest_relic", InteractableKind.Chest, "연다",
                stateChanges: new[] { new AnchorStateChange(AnchorState.Looted) }).Value;
            var registry = new InteractableRegistry();
            registry.Add(def);
            registry.Use("chest_relic", new InteractionContext(1, false, false, "forest"));

            var store = new InteractionRuntimeStore();
            store.Capture(registry);
            Result<AnchorRuntime> restored = store.RuntimeFor(def);

            Assert.That(restored.IsSuccess, Is.True);
            Assert.That(restored.Value.State, Is.EqualTo(AnchorState.Looted));
            Assert.That(restored.Value.UsesRemaining, Is.EqualTo(0));
        }

        [Test]
        public void RuntimeStore_MissingSnapshot_ReturnsFreshDefault()
        {
            var def = InteractableDef.Create("resource_01", InteractableKind.Resource, "줍는다", maxUses: 2).Value;
            var store = new InteractionRuntimeStore();

            Result<AnchorRuntime> restored = store.RuntimeFor(def);

            Assert.That(restored.IsSuccess, Is.True);
            Assert.That(restored.Value.State, Is.EqualTo(AnchorState.Unlooted));
            Assert.That(restored.Value.UsesRemaining, Is.EqualTo(2));
        }

        [Test]
        public void Registry_HoverableAnchors_ExcludesHiddenOrbs()
        {
            var registry = new InteractableRegistry();
            registry.Add(InteractableDef.Create("chest", InteractableKind.Chest, "연다").Value);
            registry.Add(InteractableDef.Create(
                "orb_memory", InteractableKind.Shrine, "기억",
                visibilityRule: VisibilityRule.RegressorMemoryOnly).Value);

            var noRegressor = registry.HoverableAnchors(new InteractionContext(1, false, false, "ice"));
            Assert.That(noRegressor.Count, Is.EqualTo(1));

            var withRegressor = registry.HoverableAnchors(
                new InteractionContext(1, false, false, "ice", new[] { "regressor" }));
            Assert.That(withRegressor.Count, Is.EqualTo(2));
        }

        // ---- Orb cue matching & filter -------------------------------------

        [Test]
        public void OrbCue_Create_RequiresText()
        {
            Assert.That(OrbCueDef.Create("id", OrbCueKind.Memory, "").IsFailure, Is.True);
        }

        [Test]
        public void OrbCue_MemoryCue_RequiresRegressorMemory()
        {
            var cue = OrbCueDef.Create(
                "cue_mem", OrbCueKind.Memory, "여기서 죽었다",
                requiresRegressorMemory: true).Value;

            Assert.That(cue.Matches(new OrbCueContext(DispositionType.Aggressive, false)), Is.False);
            Assert.That(cue.Matches(new OrbCueContext(DispositionType.Aggressive, true)), Is.True);
        }

        [Test]
        public void OrbCue_CompanionCue_RequiresDisposition()
        {
            var cue = OrbCueDef.Create(
                "cue_comp", OrbCueKind.Companion, "물러서라",
                requiredDisposition: DispositionType.Protective).Value;

            Assert.That(cue.Matches(new OrbCueContext(DispositionType.Aggressive, false)), Is.False);
            Assert.That(cue.Matches(new OrbCueContext(DispositionType.Protective, false)), Is.True);
        }

        [Test]
        public void OrbCue_SkillTagGate_Matches()
        {
            var cue = OrbCueDef.Create(
                "cue_haz", OrbCueKind.Hazard, "바닥이 약하다",
                requiredSkillTag: "perception").Value;

            Assert.That(cue.Matches(new OrbCueContext(DispositionType.Aggressive, false)), Is.False);
            Assert.That(cue.Matches(
                new OrbCueContext(DispositionType.Aggressive, false, new[] { "perception" })), Is.True);
        }

        [Test]
        public void OrbCue_Filter_MapsKindToBucket()
        {
            Assert.That(OrbCueDef.Create("m", OrbCueKind.Memory, "x").Value.Filter, Is.EqualTo(OrbFilter.Cognition));
            Assert.That(OrbCueDef.Create("c", OrbCueKind.Companion, "x").Value.Filter, Is.EqualTo(OrbFilter.Cognition));
            Assert.That(OrbCueDef.Create("h", OrbCueKind.Hazard, "x").Value.Filter, Is.EqualTo(OrbFilter.Hazard));
            Assert.That(OrbCueDef.Create("l", OrbCueKind.Loot, "x").Value.Filter, Is.EqualTo(OrbFilter.Loot));
        }

        [Test]
        public void OrbCue_PassesFilter_AllShowsEverything()
        {
            var hazard = OrbCueDef.Create("h", OrbCueKind.Hazard, "x").Value;

            Assert.That(hazard.PassesFilter(OrbFilter.All), Is.True);
            Assert.That(hazard.PassesFilter(OrbFilter.Hazard), Is.True);
            Assert.That(hazard.PassesFilter(OrbFilter.Loot), Is.False);
        }

        private static InteractableDef Def(string id, InteractableKind kind)
        {
            return InteractableDef.Create(id, kind, id + " 프롬프트").Value;
        }
    }
}
