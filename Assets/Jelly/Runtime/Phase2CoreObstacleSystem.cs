using System;
using UnityEngine;

namespace Jelly
{
    public enum MultiHoleVariantType
    {
        SizeChoice = 0,       // Boyut Seçimi (Küçük Delik ≤75 vs Normal Delik 76–114)
        SplitChoice = 1,      // Bölünme Seçimi (Tek Parça Merkez ≤70 vs ÷2 İkiz Yan Delikler)
        RiskRewardChoice = 2, // Risk–Ödül Seçimi (Güvenli Geniş Geçit vs Dar Yüksek Ödüllü Geçit)
        Custom = 3            // Veri odaklı özel açıklık dizilimi
    }

    [Serializable]
    public struct MultiHoleApertureSpec
    {
        public string HoleId;
        public float CenterX;
        public float CenterY;
        public float Diameter;
        public float MinAllowedMass;
        public float MaxAllowedMass;
        public bool RequiresSplitState;
        public bool IsHighRiskBonus;
        public int BaseScoreReward;
        public int BonusComboStacks;
        public Color RimColor;

        public float Radius => Diameter * 0.5f;

        public bool MatchesMass(float candidateMass, float tolerance = 0.01f)
        {
            return candidateMass >= (MinAllowedMass - tolerance) &&
                   candidateMass <= (MaxAllowedMass + tolerance);
        }

        public bool MatchesLateralAlignment(float candidateX, float alignmentMargin = 0.22f)
        {
            float allowedHalfSpan = Radius + alignmentMargin;
            return Mathf.Abs(candidateX - CenterX) <= allowedHalfSpan;
        }
    }

    [Serializable]
    public class MultiHoleWallConfig
    {
        public MultiHoleVariantType VariantType = MultiHoleVariantType.SizeChoice;
        public string HeaderTitle = "ÇOKLU DELİK DUVARI";
        public bool IsSolidUnbreakableWall = true;
        public MultiHoleApertureSpec[] Apertures = Array.Empty<MultiHoleApertureSpec>();

        public static MultiHoleWallConfig CreatePreset(MultiHoleVariantType variant)
        {
            switch (variant)
            {
                case MultiHoleVariantType.SizeChoice:
                    return CreateSizeChoicePreset();
                case MultiHoleVariantType.SplitChoice:
                    return CreateSplitChoicePreset();
                case MultiHoleVariantType.RiskRewardChoice:
                    return CreateRiskRewardPreset();
                default:
                    return CreateSizeChoicePreset();
            }
        }

        /// <summary>
        /// Varyasyon 1 — Boyut Seçimi:
        /// Sol delik Küçük Jöle (≤75, Ø0.82m), Sağ delik Normal Jöle (76–114, Ø1.14m).
        /// </summary>
        public static MultiHoleWallConfig CreateSizeChoicePreset()
        {
            return new MultiHoleWallConfig
            {
                VariantType = MultiHoleVariantType.SizeChoice,
                HeaderTitle = "ÇOKLU DELİK: BOYUT SEÇİMİ (Sol ≤75 | Sağ 76-114)",
                IsSolidUnbreakableWall = true,
                Apertures = new[]
                {
                    new MultiHoleApertureSpec
                    {
                        HoleId = "Size_SmallLeft",
                        CenterX = -1.55f,
                        CenterY = 0.90f,
                        Diameter = 0.82f,
                        MinAllowedMass = 20f,
                        MaxAllowedMass = 75f,
                        RequiresSplitState = false,
                        IsHighRiskBonus = false,
                        BaseScoreReward = 40,
                        BonusComboStacks = 1,
                        RimColor = new Color(0.20f, 0.92f, 1.00f)
                    },
                    new MultiHoleApertureSpec
                    {
                        HoleId = "Size_NormalRight",
                        CenterX = 1.55f,
                        CenterY = 1.05f,
                        Diameter = 1.14f,
                        MinAllowedMass = 76f,
                        MaxAllowedMass = 114f,
                        RequiresSplitState = false,
                        IsHighRiskBonus = false,
                        BaseScoreReward = 40,
                        BonusComboStacks = 1,
                        RimColor = new Color(0.22f, 0.96f, 0.54f)
                    }
                }
            };
        }

