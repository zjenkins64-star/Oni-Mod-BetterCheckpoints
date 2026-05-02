using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using PeterHan.PLib.UI;
using UnityEngine;
using UnityEngine.UI;

namespace BetterCheckpoints.Patches
{
    // Injects three columns into the vanilla AccessControlSideScreen for
    // SuitMarker targets, in place of the per-dupe direction widgets:
    //
    //   With Suit  |  Without Suit  |  Restrict use
    //
    // The first two are mutex (always exactly one checked) and drive
    // CheckpointAccessControl per-dupe overrides, governing the equip /
    // unequip reactables at the checkpoint. "Restrict use" is independent;
    // when checked, it sets vanilla AccessControl.Permission.Neither for
    // that dupe (blocks passage entirely via the existing pathfinder
    // integration).
    //
    // To make room for three 28x28 checkboxes in the row's tightly-packed
    // 256px width, we hide the per-dupe DirectionToggles, DittoMark, and
    // empty Spacer widgets for SuitMarker rows. Per-dupe direction
    // permissions therefore aren't editable from this screen — atmo-suit
    // checkpoints typically just need "block / allow", and direction
    // gating belongs on doors. Group-level direction defaults still work
    // through the section header arrows.
    //
    // Strategy: postfix the private Refresh() method. By the time it
    // returns, vanilla has finished (re)populating minionIdentityRows,
    // so we walk that dict, hide the vanilla widgets, and rebuild our
    // three-checkbox container. We rebuild on every Refresh — closures
    // bind to the row's current dupe + the current CheckpointAccessControl
    // / AccessControl, so reuse across target changes is unsafe.
    [HarmonyPatch(typeof(AccessControlSideScreen), "Refresh")]
    internal static class AccessControlSideScreen_Refresh_InjectSuitColumns
    {
        private const string LABEL_ROW_NAME = "BC_SuitColumnLabelRow";
        private const string CONTAINER_NAME = "BC_SuitColumns";
        private const string WITH_NAME = "BC_WithSuitCheck";
        private const string WITHOUT_NAME = "BC_WithoutSuitCheck";
        private const string RESTRICT_NAME = "BC_RestrictCheck";
        private const string DEAD_SUFFIX = "_dead";

        // Vanilla row child names we hide for SuitMarker rows.
        private const string VANILLA_SPACER_NAME = "Spacer";
        private const string VANILLA_DITTO_NAME = "DittoMark";
        private const string VANILLA_DIRECTION_NAME = "DirectionToggles";

        private const float CHECKBOX_SIZE = 28f;
        private const float COLUMN_SPACING = 6f;
        private const float CONTAINER_WIDTH = CHECKBOX_SIZE * 3 + COLUMN_SPACING * 2;

        private static readonly FieldInfo MinionRowsField = AccessTools.Field(
            typeof(AccessControlSideScreen), "minionIdentityRows");
        private static readonly FieldInfo TargetField = AccessTools.Field(
            typeof(AccessControlSideScreen), "target");
        private static readonly FieldInfo StandardContentField = AccessTools.Field(
            typeof(AccessControlSideScreen), "standardMinionSectionContent");
        private static readonly FieldInfo BionicHeaderField = AccessTools.Field(
            typeof(AccessControlSideScreen), "bionicMinionSectionHeader");
        private static readonly FieldInfo RobotHeaderField = AccessTools.Field(
            typeof(AccessControlSideScreen), "robotSectionHeader");

        private static void Postfix(AccessControlSideScreen __instance)
        {
            try { RefreshInjected(__instance); }
            catch (Exception ex)
            {
                Debug.LogError("[BetterCheckpoints] Failed to inject suit columns: " + ex);
            }
        }

        private static void RefreshInjected(AccessControlSideScreen sideScreen)
        {
            var ac = (AccessControl)TargetField.GetValue(sideScreen);
            var cac = ac != null ? ac.gameObject.GetComponent<CheckpointAccessControl>() : null;
            bool inject = cac != null;

            var standardContent = (GameObject)StandardContentField.GetValue(sideScreen);
            EnsureLabelRow(standardContent, inject);
            HideNonStandardSections(sideScreen, inject);

            var rows = (Dictionary<MinionAssignablesProxy, GameObject>)MinionRowsField.GetValue(sideScreen);
            if (rows == null) return;
            foreach (var kv in rows)
            {
                var minion = kv.Key;
                var rowGo = kv.Value;
                if (rowGo == null || minion == null) continue;
                var minionGo = minion.GetTargetGameObject();
                bool isStandard = minionGo != null && minionGo.HasTag(GameTags.Minions.Models.Standard);
                bool show = inject && isStandard;
                ToggleVanillaWidgets(rowGo, hide: show);
                EnsureRowCheckboxes(rowGo, show, ac, cac, minion);
            }
        }

