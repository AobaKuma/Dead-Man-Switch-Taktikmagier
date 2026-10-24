using RimWorld;
using UnityEngine;
using Verse;

namespace Militarmagier
{
    /// <summary>
    /// The broom's psychic lift, and its rule that only a psycaster can wield it.
    ///
    /// <see cref="Patch_EquipmentUtility_CanEquip"/> refuses anyone who cannot lift it: no psylink, or no
    /// psychic sensitivity to drive the sigils with. Nothing else qualifies - not an exoskeleton, not an
    /// automatroid frame - which is why this is not FFF's HeavyEquippableExtension, whose rules would let
    /// those through.
    ///
    /// The lift divides the weapon's mass (<see cref="StatPart_PsychicLiftMass"/>) and its wielder's
    /// move-speed penalty (<see cref="StatPart_PsychicLiftMoveSpeed"/>) by 1 + the holder's psychic
    /// sensitivity, when the holder can lift it: half at 100%, a third at 200%. On the ground, or with
    /// anyone else carrying it, it weighs its full mass.
    /// </summary>
    public class PsychicLiftExtension : DefModExtension
    {
        /// <summary>MoveSpeed offset while wielded, before the lift. Used instead of equippedStatOffsets so it can scale.</summary>
        public float moveSpeedOffset;

        public static bool CanLift(Pawn pawn)
        {
            return pawn.HasPsylink && pawn.GetStatValue(StatDefOf.PsychicSensitivity) > float.Epsilon;
        }

        /// <summary>The factor on the weapon's weight with this pawn holding it.</summary>
        public static float LiftFactor(Pawn pawn)
        {
            if (!CanLift(pawn))
            {
                return 1f;
            }
            return 1f / (1f + Mathf.Max(0f, pawn.GetStatValue(StatDefOf.PsychicSensitivity)));
        }

        /// <summary>The pawn wielding the thing, carrying it in hand or keeping it in inventory, if any.</summary>
        public static Pawn Holder(Thing thing)
        {
            switch (thing.ParentHolder)
            {
                case Pawn_EquipmentTracker equipment:
                    return equipment.pawn;
                case Pawn_InventoryTracker inventory:
                    return inventory.pawn;
                case Pawn_CarryTracker carry:
                    return carry.pawn;
                default:
                    return null;
            }
        }
    }

    /// <summary>
    /// Scales a <see cref="PsychicLiftExtension"/> weapon's mass by its holder's lift. Added to the Mass
    /// stat by 1.7/Patches/Stat_PsychicLift.xml. Pawn mass picks it up through vanilla's gear and
    /// inventory mass, so encumbrance and caravan capacity see the lighter weapon.
    /// </summary>
    public class StatPart_PsychicLiftMass : StatPart
    {
        public override void TransformValue(StatRequest req, ref float val)
        {
            if (TryGetLift(req, out float factor))
            {
                val *= factor;
            }
        }

        public override string ExplanationPart(StatRequest req)
        {
            if (TryGetLift(req, out float factor))
            {
                return "DMST_PsychicLiftStat".Translate() + ": x" + factor.ToStringPercent();
            }
            return null;
        }

        private static bool TryGetLift(StatRequest req, out float factor)
        {
            factor = 1f;
            if (!req.HasThing || !req.Thing.def.HasModExtension<PsychicLiftExtension>())
            {
                return false;
            }
            Pawn holder = PsychicLiftExtension.Holder(req.Thing);
            if (holder == null || !PsychicLiftExtension.CanLift(holder))
            {
                return false;
            }
            factor = PsychicLiftExtension.LiftFactor(holder);
            return true;
        }
    }

    /// <summary>
    /// The move-speed penalty of a wielded <see cref="PsychicLiftExtension"/> weapon, scaled by the
    /// wielder's lift. Added to the MoveSpeed stat by 1.7/Patches/Stat_PsychicLift.xml.
    /// </summary>
    public class StatPart_PsychicLiftMoveSpeed : StatPart
    {
        public override void TransformValue(StatRequest req, ref float val)
        {
            if (TryGetOffset(req, out float offset, out _))
            {
                val += offset;
            }
        }

        public override string ExplanationPart(StatRequest req)
        {
            if (TryGetOffset(req, out float offset, out Thing weapon))
            {
                return weapon.LabelCap + " (" + "DMST_PsychicLiftStat".Translate() + "): "
                    + offset.ToStringByStyle(parentStat.toStringStyle, ToStringNumberSense.Offset);
            }
            return null;
        }

        private static bool TryGetOffset(StatRequest req, out float offset, out Thing weapon)
        {
            offset = 0f;
            weapon = null;
            if (req.Thing is not Pawn pawn)
            {
                return false;
            }
            weapon = pawn.equipment?.Primary;
            PsychicLiftExtension lift = weapon?.def.GetModExtension<PsychicLiftExtension>();
            if (lift == null || lift.moveSpeedOffset == 0f)
            {
                return false;
            }
            offset = lift.moveSpeedOffset * PsychicLiftExtension.LiftFactor(pawn);
            return true;
        }
    }
}