        /// <summary>
        /// Varyasyon 2 — Bölünme Seçimi:
        /// Merkezde dar tekil delik (≤70, Ø0.78m) veya yanlarda ÷2 İkiz Bölünme delikleri (X = ±1.32m..±1.85m, her parça ≤85).
        /// </summary>
        public static MultiHoleWallConfig CreateSplitChoicePreset()
        {
            return new MultiHoleWallConfig
            {
                VariantType = MultiHoleVariantType.SplitChoice,
                HeaderTitle = "ÇOKLU DELİK: BÖLÜNME SEÇİMİ (Merkez ≤70 veya ÷2 İkiz)",
                IsSolidUnbreakableWall = true,
                Apertures = new[]
                {
                    new MultiHoleApertureSpec
                    {
                        HoleId = "Split_LeftTwin",
                        CenterX = -1.32f,
                        CenterY = 0.95f,
                        Diameter = 0.88f,
                        MinAllowedMass = 20f,
                        MaxAllowedMass = 85f,
                        RequiresSplitState = true,
                        IsHighRiskBonus = false,
                        BaseScoreReward = 30,
                        BonusComboStacks = 1,
                        RimColor = new Color(0.88f, 0.34f, 1.00f)
                    },
                    new MultiHoleApertureSpec
                    {
                        HoleId = "Split_CenterSingle",
                        CenterX = 0.0f,
                        CenterY = 0.88f,
                        Diameter = 0.78f,
                        MinAllowedMass = 20f,
                        MaxAllowedMass = 70f,
                        RequiresSplitState = false,
                        IsHighRiskBonus = false,
                        BaseScoreReward = 35,
                        BonusComboStacks = 1,
                        RimColor = new Color(0.20f, 0.92f, 1.00f)
                    },
                    new MultiHoleApertureSpec
                    {
                        HoleId = "Split_RightTwin",
                        CenterX = 1.32f,
                        CenterY = 0.95f,
                        Diameter = 0.88f,
                        MinAllowedMass = 20f,
                        MaxAllowedMass = 85f,
                        RequiresSplitState = true,
                        IsHighRiskBonus = false,
                        BaseScoreReward = 30,
                        BonusComboStacks = 1,
                        RimColor = new Color(0.88f, 0.34f, 1.00f)
                    }
                }
            };
        }

        /// <summary>
        /// Varyasyon 3 — Risk–Ödül Seçimi:
        /// Sol güvenli geniş açıklık (≤114, Ø1.18m, +30 Puan) vs Sağ dar yüksek ödüllü delik (≤68, Ø0.74m, +90 Puan & Bonus Kombo).
        /// </summary>
        public static MultiHoleWallConfig CreateRiskRewardPreset()
        {
            return new MultiHoleWallConfig
            {
                VariantType = MultiHoleVariantType.RiskRewardChoice,
                HeaderTitle = "ÇOKLU DELİK: RİSK–ÖDÜL (Sol Güvenli | Sağ ★≤68 x2 Ödül)",
                IsSolidUnbreakableWall = true,
                Apertures = new[]
                {
                    new MultiHoleApertureSpec
                    {
                        HoleId = "Risk_SafeLeft",
                        CenterX = -1.45f,
                        CenterY = 1.05f,
                        Diameter = 1.18f,
                        MinAllowedMass = 20f,
                        MaxAllowedMass = 114f,
                        RequiresSplitState = false,
                        IsHighRiskBonus = false,
                        BaseScoreReward = 30,
                        BonusComboStacks = 1,
                        RimColor = new Color(0.24f, 0.86f, 1.00f)
                    },
                    new MultiHoleApertureSpec
                    {
                        HoleId = "Risk_BonusRight",
                        CenterX = 1.65f,
                        CenterY = 0.88f,
                        Diameter = 0.74f,
                        MinAllowedMass = 20f,
                        MaxAllowedMass = 68f,
                        RequiresSplitState = false,
                        IsHighRiskBonus = true,
                        BaseScoreReward = 90,
                        BonusComboStacks = 2,
                        RimColor = new Color(1.00f, 0.82f, 0.16f)
                    }
                }
            };
        }
    }

    public enum Phase2ObstacleOutcome
    {
        CleanPass,
        BonusRiskPass,
        CrystalShatterPass,
        PartialSplitSacrifice,
        SolidWallBounce
    }

    public struct Phase2ObstacleEvaluation
    {
        public Phase2ObstacleOutcome Outcome;
        public bool CanPass;
        public bool ShatteredCrystal;
        public bool SacrificedOneSplitPiece;
        public float SurvivingLateralX;
        public int ScoreAwarded;
        public int ComboIncrement;
        public string MatchedApertureId;
        public string FeedbackBanner;
    }

    /// <summary>
    /// Unified, deterministic evaluator for Phase 2's 5 Approved Core Obstacles:
    /// 1. SmallCenterHole (Dar Geçit)
    /// 2. VerticalCrusherGate (Piston Kapı)
    /// 3. HeavySmashWall (Kırılabilir Cam Duvar)
    /// 4. TwinSplitTunnels (İkiz Tünel)
    /// 5. MultiHoleDecisionWall (Çoklu Delik Duvarı - 3 Varyasyon + Özel)
    /// Enforces the strict rule: Solid walls NEVER break; only HeavySmashWall shatters when mass >= threshold.
    /// </summary>
    public static class Phase2CoreObstacleEvaluator
    {
        public const float DefaultSplitHalfSeparation = 1.32f;