        // Hides the Bionic and Robots section headers when the target is
        // a SuitMarker — those dupes can't wear atmo suits / oxygen masks
        // and we treat them as "no suit needed" by default, so showing
        // their sections is just clutter. Vanilla RefreshContainerObjects
        // re-activates the headers on the next SetTarget for non-
        // SuitMarker targets, so we don't need to restore manually.
        private static void HideNonStandardSections(AccessControlSideScreen ss, bool hide)
        {
            if (!hide) return;

            var bionic = (GameObject)BionicHeaderField.GetValue(ss);
            if (bionic != null && bionic.activeSelf) bionic.SetActive(false);

            var robot = (GameObject)RobotHeaderField.GetValue(ss);
            if (robot != null && robot.activeSelf) robot.SetActive(false);
        }

        // Hides vanilla per-dupe direction widgets when the target is a
        // SuitMarker so our checkboxes have the slot to themselves. For
        // non-SuitMarker targets we leave the vanilla widgets alone —
        // vanilla ConfigureRow already set exactly one of DittoMark /
        // DirectionToggles active based on whether the dupe is using
        // default permission, and we must not flip that.
        private static void ToggleVanillaWidgets(GameObject rowGo, bool hide)
        {
            // Spacer is just empty filler; safe to toggle either way.
            var spacer = rowGo.transform.Find(VANILLA_SPACER_NAME);
            if (spacer != null && spacer.gameObject.activeSelf == hide)
            {
                spacer.gameObject.SetActive(!hide);
            }

            if (!hide) return;

            // SuitMarker target: force both off so our checkbox container
            // owns the right side of the row.
            var ditto = rowGo.transform.Find(VANILLA_DITTO_NAME);
            if (ditto != null && ditto.gameObject.activeSelf)
            {
                ditto.gameObject.SetActive(false);
            }
            var dir = rowGo.transform.Find(VANILLA_DIRECTION_NAME);
            if (dir != null && dir.gameObject.activeSelf)
            {
                dir.gameObject.SetActive(false);
            }
        }

        // Adds a label row inside the Standard section's Content as the
        // first child, so its layout group is the SAME parent as the
        // dupe rows below. Mirroring the dupe row's HLG (padding right=7,
        // forceExpandW=False, childControlW=True, spacing=0,
        // alignment=MiddleLeft) means the label group's right edge lines
        // up exactly with the checkbox container's right edge below it,
        // and individual labels share x-positions with their checkboxes.
        private static void EnsureLabelRow(GameObject sectionContent, bool show)
        {
            if (sectionContent == null) return;
            DestroyMarked(sectionContent, LABEL_ROW_NAME);
            if (!show) return;

            var rowGo = new GameObject(LABEL_ROW_NAME, typeof(RectTransform));
            rowGo.transform.SetParent(sectionContent.transform, worldPositionStays: false);
            rowGo.transform.SetAsFirstSibling();

            var hlg = rowGo.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(0, 7, 4, 4);
            hlg.spacing = 0;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true;
            hlg.childForceExpandWidth = false;
            hlg.childControlHeight = true;
            hlg.childForceExpandHeight = false;

            // Flex spacer absorbs all the space the dupe row uses for
            // portrait + name + (hidden vanilla widgets).
            var spacer = new GameObject("Spacer", typeof(RectTransform));
            spacer.transform.SetParent(rowGo.transform, worldPositionStays: false);
            var spacerLE = spacer.AddComponent<LayoutElement>();
            spacerLE.minWidth = 0f;
            spacerLE.preferredWidth = 0f;
            spacerLE.flexibleWidth = 1f;
            spacerLE.minHeight = 1f;
            spacerLE.preferredHeight = 1f;
            spacerLE.flexibleHeight = 0f;

            // Label group: same internal layout as the checkbox container
            // (3 cells of CHECKBOX_SIZE wide with COLUMN_SPACING between).
            // Total preferred width = CONTAINER_WIDTH so the right edge
            // of this group aligns with the right edge of every dupe
            // row's checkbox container.
            var labelGroup = new GameObject("LabelGroup", typeof(RectTransform));
            labelGroup.transform.SetParent(rowGo.transform, worldPositionStays: false);
            var lgHLG = labelGroup.AddComponent<HorizontalLayoutGroup>();
            lgHLG.padding = new RectOffset(0, 0, 0, 0);
            lgHLG.spacing = (int)COLUMN_SPACING;
            lgHLG.childAlignment = TextAnchor.MiddleCenter;
            lgHLG.childControlWidth = true;
            lgHLG.childForceExpandWidth = false;
            lgHLG.childControlHeight = true;
            lgHLG.childForceExpandHeight = false;
            var lgLE = labelGroup.AddComponent<LayoutElement>();
            lgLE.minWidth = CONTAINER_WIDTH;
            lgLE.preferredWidth = CONTAINER_WIDTH;
            lgLE.flexibleWidth = 0f;
            lgLE.minHeight = 32f;
            lgLE.preferredHeight = 32f;
            lgLE.flexibleHeight = 0f;

            AddColumnLabel(labelGroup, "With", ModStrings.SideScreen.COLUMN_WITH_SUIT);
            AddColumnLabel(labelGroup, "Without", ModStrings.SideScreen.COLUMN_WITHOUT_SUIT);
            AddColumnLabel(labelGroup, "Restrict", ModStrings.SideScreen.COLUMN_RESTRICT);

            // Constrain row height so it doesn't stretch.
            var rowLE = rowGo.AddComponent<LayoutElement>();
            rowLE.minHeight = 32f;
            rowLE.preferredHeight = 32f;
            rowLE.flexibleHeight = 0f;
        }

