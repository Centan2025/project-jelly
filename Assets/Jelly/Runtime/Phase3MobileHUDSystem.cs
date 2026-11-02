using System;
using UnityEngine;

namespace Jelly
{
    public enum MassCapabilityTier
    {
        SmallGateClearance = 0, // Etkin Kütle <= 75: Küçük geçit & alçak piston simgesi aydınlanır
        StandardBalanced = 1,   // Etkin Kütle 76 - 114: Standart akış & ideal bölünme simgesi aydınlanır
        CrystalSmashReady = 2   // Etkin Kütle >= 115: Kristal cam duvar kırma simgesi aktifleşir
    }

    public enum ContextualActionKind
    {
        Split = 0,          // Tek parçayken -> "÷2 Bölün"
        Merge = 1,          // İkiz parçayken -> ">< Birleş"
        SplitLockedLowMass  // Kütle < 40 iken -> Bölünme kilitli
    }

    public struct MobileHUDViewModel
    {
        public float TotalMass;
        public float EffectivePieceMass;
        public bool IsSplit;
        public int DistanceMeters;
        public MassCapabilityTier ActiveTier;
        public bool IsSmallGateIconLit;
        public bool IsNormalSplitIconLit;
        public bool IsCrystalSmashIconLit;
        public string CapabilityHintText;
        public ContextualActionKind ActionKind;
        public string ActionButtonLabel;
        public bool ActionButtonInteractable;
        public bool ShowComboBadge;
        public int LiveComboMultiplier;
        public bool ShowTransientBanner;
        public bool ShowPowerupPill;
    }

    /// <summary>
    /// Phase 3 Mobile HUD & Contextual Control Evaluator.
    /// Simplifies in-game HUD to Distance, Mass + Capability Icons, and a Single Contextual Action Button.
    /// </summary>
    public static class Phase3MobileHUDSystem
    {
        public const float SmallGateMaxMass = 75f;
        public const float CrystalSmashMinMass = 115f;
        public const float MinMassRequiredToSplit = 40f;

        public static MassCapabilityTier EvaluateCapabilityTier(float totalMass, bool isSplit)
        {
            float effectiveMass = isSplit ? totalMass * 0.5f : totalMass;
            if (effectiveMass <= SmallGateMaxMass + 0.01f)
                return MassCapabilityTier.SmallGateClearance;
            if (effectiveMass >= CrystalSmashMinMass - 0.01f)
                return MassCapabilityTier.CrystalSmashReady;
            return MassCapabilityTier.StandardBalanced;
        }

        public static MobileHUDViewModel BuildViewModel(
            float totalMass,
            bool isSplit,
            float distanceMeters,
            int comboCount,
            float statusBannerTimer,
            bool hasActivePowerup)
        {
            float effectiveMass = isSplit ? totalMass * 0.5f : totalMass;
            MassCapabilityTier tier = EvaluateCapabilityTier(totalMass, isSplit);

            bool smallLit = tier == MassCapabilityTier.SmallGateClearance;
            bool normalLit = tier == MassCapabilityTier.StandardBalanced;
            bool smashLit = tier == MassCapabilityTier.CrystalSmashReady;

            string capHint;
            if (smallLit)
                capHint = "◯ Küçük Geçit Aktif (≤75)";
            else if (smashLit)
                capHint = "💎 Kristal Kırma Aktif (≥115)";
            else
                capHint = "÷2 Bölünme & Normal Geçit (76-114)";

            ContextualActionKind actKind;
            string actLabel;
            bool actInteractable;

            if (isSplit)
            {
                actKind = ContextualActionKind.Merge;
                actLabel = "><  BİRLEŞ";
                actInteractable = true;
            }
            else if (totalMass >= MinMassRequiredToSplit - 0.01f)
            {
                actKind = ContextualActionKind.Split;
                actLabel = "÷2  BÖLÜN";
                actInteractable = true;
            }
            else
            {
                actKind = ContextualActionKind.SplitLockedLowMass;
                actLabel = "÷2  (KÜTLE AZ)";
                actInteractable = false;
            }

            int mult = Mathf.Clamp(1 + comboCount / 2, 1, 5);

            return new MobileHUDViewModel
            {
                TotalMass = totalMass,
                EffectivePieceMass = effectiveMass,
                IsSplit = isSplit,
                DistanceMeters = Mathf.Max(0, Mathf.FloorToInt(distanceMeters)),
                ActiveTier = tier,
                IsSmallGateIconLit = smallLit,
                IsNormalSplitIconLit = normalLit,
                IsCrystalSmashIconLit = smashLit,
                CapabilityHintText = capHint,
                ActionKind = actKind,
                ActionButtonLabel = actLabel,
                ActionButtonInteractable = actInteractable,
                ShowComboBadge = comboCount >= 2,
                LiveComboMultiplier = mult,
                ShowTransientBanner = statusBannerTimer > 0.05f,
                ShowPowerupPill = hasActivePowerup
            };
        }

        public static bool TryExecuteContextualAction(bool currentIsSplit, float currentTotalMass, out bool nextIsSplit)
        {
            if (currentIsSplit)
            {
                nextIsSplit = false;
                return true;
            }

            if (currentTotalMass >= MinMassRequiredToSplit - 0.01f)
            {
                nextIsSplit = true;
                return true;
            }

            nextIsSplit = false;
            return false;
        }
    }
}