        public static float ComputeVisualDiameter(float mass)
        {
            if (mass <= 75f)
            {
                float t = Mathf.InverseLerp(30f, 75f, mass);
                return Mathf.Lerp(0.62f, 0.85f, t);
            }
            if (mass < 115f)
            {
                float t = Mathf.InverseLerp(76f, 114f, mass);
                return Mathf.Lerp(0.92f, 1.08f, t);
            }
            float tGiant = Mathf.InverseLerp(115f, 170f, mass);
            return Mathf.Lerp(1.16f, 1.65f, tGiant);
        }

        public static Phase2ObstacleEvaluation EvaluateSmallCenterHole(
            float totalMass,
            float centerX,
            bool isSplit,
            float maxAllowedMass = 75f,
            float holeHalfTolerance = 0.88f)
        {
            if (isSplit)
            {
                float fragmentMass = totalMass * 0.5f;
                float leftX = centerX - DefaultSplitHalfSeparation;
                float rightX = centerX + DefaultSplitHalfSeparation;
                bool leftInHole = Mathf.Abs(leftX) <= holeHalfTolerance && fragmentMass <= maxAllowedMass + 0.01f;
                bool rightInHole = Mathf.Abs(rightX) <= holeHalfTolerance && fragmentMass <= maxAllowedMass + 0.01f;

                if (leftInHole || rightInHole)
                {
                    return new Phase2ObstacleEvaluation
                    {
                        Outcome = Phase2ObstacleOutcome.PartialSplitSacrifice,
                        CanPass = true,
                        ShatteredCrystal = false,
                        SacrificedOneSplitPiece = true,
                        SurvivingLateralX = leftInHole ? leftX : rightX,
                        ScoreAwarded = 20,
                        ComboIncrement = 0,
                        MatchedApertureId = "SmallCenterHole",
                        FeedbackBanner = "⚠ TEK DELİK! Delikten geçen parçayla devam (-%50 Kütle)!"
                    };
                }

                return new Phase2ObstacleEvaluation
                {
                    Outcome = Phase2ObstacleOutcome.SolidWallBounce,
                    CanPass = false,
                    ShatteredCrystal = false,
                    FeedbackBanner = "💥 KATI DUVARA ÇARPTIN (-22 Kütle)! Ortadaki deliğe hizalan ve küçül!"
                };
            }

            bool aligned = Mathf.Abs(centerX) <= holeHalfTolerance;
            bool smallEnough = totalMass <= maxAllowedMass + 0.01f;
            if (aligned && smallEnough)
            {
                return new Phase2ObstacleEvaluation
                {
                    Outcome = Phase2ObstacleOutcome.CleanPass,
                    CanPass = true,
                    ShatteredCrystal = false,
                    ScoreAwarded = 35,
                    ComboIncrement = 1,
                    MatchedApertureId = "SmallCenterHole",
                    FeedbackBanner = "✓ DAR GEÇİTTEN SÜZÜLDÜN! (+35 Puan)"
                };
            }

            return new Phase2ObstacleEvaluation
            {
                Outcome = Phase2ObstacleOutcome.SolidWallBounce,
                CanPass = false,
                ShatteredCrystal = false,
                FeedbackBanner = !aligned
                    ? "💥 KATI DUVARA ÇARPTIN (-22 Kütle)! Ortadaki deliğe yönel!"
                    : $"💥 DELİK İÇİN BÜYÜKTÜN (Kütle {totalMass:F0} > {maxAllowedMass:F0})!"
            };
        }

        public static Phase2ObstacleEvaluation EvaluateVerticalCrusherGate(
            float totalMass,
            float centerX,
            bool isSplit,
            float currentCrusherBottomY,
            float maxSmallJellyMass = 75f)
        {
            // Out of track bounds check
            if (Mathf.Abs(centerX) > 2.65f)
            {
                return new Phase2ObstacleEvaluation
                {
                    Outcome = Phase2ObstacleOutcome.SolidWallBounce,
                    CanPass = false,
                    ShatteredCrystal = false,
                    FeedbackBanner = "💥 PİSTON DİREĞİNE ÇARPTIN (-22 Kütle)!"
                };
            }

            float effectiveMass = isSplit ? totalMass * 0.5f : totalMass;
            float jellyHeight = ComputeVisualDiameter(effectiveMass);

            bool smallClearanceOk = effectiveMass <= maxSmallJellyMass + 0.01f && currentCrusherBottomY >= 0.62f;
            bool heightClearanceOk = currentCrusherBottomY >= jellyHeight - 0.04f;

            if (smallClearanceOk || heightClearanceOk)
            {
                return new Phase2ObstacleEvaluation
                {
                    Outcome = Phase2ObstacleOutcome.CleanPass,
                    CanPass = true,
                    ShatteredCrystal = false,
                    ScoreAwarded = 40,
                    ComboIncrement = 1,
                    MatchedApertureId = "VerticalCrusherUnderpass",
                    FeedbackBanner = "✓ PİSTON KAPININ ALTINDAN GEÇTİN! (+40 Puan)"
                };
            }

            return new Phase2ObstacleEvaluation
            {
                Outcome = Phase2ObstacleOutcome.SolidWallBounce,
                CanPass = false,
                ShatteredCrystal = false,
                FeedbackBanner = "💥 İNEN PİSTONA ÇARPTIN (-22 Kütle)! Küçük Jöle ol veya piston kalkınca geç!"
            };
        }