        private static void AddColumnLabel(GameObject parent, string id, string text)
        {
            var label = new PLabel(id)
            {
                Text = text,
                TextStyle = PUITuning.Fonts.TextDarkStyle,
                TextAlignment = TextAnchor.MiddleCenter,
            };
            var go = label.AddTo(parent, -1);
            var le = go.AddOrGet<LayoutElement>();
            le.minWidth = CHECKBOX_SIZE;
            le.preferredWidth = CHECKBOX_SIZE;
            le.flexibleWidth = 0f;
            le.minHeight = 28f;
            le.preferredHeight = 28f;
            le.flexibleHeight = 0f;
        }

        private static void EnsureRowCheckboxes(GameObject rowGo, bool show,
            AccessControl ac, CheckpointAccessControl cac, MinionAssignablesProxy minion)
        {
            DestroyMarked(rowGo, CONTAINER_NAME);
            if (!show) return;

            // Live dupe's KPrefabID InstanceID — the key the reactable
            // patches use. MinionAssignablesProxy has a different ID, so
            // we resolve the proxy back to the live dupe.
            var dupeGo = minion.GetTargetGameObject();
            if (dupeGo == null) return;
            var kpid = dupeGo.GetComponent<KPrefabID>();
            if (kpid == null) return;
            int id = kpid.InstanceID;

            BuildCheckboxes(rowGo, id, ac, cac, minion);
        }

