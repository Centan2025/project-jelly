using UnityEngine;

namespace Jelly
{
    public enum TrackSegmentModuleType
    {
        StandardRoad,
        SplitTwinBridge,        // Ortası uçurumlu ikiye ayrılan çatal köprü (Sol Köprü & Sağ Köprü)
        JumpRampOverAbyss,      // Zıplama rampası + Uçurum + Havadaki Halka
        DoubleDeckerOverpass,   // 2 Katlı Yol: Rampa ile çıkılan Üst Geçit (Cam zeminli) + Alt Tünel
        LaserSlicerGate,        // Mavi Dikey Lazer (Jöleyi ÷2 böler) veya Kırmızı Yatay Tıraşlayıcı Lazer
        SurfaceHazardZone,      // Buz Zemin, Yana İten Konveyör Bant, Manyetik Alan, Yapışkan Zemin, Zıplama Pedi
        ColorMatchSection,      // Boya Havuzu (Mavi/Altın/Mor) + Aynı Renk Enerji Kapısı
        LevelFinaleMultiplier   // Bölüm Sonu x2, x3, x5, x10 Kırılabilir Çarpan Duvarları & Bitiş!
    }

    public enum SurfaceHazardKind
    {
        IceDrift,               // Kaygan buz
        ConveyorPushLeft,       // Sola iten konveyör bant
        ConveyorPushRight,      // Sağa iten konveyör bant
        SlimeMassDrain,         // Kenarlarda kütle eriten zehirli slime şeritleri
        MagneticField,          // Faz 4: Manyetik Alan (Jöleyi sağa veya sola çeker)
        StickyFloor,            // Faz 4: Yapışkan Zemin (Hızı düşürür, hareket kontrolünü değiştirir)
        JumpPad                 // Faz 4: Zıplama Pedi (Jöleyi üst seviyeye fırlatır)
    }

    public enum LaserGateKind
    {
        BlueVerticalAutoSplitter,   // İçinden geçince jöleyi otomatik ikiye (÷2) böler
        RedHorizontalMassTrimmer    // İçinden geçince fazla kütleyi 70'e tıraşlar (dar deliğe hazırlık)
    }

    public class JellyTrackSegmentNode : MonoBehaviour
    {
        public TrackSegmentModuleType moduleType;
        public float zStart;
        public float zEnd;

        // Jump Ramp & Overpass parameters
        public float rampTriggerZ;
        public float launchUpVelocity = 11.5f;
        public float upperDeckHeight = 2.5f;
        public float glassFloorZStart;
        public float glassFloorZEnd;
        public Transform breakableGlassFloor;
        public bool glassBroken;

        // Split Bridge parameters
        public float abyssZStart;
        public float abyssZEnd;
        public float centerGapHalfWidth = 0.72f; // |X| < 0.72 is open chasm between left & right bridges!

        // Laser Slicer parameters
        public LaserGateKind laserKind;
        public float laserTriggerZ;
        public bool laserTriggered;

        // Surface Hazard parameters
        public SurfaceHazardKind hazardKind;
        public float conveyorPushForce = 3.2f;

        // Color Match parameters
        public int requiredColorIndex; // 0=Cyan, 1=Gold, 2=Magenta
        public float dyePadZ;
        public float colorGateZ;
        public bool dyeApplied;
        public bool colorGateResolved;

        // Finale Multiplier Wall parameters
        public int multiplierValue = 2;
        public float requiredMassToBreak = 60f;
        public float wallZ;
        public bool wallResolved;
        public Transform breakableWallVisual;
        public bool isFinalGoalLine;
    }
}