        public static Phase2ObstacleEvaluation EvaluateHeavySmashWall(
            float totalMass,
            float centerX,
            bool isSplit,
            float minRequiredMassForSmash = 115f,
            float panelHalfWidth = 1.55f)
        {
            float effectiveMass = isSplit ? totalMass * 0.5f : totalMass;
            bool alignedWithCrystal = isSplit
                ? (Mathf.Abs(centerX - DefaultSplitHalfSeparation) <= panelHalfWidth ||
                   Mathf.Abs(centerX + DefaultSplitHalfSeparation) <= panelHalfWidth)
                : Mathf.Abs(centerX) <= panelHalfWidth;

            if (alignedWithCrystal && effectiveMass >= minRequiredMassForSmash - 0.01f)
            {
                return new Phase2ObstacleEvaluation
                {
                    Outcome = Phase2ObstacleOutcome.CrystalShatterPass,
                    CanPass = true,
                    ShatteredCrystal = true,
                    ScoreAwarded = 45,
                    ComboIncrement = 1,
                    MatchedApertureId = "BreakableCrystalPanel",
                    FeedbackBanner = "💥 DEV JÖLEYLE KRİSTAL CAM DUVARI PARÇALADIN! (+45 Puan)"
                };
            }

            return new Phase2ObstacleEvaluation
            {
                Outcome = Phase2ObstacleOutcome.SolidWallBounce,
                CanPass = false,
                ShatteredCrystal = false,
                FeedbackBanner = !alignedWithCrystal
                    ? "💥 ÇERÇEVE DİREĞİNE ÇARPTIN (-22 Kütle)! Ortadaki kristale hizalan!"
                    : $"💥 KRİSTAL DUVAR İÇİN HAFİFTİN (Etkin Kütle {effectiveMass:F0} < {minRequiredMassForSmash:F0})!"
            };
        }

        public static Phase2ObstacleEvaluation EvaluateTwinSplitTunnels(
            float totalMass,
            float centerX,
            bool isSplit,
            float maxSingleTunnelMass = 95f)
        {
            if (isSplit)
            {
                float fragMass = totalMass * 0.5f;
                if (fragMass > maxSingleTunnelMass + 0.01f)
                {
                    return new Phase2ObstacleEvaluation
                    {
                        Outcome = Phase2ObstacleOutcome.SolidWallBounce,
                        CanPass = false,
                        ShatteredCrystal = false,
                        FeedbackBanner = "💥 PARÇALAR TÜNEL İÇİN ÇOK BÜYÜK (-22 Kütle)!"
                    };
                }

                float leftX = centerX - DefaultSplitHalfSeparation;
                float rightX = centerX + DefaultSplitHalfSeparation;

                bool leftInLeft = leftX >= -2.35f && leftX <= -0.42f;
                bool rightInRight = rightX >= 0.42f && rightX <= 2.35f;

                if (leftInLeft && rightInRight)
                {
                    return new Phase2ObstacleEvaluation
                    {
                        Outcome = Phase2ObstacleOutcome.CleanPass,
                        CanPass = true,
                        ShatteredCrystal = false,
                        ScoreAwarded = 40,
                        ComboIncrement = 1,
                        MatchedApertureId = "TwinTunnels_Both",
                        FeedbackBanner = "✓ İKİZ TÜNELDEN BÖLÜNEREK GEÇTİN! (+40 Puan)"
                    };
                }

                bool leftValidAny = (leftX >= -2.35f && leftX <= -0.42f) || (leftX >= 0.42f && leftX <= 2.35f);
                bool rightValidAny = (rightX >= -2.35f && rightX <= -0.42f) || (rightX >= 0.42f && rightX <= 2.35f);

                if (leftValidAny || rightValidAny)
                {
                    return new Phase2ObstacleEvaluation
                    {
                        Outcome = Phase2ObstacleOutcome.PartialSplitSacrifice,
                        CanPass = true,
                        ShatteredCrystal = false,
                        SacrificedOneSplitPiece = true,
                        SurvivingLateralX = leftValidAny ? leftX : rightX,
                        ScoreAwarded = 20,
                        ComboIncrement = 0,
                        MatchedApertureId = "TwinTunnels_SingleSurvivor",
                        FeedbackBanner = "⚠ BİR PARÇA DİREĞE ÇARPTI! Tünelden geçen parçayla devam (-%50 Kütle)!"
                    };
                }

                return new Phase2ObstacleEvaluation
                {
                    Outcome = Phase2ObstacleOutcome.SolidWallBounce,
                    CanPass = false,
                    ShatteredCrystal = false,
                    FeedbackBanner = "💥 ORTA DİREĞE ÇARPTIN (-22 Kütle)! Tünellere hizalan!"
                };
            }

            // Unsplit jelly: can pass through a single side tunnel only if aligned with left/right tunnel and mass <= maxSingleTunnelMass
            bool inSideTunnel = Mathf.Abs(centerX) >= 0.72f && Mathf.Abs(centerX) <= 2.25f;
            if (inSideTunnel && totalMass <= maxSingleTunnelMass + 0.01f)
            {
                return new Phase2ObstacleEvaluation
                {
                    Outcome = Phase2ObstacleOutcome.CleanPass,
                    CanPass = true,
                    ShatteredCrystal = false,
                    ScoreAwarded = 30,
                    ComboIncrement = 1,
                    MatchedApertureId = centerX < 0f ? "TwinTunnel_Left" : "TwinTunnel_Right",
                    FeedbackBanner = "✓ YAN TÜNELDEN TEK PARÇA GEÇTİN! (+30 Puan)"
                };
            }

            return new Phase2ObstacleEvaluation
            {
                Outcome = Phase2ObstacleOutcome.SolidWallBounce,
                CanPass = false,
                ShatteredCrystal = false,
                FeedbackBanner = "💥 ORTADAKİ KATI DİREĞE ÇARPTIN (-22 Kütle)! Bölün (÷2) veya yan tünele gir!"
            };
        }