        private static void BuildCheckboxes(GameObject rowGo, int id,
            AccessControl ac, CheckpointAccessControl cac, MinionAssignablesProxy minion)
        {
            GameObject withGo = null;
            GameObject withoutGo = null;
            GameObject restrictGo = null;

            // ---- With Suit / Without Suit (strict mutex) ----
            // Exactly one of these is checked at any time. Clicking an
            // unchecked box checks it AND auto-unchecks the other.
            // Clicking the already-checked box is a no-op (you can't
            // turn off both — that's what Restrict use is for).
            //
            // The choice only controls the equip-on-entry transition.
            // Drop-on-return happens regardless of which mode is
            // selected (handled by the UnequipSuitReactable patch).
            var withCheck = new PCheckBox(WITH_NAME)
            {
                Text = string.Empty,
                InitialState = cac.GetWithSuitAllowed(id) ? PCheckBox.STATE_CHECKED : PCheckBox.STATE_UNCHECKED,
                ToolTip = ModStrings.SideScreen.WITH_SUIT_TOOLTIP,
                OnChecked = (source, state) =>
                {
                    bool newValue = state != PCheckBox.STATE_CHECKED;
                    if (!newValue)
                    {
                        // Strict mutex: at least one must be checked.
                        PCheckBox.SetCheckState(source, PCheckBox.STATE_CHECKED);
                        return;
                    }
                    cac.SetWithSuitOverride(id, true);
                    PCheckBox.SetCheckState(source, PCheckBox.STATE_CHECKED);
                    if (withoutGo != null)
                    {
                        cac.SetWithoutSuitOverride(id, false);
                        PCheckBox.SetCheckState(withoutGo, PCheckBox.STATE_UNCHECKED);
                    }
                },
            };
            withCheck.OnRealize += go => { withGo = go; go.name = WITH_NAME; ConstrainSize(go); };

            var withoutCheck = new PCheckBox(WITHOUT_NAME)
            {
                Text = string.Empty,
                InitialState = cac.GetWithoutSuitAllowed(id) ? PCheckBox.STATE_CHECKED : PCheckBox.STATE_UNCHECKED,
                ToolTip = ModStrings.SideScreen.WITHOUT_SUIT_TOOLTIP,
                OnChecked = (source, state) =>
                {
                    bool newValue = state != PCheckBox.STATE_CHECKED;
                    if (!newValue)
                    {
                        PCheckBox.SetCheckState(source, PCheckBox.STATE_CHECKED);
                        return;
                    }
                    cac.SetWithoutSuitOverride(id, true);
                    PCheckBox.SetCheckState(source, PCheckBox.STATE_CHECKED);
                    if (withGo != null)
                    {
                        cac.SetWithSuitOverride(id, false);
                        PCheckBox.SetCheckState(withGo, PCheckBox.STATE_UNCHECKED);
                    }
                },
            };
            withoutCheck.OnRealize += go => { withoutGo = go; go.name = WITHOUT_NAME; ConstrainSize(go); };

            // ---- Restrict use (independent; drives vanilla AccessControl) ----
            bool isRestricted = ac != null && ac.GetSetPermission(minion) == AccessControl.Permission.Neither;
            var restrictCheck = new PCheckBox(RESTRICT_NAME)
            {
                Text = string.Empty,
                InitialState = isRestricted ? PCheckBox.STATE_CHECKED : PCheckBox.STATE_UNCHECKED,
                ToolTip = ModStrings.SideScreen.RESTRICT_TOOLTIP,
                OnChecked = (source, state) =>
                {
                    bool newValue = state != PCheckBox.STATE_CHECKED;
                    PCheckBox.SetCheckState(source, newValue ? PCheckBox.STATE_CHECKED : PCheckBox.STATE_UNCHECKED);
                    if (ac == null) return;
                    ac.SetPermission(minion,
                        newValue
                            ? AccessControl.Permission.Neither
                            : AccessControl.Permission.Both);
                },
            };
            restrictCheck.OnRealize += go => { restrictGo = go; go.name = RESTRICT_NAME; ConstrainSize(go); };

            var container = new PPanel(CONTAINER_NAME)
            {
                Direction = PanelDirection.Horizontal,
                Spacing = (int)COLUMN_SPACING,
                Alignment = TextAnchor.MiddleCenter,
                // No margin: external width must match the label group's
                // CONTAINER_WIDTH so labels align with checkboxes column
                // for column.
                Margin = new RectOffset(0, 0, 0, 0),
            };
            container.AddChild(withCheck);
            container.AddChild(withoutCheck);
            container.AddChild(restrictCheck);

            var containerGo = container.AddTo(rowGo, -1);
            containerGo.name = CONTAINER_NAME;
            // Force point-anchoring so the row's HLG controls width via
            // our LayoutElement (PPanel.AddTo configures fill anchors,
            // which would make sizeDelta act as parent-relative offsets).
            var crt = containerGo.transform as RectTransform;
            if (crt != null)
            {
                crt.anchorMin = new Vector2(0.5f, 0.5f);
                crt.anchorMax = new Vector2(0.5f, 0.5f);
                crt.pivot = new Vector2(0.5f, 0.5f);
            }
            ConstrainContainerWidth(containerGo);

            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)rowGo.transform);
        }

        private static void ConstrainContainerWidth(GameObject go)
        {
            var le = go.AddOrGet<LayoutElement>();
            le.minWidth = CONTAINER_WIDTH;
            le.preferredWidth = CONTAINER_WIDTH;
            le.flexibleWidth = 0f;
        }

        private static void ConstrainSize(GameObject go)
        {
            var le = go.AddOrGet<LayoutElement>();
            le.minWidth = CHECKBOX_SIZE;
            le.preferredWidth = CHECKBOX_SIZE;
            le.flexibleWidth = 0f;
            le.minHeight = CHECKBOX_SIZE;
            le.preferredHeight = CHECKBOX_SIZE;
            le.flexibleHeight = 0f;
        }

        // Renames before destroying so a same-frame Find() doesn't return
        // the to-be-destroyed object (Object.Destroy is end-of-frame).
        private static void DestroyMarked(GameObject parent, string name)
        {
            var t = parent.transform.Find(name);
            if (t == null) return;
            t.name = name + DEAD_SUFFIX;
            UnityEngine.Object.Destroy(t.gameObject);
        }
    }
}
