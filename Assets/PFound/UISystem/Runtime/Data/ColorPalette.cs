using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace PFound.UISystem.Data
{
    [Serializable]
    public struct ColorPalette
    {
        [BoxGroup("Primary")]
        public Color Primary;
        [BoxGroup("Primary")]
        public Color OnPrimary;
        [BoxGroup("Primary")]
        public Color PrimaryContainer;
        [BoxGroup("Primary")]
        public Color OnPrimaryContainer;

        [BoxGroup("Secondary")]
        public Color Secondary;
        [BoxGroup("Secondary")]
        public Color OnSecondary;
        [BoxGroup("Secondary")]
        public Color SecondaryContainer;
        [BoxGroup("Secondary")]
        public Color OnSecondaryContainer;

        [BoxGroup("Surface")]
        public Color Surface;
        [BoxGroup("Surface")]
        public Color OnSurface;
        [BoxGroup("Surface")]
        public Color SurfaceVariant;
        [BoxGroup("Surface")]
        public Color OnSurfaceVariant;

        [BoxGroup("Error")]
        public Color Error;
        [BoxGroup("Error")]
        public Color OnError;

        [BoxGroup("Outline")]
        public Color Outline;
        [BoxGroup("Outline")]
        public Color OutlineVariant;

        [BoxGroup("Background")]
        public Color Background;

        [BoxGroup("Inverse")]
        public Color InverseSurface;
        [BoxGroup("Inverse")]
        public Color InverseOnSurface;
        [BoxGroup("Inverse")]
        public Color InversePrimary;

        [BoxGroup("Tertiary")]
        public Color Tertiary;
        [BoxGroup("Tertiary")]
        public Color OnTertiary;
        [BoxGroup("Tertiary")]
        public Color TertiaryContainer;
        [BoxGroup("Tertiary")]
        public Color OnTertiaryContainer;

        [BoxGroup("Surface Containers")]
        public Color SurfaceContainerLowest;
        [BoxGroup("Surface Containers")]
        public Color SurfaceContainerLow;
        [BoxGroup("Surface Containers")]
        public Color SurfaceContainer;
        [BoxGroup("Surface Containers")]
        public Color SurfaceContainerHigh;
        [BoxGroup("Surface Containers")]
        public Color SurfaceContainerHighest;

        [BoxGroup("Primary Fixed")]
        public Color PrimaryFixed;
        [BoxGroup("Primary Fixed")]
        public Color PrimaryFixedDim;
        [BoxGroup("Primary Fixed")]
        public Color OnPrimaryFixed;
        [BoxGroup("Primary Fixed")]
        public Color OnPrimaryFixedVariant;

        [BoxGroup("Secondary Fixed")]
        public Color SecondaryFixed;
        [BoxGroup("Secondary Fixed")]
        public Color SecondaryFixedDim;
        [BoxGroup("Secondary Fixed")]
        public Color OnSecondaryFixed;
        [BoxGroup("Secondary Fixed")]
        public Color OnSecondaryFixedVariant;

        [BoxGroup("Tertiary Fixed")]
        public Color TertiaryFixed;
        [BoxGroup("Tertiary Fixed")]
        public Color TertiaryFixedDim;
        [BoxGroup("Tertiary Fixed")]
        public Color OnTertiaryFixed;
        [BoxGroup("Tertiary Fixed")]
        public Color OnTertiaryFixedVariant;

        [BoxGroup("Error")]
        public Color ErrorContainer;
        [BoxGroup("Error")]
        public Color OnErrorContainer;

        [BoxGroup("Surface")]
        public Color SurfaceDim;
        [BoxGroup("Surface")]
        public Color SurfaceBright;

        [BoxGroup("Utility")]
        public Color Scrim;
        [BoxGroup("Utility")]
        public Color SurfaceTint;
        [BoxGroup("Utility")]
        public Color Shadow;
    }
}