        public static Phase2ObstacleEvaluation EvaluateMultiHoleWall(
            MultiHoleWallConfig config,
            float totalMass,
            float centerX,
            bool isSplit)
        {
            if (config == null || config.Apertures == null || config.Apertures.Length == 0)
            {
                return new Phase2ObstacleEvaluation
                {
                    Outcome = Phase2ObstacleOutcome.SolidWallBounce,
                    CanPass = false,
                    ShatteredCrystal = false,
                    FeedbackBanner = "💥 KATI DUVARA ÇARPTIN!"
                };
            }

            if (isSplit)
            {
                float fragMass = totalMass * 0.5f;
                float leftX = centerX - DefaultSplitHalfSeparation;
                float rightX = centerX + DefaultSplitHalfSeparation;

                int leftHoleIdx = FindMatchingApertureIndex(config.Apertures, fragMass, leftX, true);
                int rightHoleIdx = FindMatchingApertureIndex(config.Apertures, fragMass, rightX, true);

                if (leftHoleIdx >= 0 && rightHoleIdx >= 0)
                {
                    var holeL = config.Apertures[leftHoleIdx];
                    var holeR = config.Apertures[rightHoleIdx];
                    bool isBonus = holeL.IsHighRiskBonus || holeR.IsHighRiskBonus;
                    int totalScore = holeL.BaseScoreReward + (leftHoleIdx != rightHoleIdx ? holeR.BaseScoreReward : 15);
                    int combo = Mathf.Max(holeL.BonusComboStacks, holeR.BonusComboStacks);

                    return new Phase2ObstacleEvaluation
                    {
                        Outcome = isBonus ? Phase2ObstacleOutcome.BonusRiskPass : Phase2ObstacleOutcome.CleanPass,
                        CanPass = true,
                        ShatteredCrystal = false,
                        ScoreAwarded = totalScore,
                        ComboIncrement = combo,
                        MatchedApertureId = $"{holeL.HoleId}+{holeR.HoleId}",
                        FeedbackBanner = $"✓ ÇOKLU DELİKTEN İKİZ PARÇAYLA GEÇTİN! (+{totalScore} Puan)"
                    };
                }

                if (leftHoleIdx >= 0 || rightHoleIdx >= 0)
                {
                    bool leftSurvived = leftHoleIdx >= 0;
                    var survHole = config.Apertures[leftSurvived ? leftHoleIdx : rightHoleIdx];
                    return new Phase2ObstacleEvaluation
                    {
                        Outcome = Phase2ObstacleOutcome.PartialSplitSacrifice,
                        CanPass = true,
                        ShatteredCrystal = false,
                        SacrificedOneSplitPiece = true,
                        SurvivingLateralX = leftSurvived ? leftX : rightX,
                        ScoreAwarded = Mathf.Max(15, survHole.BaseScoreReward / 2),
                        ComboIncrement = 0,
                        MatchedApertureId = survHole.HoleId,
                        FeedbackBanner = "⚠ BİR PARÇA ÇOKLU DUVARA ÇARPTI! Kalan parçayla devam (-%50 Kütle)!"
                    };
                }

                return new Phase2ObstacleEvaluation
                {
                    Outcome = Phase2ObstacleOutcome.SolidWallBounce,
                    CanPass = false,
                    ShatteredCrystal = false,
                    FeedbackBanner = "💥 ÇOKLU DELİK DUVARINA ÇARPTIN (-22 Kütle)! İkiz deliklere hizalan!"
                };
            }

            // Single (unsplit) jelly evaluation
            int matchIdx = FindMatchingApertureIndex(config.Apertures, totalMass, centerX, false);
            if (matchIdx >= 0)
            {
                var hole = config.Apertures[matchIdx];
                return new Phase2ObstacleEvaluation
                {
                    Outcome = hole.IsHighRiskBonus ? Phase2ObstacleOutcome.BonusRiskPass : Phase2ObstacleOutcome.CleanPass,
                    CanPass = true,
                    ShatteredCrystal = false,
                    ScoreAwarded = hole.BaseScoreReward,
                    ComboIncrement = Mathf.Max(1, hole.BonusComboStacks),
                    MatchedApertureId = hole.HoleId,
                    FeedbackBanner = hole.IsHighRiskBonus
                        ? $"★ RİSK DELİĞİNDEN SÜZÜLDÜN! SÜPER ÖDÜL (+{hole.BaseScoreReward} Puan)!"
                        : $"✓ DOĞRU DELİĞİ SEÇTİN! (+{hole.BaseScoreReward} Puan)"
                };
            }

            // Determine helpful failure reason (aligned with a hole but wrong mass/split state vs missed all holes)
            for (int i = 0; i < config.Apertures.Length; i++)
            {
                var hole = config.Apertures[i];
                if (hole.MatchesLateralAlignment(centerX))
                {
                    if (hole.RequiresSplitState)
                    {
                        return new Phase2ObstacleEvaluation
                        {
                            Outcome = Phase2ObstacleOutcome.SolidWallBounce,
                            CanPass = false,
                            ShatteredCrystal = false,
                            FeedbackBanner = "💥 BU DELİK BÖLÜNME (÷2) GEREKTİRİYOR (-22 Kütle)!"
                        };
                    }
                    if (totalMass > hole.MaxAllowedMass)
                    {
                        return new Phase2ObstacleEvaluation
                        {
                            Outcome = Phase2ObstacleOutcome.SolidWallBounce,
                            CanPass = false,
                            ShatteredCrystal = false,
                            FeedbackBanner = $"💥 BU DELİK İÇİN BÜYÜKSÜN (Kütle {totalMass:F0} > {hole.MaxAllowedMass:F0})! Diğer deliği seç veya küçül!"
                        };
                    }
                    if (totalMass < hole.MinAllowedMass)
                    {
                        return new Phase2ObstacleEvaluation
                        {
                            Outcome = Phase2ObstacleOutcome.SolidWallBounce,
                            CanPass = false,
                            ShatteredCrystal = false,
                            FeedbackBanner = $"💥 BU DELİK İÇİN KÜÇÜKSÜN (Kütle {totalMass:F0} < {hole.MinAllowedMass:F0})! Küçük deliğe yönel!"
                        };
                    }
                }
            }

            return new Phase2ObstacleEvaluation
            {
                Outcome = Phase2ObstacleOutcome.SolidWallBounce,
                CanPass = false,
                ShatteredCrystal = false,
                FeedbackBanner = "💥 ÇOKLU DELİK DUVARINA ÇARPTIN (-22 Kütle)! Uygun deliğe hizalan!"
            };
        }

        private static int FindMatchingApertureIndex(
            MultiHoleApertureSpec[] apertures,
            float candidateMass,
            float candidateX,
            bool isSplitFragment)
        {
            for (int i = 0; i < apertures.Length; i++)
            {
                var a = apertures[i];
                if (a.RequiresSplitState && !isSplitFragment)
                    continue;
                if (a.MatchesLateralAlignment(candidateX) && a.MatchesMass(candidateMass))
                    return i;
            }
            return -1;
        }

        // =====================================================================
        // FAZ 4 & FAZ 6 YENİ OBJE AİLELERİ DEĞERLENDİRİCİLERİ
        // =====================================================================

        /// <summary>
        /// Faz 4 (Yüksek Öncelik): Esnek Geçit (FlexibleSqueezeGate).
        /// Jöle daralarak (squash/stretch) uzun ve dar bir açıklıktan geçer (etkin kütle <= 88).
        /// Katı duvar kırılmaz.
        /// </summary>
        public static Phase2ObstacleEvaluation EvaluateFlexibleSqueezeGate(
            float totalMass,
            float centerX,
            bool isSplit,
            float maxSqueezeMass = 88f,
            float corridorHalfWidth = 0.78f)
        {
            if (isSplit)
            {
                float fragMass = totalMass * 0.5f;
                float leftX = centerX - DefaultSplitHalfSeparation;
                float rightX = centerX + DefaultSplitHalfSeparation;
                bool leftAligned = Mathf.Abs(leftX) <= corridorHalfWidth && fragMass <= maxSqueezeMass + 0.01f;
                bool rightAligned = Mathf.Abs(rightX) <= corridorHalfWidth && fragMass <= maxSqueezeMass + 0.01f;

                if (leftAligned || rightAligned)
                {
                    return new Phase2ObstacleEvaluation
                    {
                        Outcome = Phase2ObstacleOutcome.PartialSplitSacrifice,
                        CanPass = true,
                        ShatteredCrystal = false,
                        SacrificedOneSplitPiece = true,
                        SurvivingLateralX = leftAligned ? leftX : rightX,
                        ScoreAwarded = 25,
                        ComboIncrement = 0,
                        MatchedApertureId = "FlexibleSqueezeCorridor",
                        FeedbackBanner = "⚠ ESNEK GEÇİT TEK KORİDORLU! Koridora giren parçayla devam (-%50 Kütle)!"
                    };
                }

                return new Phase2ObstacleEvaluation
                {
                    Outcome = Phase2ObstacleOutcome.SolidWallBounce,
                    CanPass = false,
                    ShatteredCrystal = false,
                    FeedbackBanner = "💥 ESNEK GEÇİT DUVARINA ÇARPTIN (-22 Kütle)! Birleş ve ortadaki koridora gir!"
                };
            }

            bool aligned = Mathf.Abs(centerX) <= corridorHalfWidth;
            bool canSqueeze = totalMass <= maxSqueezeMass + 0.01f;
            if (aligned && canSqueeze)
            {
                return new Phase2ObstacleEvaluation
                {
                    Outcome = Phase2ObstacleOutcome.CleanPass,
                    CanPass = true,
                    ShatteredCrystal = false,
                    ScoreAwarded = 45,
                    ComboIncrement = 1,
                    MatchedApertureId = "FlexibleSqueezeCorridor",
                    FeedbackBanner = "✓ ESNEK GEÇİTTEN UZAYARAK SÜZÜLDÜN! (+45 Puan)"
                };
            }

            return new Phase2ObstacleEvaluation
            {
                Outcome = Phase2ObstacleOutcome.SolidWallBounce,
                CanPass = false,
                ShatteredCrystal = false,
                FeedbackBanner = !aligned
                    ? "💥 ESNEK GEÇİT DUVARINA ÇARPTIN (-22 Kütle)! Ortadaki dar koridora hizalan!"
                    : $"💥 ESNEK KORİDOR İÇİN FAZLA İRİSİN (Kütle {totalMass:F0} > {maxSqueezeMass:F0})!"
            };
        }

        /// <summary>
        /// Faz 4 (Yüksek Öncelik): Kütle Terazisi (MassScaleGate).
        /// Kapıyı açmak için belirli toplam kütle (varsayılan >= 105) gerekir.
        /// Katı çerçeve kırılmaz; terazi eşiği sağlanınca kapı mekanik olarak açılır.
        /// </summary>
        public static Phase2ObstacleEvaluation EvaluateMassScaleGate(
            float totalMass,
            float centerX,
            bool isSplit,
            float minRequiredScaleMass = 105f,
            float scalePadHalfWidth = 1.85f)
        {
            bool onScalePad = Mathf.Abs(centerX) <= scalePadHalfWidth;
            bool heavyEnough = totalMass >= minRequiredScaleMass - 0.01f;

            if (onScalePad && heavyEnough)
            {
                return new Phase2ObstacleEvaluation
                {
                    Outcome = Phase2ObstacleOutcome.CleanPass,
                    CanPass = true,
                    ShatteredCrystal = false,
                    ScoreAwarded = 50,
                    ComboIncrement = 1,
                    MatchedApertureId = "MassScalePlate",
                    FeedbackBanner = $"⚖ KÜTLE TERAZİSİ AÇILDI ({totalMass:F0} ≥ {minRequiredScaleMass:F0})! (+50 Puan)"
                };
            }

            return new Phase2ObstacleEvaluation
            {
                Outcome = Phase2ObstacleOutcome.SolidWallBounce,
                CanPass = false,
                ShatteredCrystal = false,
                FeedbackBanner = !onScalePad
                    ? "💥 TERAZİ DİREĞİNE ÇARPTIN (-22 Kütle)! Ortadaki baskı plakasına bas!"
                    : $"⚖ KÜTLE TERAZİSİ AÇILMADI (Kütle {totalMass:F0} < {minRequiredScaleMass:F0})! Büyü (+) topla!"
            };
        }

        /// <summary>
        /// Faz 4 (Yüksek Öncelik): Hareketli Delik Duvarı (SlidingHoleWall).
        /// Yatayda sağa-sola kayan dairesel delik; oyuncudan doğru zamanlama ve uygun kütle ister.
        /// </summary>
        public static Phase2ObstacleEvaluation EvaluateSlidingHoleWall(
            float totalMass,
            float centerX,
            bool isSplit,
            float currentHoleCenterX,
            float maxHoleMass = 95f,
            float holeHalfTolerance = 0.88f)
        {
            if (isSplit)
            {
                float fragMass = totalMass * 0.5f;
                float leftX = centerX - DefaultSplitHalfSeparation;
                float rightX = centerX + DefaultSplitHalfSeparation;
                bool leftInHole = Mathf.Abs(leftX - currentHoleCenterX) <= holeHalfTolerance && fragMass <= maxHoleMass + 0.01f;
                bool rightInHole = Mathf.Abs(rightX - currentHoleCenterX) <= holeHalfTolerance && fragMass <= maxHoleMass + 0.01f;

                if (leftInHole || rightInHole)
                {
                    return new Phase2ObstacleEvaluation
                    {
                        Outcome = Phase2ObstacleOutcome.PartialSplitSacrifice,
                        CanPass = true,
                        ShatteredCrystal = false,
                        SacrificedOneSplitPiece = true,
                        SurvivingLateralX = leftInHole ? leftX : rightX,
                        ScoreAwarded = 25,
                        ComboIncrement = 0,
                        MatchedApertureId = "SlidingHoleAperture",
                        FeedbackBanner = "⚠ HAREKETLİ DELİKTEN TEK PARÇA GEÇTİ (-%50 Kütle)!"
                    };
                }

                return new Phase2ObstacleEvaluation
                {
                    Outcome = Phase2ObstacleOutcome.SolidWallBounce,
                    CanPass = false,
                    ShatteredCrystal = false,
                    FeedbackBanner = "💥 HAREKETLİ DELİK DUVARINA ÇARPTIN (-22 Kütle)! Kayan deliği yakala!"
                };
            }

            bool aligned = Mathf.Abs(centerX - currentHoleCenterX) <= holeHalfTolerance;
            bool fitsMass = totalMass <= maxHoleMass + 0.01f;
            if (aligned && fitsMass)
            {
                return new Phase2ObstacleEvaluation
                {
                    Outcome = Phase2ObstacleOutcome.CleanPass,
                    CanPass = true,
                    ShatteredCrystal = false,
                    ScoreAwarded = 50,
                    ComboIncrement = 1,
                    MatchedApertureId = "SlidingHoleAperture",
                    FeedbackBanner = "✓ HAREKETLİ DELİĞİ TAM ZAMANINDA YAKALADIN! (+50 Puan)"
                };
            }

            return new Phase2ObstacleEvaluation
            {
                Outcome = Phase2ObstacleOutcome.SolidWallBounce,
                CanPass = false,
                ShatteredCrystal = false,
                FeedbackBanner = !aligned
                    ? "💥 HAREKETLİ DUVARA ÇARPTIN (-22 Kütle)! Kayan deliğin zamanlamasını tuttur!"
                    : $"💥 HAREKETLİ DELİK İÇİN BÜYÜKSÜN (Kütle {totalMass:F0} > {maxHoleMass:F0})!"
            };
        }

        /// <summary>
        /// Faz 6 (İleri Faz): Parçalanma Kapısı (MicroShatterGate).
        /// Jöleyi ızgara lazerlerinden geçirerek çok sayıda küçük parçaya dönüştürür ve bölünme durumuna geçirir.
        /// </summary>
        public static Phase2ObstacleEvaluation EvaluateMicroShatterGate(
            float totalMass,
            float centerX,
            bool isSplit,
            float minRequiredMass = 50f,
            float gridHalfWidth = 2.15f)
        {
            bool inGrid = Mathf.Abs(centerX) <= gridHalfWidth;
            bool enoughMass = totalMass >= minRequiredMass - 0.01f;
            if (inGrid && enoughMass)
            {
                return new Phase2ObstacleEvaluation
                {
                    Outcome = Phase2ObstacleOutcome.BonusRiskPass,
                    CanPass = true,
                    ShatteredCrystal = false,
                    ScoreAwarded = 60,
                    ComboIncrement = 2,
                    MatchedApertureId = "MicroShatterGrid",
                    FeedbackBanner = "✨ PARÇALANMA KAPISI! Çoklu Mikro-Jöle Sürü Halinde Süzüldü! (+60 Puan)"
                };
            }

            return new Phase2ObstacleEvaluation
            {
                Outcome = Phase2ObstacleOutcome.SolidWallBounce,
                CanPass = false,
                ShatteredCrystal = false,
                FeedbackBanner = !inGrid
                    ? "💥 PARÇALANMA KAPISI ÇERÇEVESİNE ÇARPTIN (-22 Kütle)!"
                    : $"💥 PARÇALANMA İÇİN KÜTLE YETERSİZ ({totalMass:F0} < {minRequiredMass:F0})!"
            };
        }
    }
}
